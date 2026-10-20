import { ViewerClient } from '@zrimo/viewer';
import '@zrimo/viewer/styles.css';
import { mount as mountShrimp } from 'shrimp-doc-viewer';

const ZRIMO = new Set([
  'pdf',
  'doc', 'docx', 'docm',
  'xls', 'xlsx', 'xlsm',
  'ppt', 'pptx', 'pptm', 'ppsx',
  'jpg', 'jpeg', 'png', 'gif', 'webp', 'bmp', 'svg', 'avif', 'tif', 'tiff',
  'csv', 'tsv'
]);

// Zrimo 0.1.2 пока не включает ODF/RTF/TXT; для них используем ShrimpDocViewer.
const SHRIMP = new Set(['odt', 'ods', 'odp', 'rtf', 'txt', 'md', 'markdown']);
const SHRIMP_FALLBACK = new Set(['docx', 'xlsx', 'pptx', 'jpg', 'jpeg', 'png', 'gif', 'webp', 'bmp', 'svg', 'csv', 'tsv']);

let zrimoClient = null;
let zrimoViewer = null;
let shrimpViewer = null;
let activeKind = null;
let activeFileName = '';

function extensionOf(name) {
  const n = String(name || '').toLowerCase();
  const i = n.lastIndexOf('.');
  return i >= 0 ? n.slice(i + 1) : '';
}

async function clear(container) {
  if (activeKind === 'zrimo' && zrimoViewer) {
    try { await zrimoViewer.destroy(); } catch (_) {}
    zrimoViewer = null;
  }
  if (activeKind === 'shrimp' && shrimpViewer) {
    try { shrimpViewer.destroy(); } catch (_) {}
    shrimpViewer = null;
  }
  activeKind = null;
  activeFileName = '';
  if (container) container.replaceChildren();
}

async function renderZrimo(container, bytes, fileName) {
  if (!zrimoClient) {
    zrimoClient = ViewerClient.create({
      assetBaseUrl: new URL('../zrimo/', import.meta.url)
    });
  }
  zrimoViewer = zrimoClient.createViewer({
    container,
    ui: true,
    fit: 'width',
    locale: 'ru'
  });
  activeKind = 'zrimo';
  activeFileName = fileName || 'Документ';
  await zrimoViewer.load(new Uint8Array(bytes), { fileName });
}

async function renderShrimp(container, bytes, fileName) {
  const ext = extensionOf(fileName);
  shrimpViewer = await mountShrimp(container, new Uint8Array(bytes), {
    format: ext === 'txt' ? 'text' : (ext === 'md' ? 'markdown' : ext),
    height: '100%'
  });
  activeKind = 'shrimp';
  activeFileName = fileName || 'Документ';
}

export function supports(fileName) {
  const ext = extensionOf(fileName);
  return ZRIMO.has(ext) || SHRIMP.has(ext);
}

export async function renderDocument(container, buffer, fileName) {
  if (!(container instanceof HTMLElement)) throw new Error('Не найден контейнер viewer.');
  const ext = extensionOf(fileName);
  await clear(container);

  if (SHRIMP.has(ext)) {
    await renderShrimp(container, buffer, fileName);
    return { engine: 'shrimp', extension: ext };
  }

  if (!ZRIMO.has(ext)) {
    throw new Error('Формат .' + (ext || '?') + ' пока не поддерживается встроенным viewer.');
  }

  try {
    await renderZrimo(container, buffer, fileName);
    return { engine: 'zrimo', extension: ext };
  } catch (e) {
    if (!SHRIMP_FALLBACK.has(ext)) throw e;
    await clear(container);
    await renderShrimp(container, buffer, fileName);
    return { engine: 'shrimp-fallback', extension: ext };
  }
}

function canvasToPng(canvas) {
  return canvas.toDataURL('image/png');
}

async function zrimoPrintPages() {
  if (!zrimoViewer || zrimoViewer.state.status !== 'ready') {
    throw new Error('Документ ещё не готов к печати.');
  }

  const pageCount = Number(zrimoViewer.state.pageCount || 0);
  if (!pageCount) throw new Error('Не удалось определить количество страниц.');

  const images = [];
  for (let i = 0; i < pageCount; i++) {
    const canvas = document.createElement('canvas');
    await zrimoViewer.renderPage(i, canvas, {
      zoom: 1.35,
      devicePixelRatio: 1
    });
    images.push(canvasToPng(canvas));
  }
  return images;
}

