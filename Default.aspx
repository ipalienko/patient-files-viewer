<%@ Page Language="C#" AutoEventWireup="true" %>
<!DOCTYPE html>
<html lang="ru">
<head runat="server">
  <meta charset="utf-8" />
  <meta name="viewport" content="width=device-width, initial-scale=1" />
  <title>Прикреплённые файлы пациента — Полимедика</title>
  <link rel="stylesheet" href="Content/site.css" />
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
        <button id="uploadBtn" class="btn btn-primary" type="button">Прикрепить файл</button>
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

  <div id="uploadModal" class="modal-backdrop hidden" role="dialog" aria-modal="true" aria-labelledby="uploadModalTitle">
    <div class="modal-card">
      <div class="modal-head">
        <div>
          <h2 id="uploadModalTitle">Прикрепить файл</h2>
          <div class="muted">Файл будет сохранён в файловом хранилище и привязан к текущей МКАБ.</div>
        </div>
        <button id="uploadCloseBtn" class="icon-btn" type="button" aria-label="Закрыть">×</button>
      </div>

      <form id="uploadForm" class="upload-form">
        <label class="form-field">
          <span>Файл</span>
          <input id="uploadFile" name="file" type="file" required
                 accept=".pdf,.jpg,.jpeg,.png,.gif,.webp,.bmp,.svg,.avif,.tif,.tiff,.doc,.docx,.docm,.rtf,.txt,.xls,.xlsx,.xlsm,.csv,.tsv,.ods,.ppt,.pptx,.pptm,.ppsx,.odp,.odt,.xps" />
        </label>

        <label class="form-field">
          <span>Вид документа</span>
          <select id="uploadFileType" name="fileTypeId" required>
            <option value="">Загрузка списка…</option>
          </select>
        </label>

        <div class="upload-grid">
          <label class="form-field">
            <span>Дата документа</span>
            <input id="uploadDocumentDate" name="documentDate" type="date" required />
          </label>

          <label class="form-field">
            <span>Описание <span class="muted">(необязательно)</span></span>
            <input id="uploadDescription" name="description" type="text" maxlength="2000" placeholder="Краткое описание" />
          </label>
        </div>

        <div id="uploadStatus" class="upload-status hidden"></div>

        <div class="modal-actions">
          <button id="uploadCancelBtn" class="btn btn-secondary" type="button">Отмена</button>
          <button id="uploadSubmitBtn" class="btn btn-primary" type="submit">Прикрепить</button>
        </div>
      </form>
    </div>
  </div>

  <script type="module" src="Scripts/app.js"></script>
</body>
</html>