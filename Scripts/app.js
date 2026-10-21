(function () {
  'use strict';

  const qs = new URLSearchParams(location.search);
  const mkabid = qs.get('mkabid');

  const body = document.getElementById('filesBody');
  const patientLine = document.getElementById('patientLine');
  const countLine = document.getElementById('countLine');
  const message = document.getElementById('message');
  const filterInput = document.getElementById('filterInput');
  const workspace = document.getElementById('workspace');
  const splitter = document.getElementById('splitter');
  const previewCard = document.getElementById('previewCard');
  const previewEmpty = document.getElementById('previewEmpty');
  const previewBusy = document.getElementById('previewBusy');
  const busyText = document.getElementById('busyText');
  const previewError = document.getElementById('previewError');
  const viewerHost = document.getElementById('viewerHost');
  const nativeFrame = document.getElementById('nativeFrame');
  const nativeImageWrap = document.getElementById('nativeImageWrap');
  const nativeImage = document.getElementById('nativeImage');
  const previewTitle = document.getElementById('previewTitle');
  const previewMeta = document.getElementById('previewMeta');
  const downloadBtn = document.getElementById('downloadBtn');
  const fullscreenBtn = document.getElementById('fullscreenBtn');
  const printBtn = document.getElementById('printBtn');
  const uploadBtn = document.getElementById('uploadBtn');
  const uploadModal = document.getElementById('uploadModal');
  const uploadCloseBtn = document.getElementById('uploadCloseBtn');
  const uploadCancelBtn = document.getElementById('uploadCancelBtn');
  const uploadForm = document.getElementById('uploadForm');
  const uploadFile = document.getElementById('uploadFile');
  const uploadFileType = document.getElementById('uploadFileType');
  const uploadDocumentDate = document.getElementById('uploadDocumentDate');
  const uploadDescription = document.getElementById('uploadDescription');
  const uploadStatus = document.getElementById('uploadStatus');
  const uploadSubmitBtn = document.getElementById('uploadSubmitBtn');

  let files = [];
  let activeId = null;
  let activeFile = null;
  let viewerModulePromise = null;
  let requestSerial = 0;
  let uploadTypesLoaded = false;
  let uploadInProgress = false;

  const NATIVE_IMAGE = new Set(['.jpg', '.jpeg', '.png', '.gif', '.webp', '.bmp', '.svg', '.avif']);
  const SPLITTER_STORAGE_KEY = 'patientFiles.leftPanelWidth';

  function esc(s) {
    return String(s == null ? '' : s).replace(/[&<>"']/g, c => ({
      '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;'
    }[c]));
  }

  function showError(text) {
    message.textContent = text;
    message.classList.remove('hidden');
  }

  function clearError() {
    message.classList.add('hidden');
    message.textContent = '';
  }

  function parseDate(value) {
    if (!value) return null;
    if (value instanceof Date) return isNaN(value.getTime()) ? null : value;

    if (typeof value === 'number') {
      const d = new Date(value);
      return isNaN(d.getTime()) ? null : d;
    }

    const text = String(value).trim();
    const dotNet = /^\/Date\((-?\d+)(?:[+-]\d+)?\)\/$/.exec(text);
    if (dotNet) {
      const d = new Date(Number(dotNet[1]));
      return isNaN(d.getTime()) ? null : d;
    }

    const d = new Date(text);
    return isNaN(d.getTime()) ? null : d;
  }

  function fmtDate(value) {
    const d = parseDate(value);
    if (!d) return value ? String(value) : '—';
    return d.toLocaleDateString('ru-RU', {
      day: '2-digit', month: '2-digit', year: 'numeric'
    });
  }

  function fmtDateTime(value) {
    const d = parseDate(value);
    if (!d) return value ? String(value) : '—';
    return d.toLocaleString('ru-RU', {
      day: '2-digit', month: '2-digit', year: 'numeric',
      hour: '2-digit', minute: '2-digit'
    });
  }

  function formatOf(f) {
    let ext = String((f && f.extension) || '').trim();
    if (!ext && f && f.fileName) {
      const i = f.fileName.lastIndexOf('.');
      if (i >= 0) ext = f.fileName.slice(i);
    }
    return ext.replace(/^\./, '').toUpperCase() || '—';
  }

  function fileUrl(id, mode) {
    return 'File.ashx?id=' + encodeURIComponent(id) +
      '&mkabid=' + encodeURIComponent(mkabid) +
      '&mode=' + encodeURIComponent(mode || 'preview');
  }

  async function load() {
    clearError();
    activeFile = null;
    printBtn.classList.add('hidden');

    if (!mkabid || !/^\d+$/.test(mkabid)) {
      patientLine.textContent = 'Не передан корректный MKABID';
      showError('Откройте страницу как Default.aspx?mkabid=123456');
      return;
    }

    body.innerHTML = '<tr class="empty-row"><td colspan="5">Загрузка…</td></tr>';

    try {
      const r = await fetch('Api.ashx?action=list&mkabid=' + encodeURIComponent(mkabid), {
        credentials: 'same-origin',
        cache: 'no-store'
      });
      const data = await r.json();
      if (!r.ok || !data.ok) throw new Error(data.error || ('HTTP ' + r.status));

      files = data.files || [];
      const p = data.patient || {};
      patientLine.textContent = (p.fio || 'Пациент') +
        ' · ' + (p.birthDate ? fmtDate(p.birthDate) : 'дата рождения не указана') +
        ' · MKABID ' + mkabid;
      render();
    } catch (e) {
      body.innerHTML = '';
      countLine.textContent = 'Ошибка';
      showError('Не удалось получить список файлов: ' + e.message);
    }
  }

  function render() {
    const q = (filterInput.value || '').trim().toLowerCase();
    const filtered = files.filter(f => [
      f.fileName, f.fileType, f.description, f.doctorName, f.createUserName, formatOf(f)
    ].join(' ').toLowerCase().includes(q));

    countLine.textContent = 'Найдено: ' + filtered.length + (q ? ' из ' + files.length : '');

    if (!filtered.length) {
      body.innerHTML = '<tr class="empty-row"><td colspan="5">Прикреплённых файлов не найдено</td></tr>';
      return;
    }

    body.innerHTML = filtered.map(f => `<tr data-id="${f.attachmentId}" class="${Number(f.attachmentId) === Number(activeId) ? 'active' : ''}">
      <td class="date-cell">${esc(fmtDate(f.documentDate || f.createdAt))}</td>
      <td><span class="tag">${esc(f.fileType || 'Файл')}</span>${f.description ? '<div class="small">' + esc(f.description) + '</div>' : ''}</td>
      <td class="format-cell"><span class="file-format">${esc(formatOf(f))}</span></td>
      <td>${esc(f.createUserName || f.doctorName || '—')}${f.doctorName && f.createUserName && f.doctorName !== f.createUserName ? '<div class="small">' + esc(f.doctorName) + '</div>' : ''}</td>
      <td class="file-actions">›</td>
    </tr>`).join('');

    body.querySelectorAll('tr[data-id]').forEach(tr => {
      tr.addEventListener('click', () => openPreview(Number(tr.dataset.id), tr));
    });
  }

  function resetPreviewSurface() {
    previewEmpty.classList.add('hidden');
    previewError.classList.add('hidden');
    previewError.textContent = '';
    viewerHost.classList.add('hidden');
    nativeFrame.classList.add('hidden');
    nativeFrame.removeAttribute('src');
    nativeImageWrap.classList.add('hidden');
    nativeImage.removeAttribute('src');
  }

  function showBusy(text) {
    resetPreviewSurface();
    busyText.textContent = text || 'Загрузка документа…';
    previewBusy.classList.remove('hidden');
  }

  function hideBusy() {
    previewBusy.classList.add('hidden');
  }

  function showPreviewError(text) {
    hideBusy();
    resetPreviewSurface();
    previewError.textContent = text;
    previewError.classList.remove('hidden');
  }

  function loadViewerModule() {
    if (!viewerModulePromise) {
      viewerModulePromise = import('../ViewerDist/viewer.js').catch(e => {
        viewerModulePromise = null;
        throw e;
      });
    }
    return viewerModulePromise;
  }

  async function destroyViewerIfLoaded() {
    if (!viewerModulePromise) return;
    try {
      const mod = await viewerModulePromise;
      if (mod && mod.destroyCurrent) await mod.destroyCurrent();
    } catch (_) {
      // Viewer может быть ещё не собран.
    }
  }

  async function useNativeFallback(f, url, reason) {
    const ext = (f.extension || '').toLowerCase();
    await destroyViewerIfLoaded();
    hideBusy();
    resetPreviewSurface();

    if (ext === '.pdf') {
      nativeFrame.src = url;
      nativeFrame.classList.remove('hidden');
      previewMeta.textContent = buildPreviewMeta(f, 'нативный PDF viewer');
      return true;
    }

    if (NATIVE_IMAGE.has(ext)) {
      nativeImage.src = url;
      nativeImageWrap.classList.remove('hidden');
      previewMeta.textContent = buildPreviewMeta(f, 'нативный просмотр изображения');
      return true;
    }

    showPreviewError(
      'Встроенный Office-viewer не готов или не смог открыть этот файл. ' +
      'Сначала выполните BuildViewer.cmd в папке модуля. ' +
      (reason ? 'Технически: ' + reason : '')
    );
    return false;
  }

  function buildPreviewMeta(f, suffix) {
    const parts = [
      f.fileType || 'Документ',
      fmtDate(f.documentDate || f.createdAt),
      formatOf(f)
    ];
    if (suffix) parts.push(suffix);
    return parts.filter(Boolean).join(' · ');
  }

  async function openPreview(id, tr) {
    const serial = ++requestSerial;
    const f = files.find(x => Number(x.attachmentId) === Number(id));
    if (!f) return;

    activeId = id;
    activeFile = f;

    body.querySelectorAll('tr.active').forEach(x => x.classList.remove('active'));
    tr.classList.add('active');

    // В заголовке показываем понятный тип документа, а не техническое UUID-имя файла.
    previewTitle.textContent = f.fileType || 'Документ';
    previewMeta.textContent = buildPreviewMeta(f, null);

    downloadBtn.href = fileUrl(id, 'download');
    downloadBtn.classList.remove('hidden');
    fullscreenBtn.classList.remove('hidden');
    printBtn.classList.remove('hidden');

    const url = fileUrl(id, 'preview');
    showBusy('Загрузка документа…');

    try {
      const response = await fetch(url, {
        credentials: 'same-origin',
        cache: 'no-store'
      });

      if (!response.ok) {
        const msg = await response.text();
        throw new Error(msg || ('HTTP ' + response.status));
      }

      const buffer = await response.arrayBuffer();
      if (serial !== requestSerial) return;

      let viewer;
      try {
        viewer = await loadViewerModule();
      } catch (e) {
        await useNativeFallback(f, url, e.message);
        return;
      }
      if (serial !== requestSerial) return;

      resetPreviewSurface();
      previewBusy.classList.remove('hidden');
      viewerHost.classList.remove('hidden');

      await viewer.renderDocument(
        viewerHost,
        buffer,
        f.fileName || ('file' + (f.extension || ''))
      );

      if (serial !== requestSerial) return;
      hideBusy();
      viewerHost.classList.remove('hidden');
      previewMeta.textContent = buildPreviewMeta(f, 'встроенный просмотр');
    } catch (e) {
      if (serial !== requestSerial) return;

      const ext = (f.extension || '').toLowerCase();
      if (ext === '.pdf' || NATIVE_IMAGE.has(ext)) {
        await useNativeFallback(f, url, e.message);
      } else {
        showPreviewError('Не удалось открыть документ: ' + e.message);
      }
    }
  }

  function todayIsoLocal() {
    const d = new Date();
    const local = new Date(d.getTime() - d.getTimezoneOffset() * 60000);
    return local.toISOString().slice(0, 10);
  }

  function setUploadStatus(text, isError) {
    uploadStatus.textContent = text || '';
    uploadStatus.classList.toggle('hidden', !text);
    uploadStatus.classList.toggle('error', !!isError);
  }

  async function loadUploadTypes() {
    if (uploadTypesLoaded) return;

    uploadFileType.disabled = true;
    uploadFileType.innerHTML = '<option value="">Загрузка списка…</option>';

    try {
      const r = await fetch('Api.ashx?action=types', {
        credentials: 'same-origin',
        cache: 'no-store'
      });
      const data = await r.json();
      if (!r.ok || !data.ok) throw new Error(data.error || ('HTTP ' + r.status));

      const types = data.fileTypes || [];
      uploadFileType.innerHTML = '<option value="">Выберите вид документа</option>' +
        types.map(t => {
          const label = (t.code ? '[' + t.code + '] ' : '') + (t.name || ('Тип ' + t.fileTypeId));
          return '<option value="' + esc(t.fileTypeId) + '">' + esc(label) + '</option>';
        }).join('');
      uploadTypesLoaded = true;
    } catch (e) {
      uploadFileType.innerHTML = '<option value="">Не удалось загрузить список</option>';
      setUploadStatus('Не удалось получить виды документов: ' + e.message, true);
    } finally {
      uploadFileType.disabled = false;
    }
  }

  async function openUploadModal() {
    if (!mkabid || !/^\d+$/.test(mkabid)) {
      showError('Нельзя прикрепить файл: не передан корректный MKABID.');
      return;
    }

    uploadForm.reset();
    uploadDocumentDate.value = todayIsoLocal();
    setUploadStatus('', false);
    uploadModal.classList.remove('hidden');
    document.body.classList.add('modal-open');

    await loadUploadTypes();
    setTimeout(() => uploadFile.focus(), 0);
  }

  function closeUploadModal() {
    if (uploadInProgress) return;
    uploadModal.classList.add('hidden');
    document.body.classList.remove('modal-open');
    setUploadStatus('', false);
  }

  async function submitUpload(e) {
    e.preventDefault();
    if (uploadInProgress) return;

    const selectedFile = uploadFile.files && uploadFile.files[0];
    if (!selectedFile) {
      setUploadStatus('Выберите файл.', true);
      return;
    }
    if (!uploadFileType.value) {
      setUploadStatus('Выберите вид документа.', true);
      return;
    }

    const fd = new FormData(uploadForm);
    fd.set('mkabid', mkabid);

    uploadInProgress = true;
    uploadSubmitBtn.disabled = true;
    uploadCancelBtn.disabled = true;
    uploadCloseBtn.disabled = true;
    uploadSubmitBtn.textContent = 'Прикрепление…';
    setUploadStatus('Загрузка файла и запись в МИС…', false);

    try {
      const r = await fetch('Upload.ashx', {
        method: 'POST',
        body: fd,
        credentials: 'same-origin',
        cache: 'no-store'
      });

      const raw = await r.text();
      let data = null;
      try { data = JSON.parse(raw); } catch (_) { }

      if (!r.ok || !data || !data.ok) {
        let error = data && data.error ? data.error : ('HTTP ' + r.status);
        if (r.status === 413) {
          error = 'Файл слишком большой. Проверьте MaxUploadSizeMb и лимит IIS maxAllowedContentLength.';
        }
        throw new Error(error);
      }

      setUploadStatus('Файл успешно прикреплён.', false);
      const newAttachmentId = Number(data.attachmentId);

      uploadInProgress = false;
      uploadModal.classList.add('hidden');
      document.body.classList.remove('modal-open');
      filterInput.value = '';
      await load();

      const row = body.querySelector('tr[data-id="' + newAttachmentId + '"]');
      if (row) {
        row.scrollIntoView({ block: 'nearest' });
        await openPreview(newAttachmentId, row);
      }
    } catch (e2) {
      setUploadStatus('Не удалось прикрепить файл: ' + (e2 && e2.message ? e2.message : e2), true);
    } finally {
      uploadInProgress = false;
      uploadSubmitBtn.disabled = false;
      uploadCancelBtn.disabled = false;
      uploadCloseBtn.disabled = false;
      uploadSubmitBtn.textContent = 'Прикрепить';
    }
  }

  function initSplitter() {
    if (!workspace || !splitter) return;

    const saved = localStorage.getItem(SPLITTER_STORAGE_KEY);
    if (saved && /^\d+(?:\.\d+)?px$/.test(saved)) {
      document.documentElement.style.setProperty('--files-panel-width', saved);
    }

    let dragging = false;

    splitter.addEventListener('pointerdown', e => {
      if (matchMedia('(max-width: 1180px)').matches) return;
      dragging = true;
      splitter.classList.add('active');
      splitter.setPointerCapture(e.pointerId);
      document.body.classList.add('resizing-panels');
      e.preventDefault();
    });

    splitter.addEventListener('pointermove', e => {
      if (!dragging) return;

      const rect = workspace.getBoundingClientRect();
      const splitterWidth = splitter.getBoundingClientRect().width || 8;
      const minLeft = 340;
      const minRight = 520;
      const maxLeft = Math.max(minLeft, rect.width - minRight - splitterWidth);
      let left = e.clientX - rect.left;
      left = Math.max(minLeft, Math.min(maxLeft, left));

      const value = Math.round(left) + 'px';
      document.documentElement.style.setProperty('--files-panel-width', value);
      localStorage.setItem(SPLITTER_STORAGE_KEY, value);
    });

    const finish = () => {
      dragging = false;
      splitter.classList.remove('active');
      document.body.classList.remove('resizing-panels');
    };

    splitter.addEventListener('pointerup', finish);
    splitter.addEventListener('pointercancel', finish);
    splitter.addEventListener('lostpointercapture', finish);
  }

  function keepPrintFrameUntilDone(frame) {
    let removed = false;
    const cleanup = () => {
      if (removed) return;
      removed = true;
      try { frame.remove(); } catch (_) {}
    };

    // В Chromium window.print() на некоторых машинах возвращает управление
    // ещё до закрытия системного окна печати. Поэтому НЕЛЬЗЯ удалять iframe
    // через 2–3 секунды: удаление browsing context закрывает диалог печати.
    // Удаляем его только после afterprint; длинный таймер — лишь страховка.
    try {
      frame.contentWindow.addEventListener('afterprint', () => {
        setTimeout(cleanup, 500);
      }, { once: true });
    } catch (_) {}

    setTimeout(cleanup, 10 * 60 * 1000);
  }

  function printPdfOriginal(url) {
    const frame = document.createElement('iframe');
    frame.className = 'print-helper-frame';
    frame.src = url;
    frame.onload = () => {
      setTimeout(() => {
        try {
          keepPrintFrameUntilDone(frame);
          frame.contentWindow.focus();
          frame.contentWindow.print();
        } catch (e) {
          frame.remove();
          showError('Не удалось открыть печать PDF: ' + e.message);
        }
      }, 500);
    };
    document.body.appendChild(frame);
  }

  function printNativeImage(url, title) {
    const frame = document.createElement('iframe');
    frame.className = 'print-helper-frame';
    document.body.appendChild(frame);

    const doc = frame.contentDocument;
    doc.open();
    doc.write('<!doctype html><html><head><meta charset="utf-8"><title>' + esc(title || 'Документ') + '</title>' +
      '<style>@page{margin:8mm}html,body{margin:0;padding:0;background:#fff}body{display:flex;justify-content:center;align-items:flex-start}img{display:block;max-width:100%;max-height:270mm;width:auto;height:auto;object-fit:contain}</style>' +
      '</head><body><img id="printImage" alt="Документ"></body></html>');
    doc.close();

    const img = doc.getElementById('printImage');
    img.onload = () => {
      setTimeout(() => {
        try {
          keepPrintFrameUntilDone(frame);
          frame.contentWindow.focus();
          frame.contentWindow.print();
        } catch (e) {
          frame.remove();
          showError('Не удалось открыть печать изображения: ' + e.message);
        }
      }, 150);
    };
    img.onerror = () => {
      frame.remove();
      showError('Не удалось подготовить изображение к печати.');
    };
    img.src = url;
  }

  async function printCurrentDocument() {
    if (!activeFile) return;

    const ext = (activeFile.extension || '').toLowerCase();
    const url = fileUrl(activeFile.attachmentId, 'preview');
    const oldText = printBtn.textContent;
    printBtn.disabled = true;
    printBtn.textContent = 'Подготовка…';

    try {
      // PDF печатаем как оригинальный PDF через браузер — это самый точный путь.
      if (ext === '.pdf') {
        printPdfOriginal(url);
        return;
      }

      // Если сработал нативный fallback для изображения, печатаем сам оригинал,
      // а не DOM страницы с элементами viewer.
      if (!nativeImageWrap.classList.contains('hidden') && NATIVE_IMAGE.has(ext)) {
        printNativeImage(url, activeFile.fileType || 'Изображение');
        return;
      }

      // Word/Excel/PowerPoint/ODT/RTF/TIFF/изображения и остальные форматы
      // готовит к печати сам browser-viewer: каждая страница рендерится отдельно.
      const mod = await loadViewerModule();
      if (!mod || typeof mod.printCurrentDocument !== 'function') {
        throw new Error('В текущей сборке viewer отсутствует модуль печати. Пересоберите frontend.');
      }
      await mod.printCurrentDocument(activeFile.fileType || 'Документ');
    } catch (e) {
      showError('Не удалось подготовить документ к печати: ' + (e && e.message ? e.message : e));
    } finally {
      printBtn.disabled = false;
      printBtn.textContent = oldText;
    }
  }

  filterInput.addEventListener('input', render);
  document.getElementById('refreshBtn').addEventListener('click', load);

  fullscreenBtn.addEventListener('click', async () => {
    try {
      if (!document.fullscreenElement) await previewCard.requestFullscreen();
      else await document.exitFullscreen();
    } catch (e) {
      showPreviewError('Не удалось перейти в полноэкранный режим: ' + e.message);
    }
  });

  printBtn.addEventListener('click', printCurrentDocument);

  uploadBtn.addEventListener('click', openUploadModal);
  uploadCloseBtn.addEventListener('click', closeUploadModal);
  uploadCancelBtn.addEventListener('click', closeUploadModal);
  uploadForm.addEventListener('submit', submitUpload);
  uploadModal.addEventListener('click', e => {
    if (e.target === uploadModal) closeUploadModal();
  });
  document.addEventListener('keydown', e => {
    if (e.key === 'Escape' && !uploadModal.classList.contains('hidden')) closeUploadModal();
  });

  window.addEventListener('beforeunload', () => {
    void destroyViewerIfLoaded();
  });

  initSplitter();
  load();
})();