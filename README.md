# PatientFiles — веб-просмотр прикреплённых файлов пациента

Внутренний ASP.NET/IIS-модуль для просмотра файлов, прикреплённых к медицинской карте пациента по `MKABID`.

Модуль получает метаданные из SQL Server, проверяет принадлежность файла пациенту, читает точный файл из SMB-хранилища и показывает его прямо в браузере. UNC-путь к файлу в браузер не передаётся.

## Возможности

- список прикреплённых документов пациента по `MKABID`;
- встроенный просмотр PDF, Word, Excel, PowerPoint, OpenDocument, изображений, SVG, TIFF, CSV/TSV, RTF и TXT;
- поддержка старых `.doc/.xls/.ppt` через Zrimo;
- многостраничный TIFF;
- полноэкранный просмотр;
- печать PDF и печатное представление остальных поддерживаемых форматов;
- скачивание оригинала;
- фильтр по типу документа, сотруднику и формату;
- изменяемая мышью ширина панели со списком документов;
- аудит запросов и ошибок в `App_Data/Logs`;
- локальные JS/WASM-компоненты без внешнего облачного viewer.

## Связь с БД

Модуль использует следующую связь:

```text
hlt_MKAB.MKABID
    -> hlt_MKAB.UGUID = atf_FileInfo.DescGuid
    -> atf_FileInfo.FileInfoID = atf_FileAttachment.rf_FileInfoID
    -> atf_FileAttachment.Path
```

Тип документа берётся из `atf_FileType`.

Файл выдаётся только после повторной проверки пары `MKABID + FileAttachmentID`.

## Поддерживаемые форматы

Основной viewer — **Zrimo 0.1.2**:

`PDF, DOC, DOCX, DOCM, XLS, XLSX, XLSM, PPT, PPTX, PPTM, PPSX, JPG, JPEG, PNG, GIF, WebP, BMP, SVG, AVIF, TIF, TIFF, CSV, TSV`.

Дополнительный viewer — **ShrimpDocViewer 0.5.0**:

`ODT, ODS, ODP, RTF, TXT, Markdown` и fallback для части современных форматов.

`XPS` разрешён для скачивания, но встроенный предпросмотр не реализован.

## Требования

### Сервер приложения

- Windows Server с IIS;
- ASP.NET 4.8 / .NET Framework 4.8;
- IIS Static Content;
- доступ к SQL Server;
- доступ к SMB-хранилищу файлов;
- Application Pool: `.NET CLR v4.0`, `Integrated`;
- сервисная учётная запись пула с правом чтения SMB и записи в `App_Data/Logs`.

### Для сборки browser-viewer

- Node.js `22.13+`;
- npm;
- доступ к npm registry только на этапе сборки.

После сборки Node.js в работе сайта не участвует.

## Установка

### 1. Клонировать проект

```powershell
git clone https://github.com/ipalienko/patient-files-viewer.git C:\inetpub\wwwroot\PatientFiles
cd C:\inetpub\wwwroot\PatientFiles
```

### 2. Создать локальный `web.config`

`web.config` намеренно не хранится в Git, чтобы в репозиторий не попали реквизиты подключения и внутренние адреса.

```powershell
Copy-Item .\web.config.example .\web.config
```

Откройте `web.config` и замените только локальные значения:

- `DB_SERVER` — адрес SQL Server;
- `DB_NAME` — имя базы;
- `DB_USER` / `DB_PASSWORD` — SQL-реквизиты, если используется SQL Authentication;
- `\\FILE_SERVER\SHARE_ROOT` — разрешённый SMB-корень.

Если используется Windows Integrated Security для SQL, замените SQL Authentication в connection string на `Integrated Security=True`.

Никогда не коммитьте рабочий `web.config`.

### 3. Собрать frontend

Из корня проекта:

```powershell
.\BuildViewer.ps1
```

или:

```cmd
BuildViewer.cmd
```