async function shrimpPrintPages() {
  if (!shrimpViewer) throw new Error('Документ ещё не готов к печати.');

  // DOCX/ODT/ODP/RTF/TXT/Markdown в Shrimp — постраничный FlowDoc.
  if (shrimpViewer.doc && Number.isInteger(shrimpViewer.pageCount)) {
    const images = [];
    const scale = 1.5;
    for (let i = 0; i < shrimpViewer.pageCount; i++) {
      const rendered = await shrimpViewer.doc.renderPage(i, scale);
      const canvas = document.createElement('canvas');
      canvas.width = rendered.w;
      canvas.height = rendered.h;
      const ctx = canvas.getContext('2d');
      ctx.drawImage(rendered.bitmap, 0, 0);
      rendered.bitmap.close();
      images.push(canvasToPng(canvas));
    }
    return images;
  }

  // Табличный viewer Shrimp (например ODS): печатаем текущий вид листа.
  // Это намеренно лучше, чем печать всей HTML-страницы с toolbar/scrollbar.
  if (shrimpViewer.canvas instanceof HTMLCanvasElement) {
    return [canvasToPng(shrimpViewer.canvas)];
  }

  // Изображение в Shrimp (редкий fallback): берём фактически отображаемый bitmap.
  if (shrimpViewer.img instanceof HTMLImageElement) {
    const canvas = document.createElement('canvas');
    canvas.width = shrimpViewer.img.naturalWidth || shrimpViewer.img.width || 1;
    canvas.height = shrimpViewer.img.naturalHeight || shrimpViewer.img.height || 1;
    canvas.getContext('2d').drawImage(shrimpViewer.img, 0, 0);
    return [canvasToPng(canvas)];
  }

  throw new Error('Для этого формата пока не удалось построить печатное представление.');
}

function waitForImages(doc) {
  const list = Array.from(doc.images || []);
  return Promise.all(list.map(img => {
    if (img.complete) return Promise.resolve();
    return new Promise((resolve, reject) => {
      img.onload = resolve;
      img.onerror = () => reject(new Error('Не удалось подготовить страницу для печати.'));
    });
  }));
}

async function printImages(images, title) {
  if (!images || !images.length) throw new Error('Нет страниц для печати.');

  const frame = document.createElement('iframe');
  frame.setAttribute('aria-hidden', 'true');
  frame.style.cssText = 'position:fixed;right:-10000px;bottom:-10000px;width:1px;height:1px;border:0;opacity:0;pointer-events:none';
  document.body.appendChild(frame);

  const doc = frame.contentDocument;
  doc.open();
  doc.write('<!doctype html><html><head><meta charset="utf-8"><title></title>' +
    '<style>' +
    '@page{margin:8mm}' +
    'html,body{margin:0;padding:0;background:#fff}' +
    '.print-page{display:flex;align-items:flex-start;justify-content:center;width:100%;break-after:page;page-break-after:always}' +
    '.print-page:last-child{break-after:auto;page-break-after:auto}' +
    '.print-page img{display:block;max-width:100%;max-height:270mm;width:auto;height:auto;object-fit:contain}' +
    '</style></head><body></body></html>');
  doc.close();
  doc.title = title || activeFileName || 'Документ';

  for (const src of images) {
    const page = doc.createElement('div');
    page.className = 'print-page';
    const img = doc.createElement('img');
    img.src = src;
    img.alt = 'Страница документа';
    page.appendChild(img);
    doc.body.appendChild(page);
  }

  await waitForImages(doc);
  await new Promise(resolve => setTimeout(resolve, 150));

  let removed = false;
  const cleanup = () => {
    if (removed) return;
    removed = true;
    try { frame.remove(); } catch (_) {}
  };

  // Не удаляем iframe по короткому таймеру. В Chromium на удалённом адресе
  // print() может вернуть управление до закрытия preview, и удаление iframe
  // автоматически закрывает окно печати.
  try {
    frame.contentWindow.addEventListener('afterprint', () => {
      setTimeout(cleanup, 500);
    }, { once: true });
  } catch (_) {}
  setTimeout(cleanup, 10 * 60 * 1000);

  frame.contentWindow.focus();
  frame.contentWindow.print();
}

export async function printCurrentDocument(title) {
  let images;
  if (activeKind === 'zrimo') images = await zrimoPrintPages();
  else if (activeKind === 'shrimp') images = await shrimpPrintPages();
  else throw new Error('Сначала откройте документ.');

  await printImages(images, title || activeFileName || 'Документ');
}

export async function destroyCurrent() {
  await clear(null);
}

export async function destroyAll() {
  await clear(null);
  if (zrimoClient) {
    try { await zrimoClient.destroy(); } catch (_) {}
    zrimoClient = null;
  }
}