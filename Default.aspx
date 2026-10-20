<%@ Page Language="C#" AutoEventWireup="true" %>
<!DOCTYPE html>
<html lang="ru">
<head runat="server">
  <meta charset="utf-8" />
  <meta name="viewport" content="width=device-width, initial-scale=1" />
  <title>Прикреплённые файлы пациента — Полимедика</title>
  <link rel="stylesheet" href="Content/site.css" />
  <link rel="stylesheet" href="ViewerDist/viewer.css" />
</head>
<body>
  <div class="page-shell">
    <header class="topbar">
      <div>
        <div class="eyebrow">ПОЛИМЕДИКА · медицинская карта</div>
        <h1>Прикреплённые файлы</h1>
        <div id="patientLine" class="patient-line">Загрузка данных пациента…</div>
      </div>
      <div class="top-actions">
        <button id="refreshBtn" class="btn btn-secondary" type="button">Обновить</button>
      </div>
    </header>

    <main id="workspace" class="layout">
      <section id="filesCard" class="card files-card">
        <div class="card-head">
          <div>
            <h2>Документы</h2>
            <div id="countLine" class="muted">—</div>
          </div>
          <input id="filterInput" class="filter" type="search" placeholder="Фильтр по названию, типу, врачу…" />
        </div>
        <div id="message" class="message hidden"></div>
        <div class="table-wrap">
          <table class="files-table">
            <thead>
              <tr>
                <th>Дата</th>
                <th>Вид документа</th>
                <th>Формат</th>
                <th>Сотрудник / врач</th>
                <th></th>
              </tr>
            </thead>
            <tbody id="filesBody"></tbody>
          </table>
        </div>
      </section>

      <div id="splitter" class="splitter" title="Потяните для изменения ширины списка" role="separator" aria-orientation="vertical" aria-label="Изменить ширину списка документов"></div>

      <section id="previewCard" class="card preview-card">
        <div class="card-head preview-head">
          <div class="preview-title-block">
            <h2 id="previewTitle">Предпросмотр</h2>
            <div id="previewMeta" class="muted">Выберите документ слева</div>
          </div>
          <div class="preview-actions">
            <button id="fullscreenBtn" class="btn btn-secondary hidden" type="button">На весь экран</button>
            <button id="printBtn" class="btn btn-secondary hidden" type="button">Печать</button>
            <a id="downloadBtn" class="btn btn-secondary hidden">Скачать оригинал</a>
          </div>
        </div>

        <div id="previewEmpty" class="preview-empty">
          <div class="preview-icon">⌕</div>
          <div>Нажмите на строку документа, чтобы открыть его прямо в этом окне.</div>
          <div class="preview-note">PDF, Word, Excel, OpenDocument, изображения и TIFF обрабатываются локальным browser-viewer.</div>
        </div>

        <div id="previewBusy" class="preview-busy hidden">
          <div class="spinner"></div>
          <div id="busyText">Загрузка документа…</div>
        </div>

        <div id="previewError" class="preview-error hidden"></div>
        <div id="viewerHost" class="viewer-host hidden" aria-live="polite"></div>
        <iframe id="nativeFrame" class="preview-frame hidden" title="Предпросмотр PDF"></iframe>
        <div id="nativeImageWrap" class="native-image-wrap hidden"><img id="nativeImage" alt="Предпросмотр изображения" /></div>
      </section>
    </main>
  </div>

  <script type="module" src="Scripts/app.js"></script>
</body>
</html>