Скрипт выполнит `npm install`, `vite build`, скопирует runtime-assets Zrimo и лицензии в `ViewerDist`.

Проверьте наличие:

```text
ViewerDist\viewer.js
ViewerDist\viewer.css
ViewerDist\zrimo\...
ViewerDist\assets\...
ViewerDist\chunks\...
```

### 4. Создать отдельный сайт IIS

Рекомендуется отдельный сайт и отдельный Application Pool.

Параметры пула:

```text
.NET CLR Version: v4.0
Managed pipeline mode: Integrated
Identity: локальная/доменная сервисная учётная запись с доступом к SMB
```

Физический путь сайта:

```text
C:\inetpub\wwwroot\PatientFiles
```

Текущий вариант проекта рассчитан на внутренний сайт без пользовательской авторизации:

```text
Anonymous Authentication: Enabled
Windows Authentication: Disabled
ASP.NET Impersonation: Disabled
```

Если сайт доступен без Windows Authentication, обязательно ограничьте доступ к нему сетевым экраном/IIS IP Restrictions только нужными внутренними подсетями.

### 5. Права на каталог логов

Учётной записи Application Pool нужны права `Modify` на:

```text
App_Data\Logs
```

### 6. Права SQL

Минимально нужны `SELECT` на:

```text
dbo.hlt_MKAB
dbo.atf_FileInfo
dbo.atf_FileAttachment
dbo.atf_FileType
```

Пример находится в `Sql/03_permissions_example.sql`.

### 7. Проверка

Найдите тестовый `MKABID` запросом:

```text
Sql/02_find_examples.sql
```

Проверьте его файлы:

```text
Sql/01_check_patient_files.sql
```

Откройте:

```text
http://WEB_SERVER:PORT/?mkabid=123456
```

Сначала рекомендуется проверить PDF и JPG/PNG, затем DOC/DOCX, XLS/XLSX, ODT и TIFF.

## Конфигурация

### `AllowedRoots`

Список разрешённых SMB-корней, разделённых `|`.

Пример с условными значениями:

```xml
<add key="AllowedRoots" value="\\FILE_SERVER\SHARE_ROOT|\\FILE_SERVER\ARCHIVE_ROOT" />
```

Путь из БД должен находиться внутри одного из разрешённых корней.

### `FileRoot`

Используется только если в БД хранятся относительные пути.

### `PathMappings`

Нужен для старых путей или букв дисков.

Пример с условными значениями:

```text
X:\=>\\FILE_SERVER\SHARE_ROOT
```

### `MaxPreviewSizeMb`

Максимальный размер файла для передачи в browser-viewer. Скачивание оригинала этим лимитом не ограничивается.

## Безопасность

- браузер не получает UNC-путь;
- `File.ashx` принимает идентификатор файла и повторно проверяет его принадлежность `MKABID`;
- путь проходит allow-list `AllowedRoots`;
- разрешены только неисполняемые расширения;
- макросы Office-документов не выполняются;
- ответы содержат `no-store`;
- viewer-assets хранятся локально;
- внешний CDN или облачный конвертер не используется;
- реальные логины, пароли, IP-адреса, имена серверов и SMB-пути не должны храниться в Git.

## Печать

- PDF печатается через оригинальный PDF в браузере;
- Word/PowerPoint/изображения/TIFF и другие форматы Zrimo рендерятся в отдельное печатное представление;
- ODT/ODP/RTF/TXT/Markdown через Shrimp печатаются постранично;
- ODS печатает текущий вид листа.

## Интеграция

Для перехода из другого модуля достаточно передать только `MKABID`:

```javascript
window.open('/?mkabid=' + encodeURIComponent(mkabid), '_blank', 'noopener');
```

Пример также есть в `Integration/AddButtonExample.txt`.

## Сторонние компоненты

См. `THIRD_PARTY_NOTICES.md`.

- Zrimo — MIT OR Apache-2.0;
- ShrimpDocViewer — MIT OR Apache-2.0.
