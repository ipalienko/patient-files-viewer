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
- прикрепление нового файла к текущей МКАБ из браузера;
- выбор вида документа из активного справочника `atf_FileType`;
- сохранение загружаемого файла в SMB-хранилище под UUID-именем с исходным расширением;
- компенсация при ошибке: если запись в БД не создалась, файл удаляется из хранилища;
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
- сервисная учётная запись пула с правом чтения SMB и записи в `App_Data/Logs`;
- для функции прикрепления файлов этой же учётной записи нужны права создания/записи/переименования/удаления в фактическом хранилище из `x_UserSettings`.

### Для сборки browser-viewer

- Node.js `22.13+`;
- npm;
- доступ к npm registry только на этапе сборки.

После сборки Node.js в работе сайта не участвует.

## Установка

### 1. Клонировать проект

```powershell
git clone REPOSITORY_URL C:\inetpub\wwwroot\PatientFiles
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
- `DB_USER` / `DB_PASSWORD` — SQL-реквизиты read-only подключения `MisDb`;
- `DB_WRITE_USER` / `DB_WRITE_PASSWORD` — отдельные SQL-реквизиты для `MisWriteDb`, используемые только при прикреплении файлов;
- `\\FILE_SERVER\SHARE_ROOT` — разрешённый SMB-корень;
- `UploadEnabled` — отдельный выключатель функции прикрепления;
- `UploadCreateUserId` / `UploadCreateUserName` — технический пользователь, который будет записан в `atf_FileAttachment`.

Если используется Windows Integrated Security для SQL, замените SQL Authentication в соответствующей connection string на `Integrated Security=True`.

Рекомендуется не выдавать права записи существующему read-only пользователю просмотра. Для загрузки заведите отдельную минимально-привилегированную учётную запись `MisWriteDb`.

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

Для просмотра минимально нужны `SELECT` на:

```text
dbo.hlt_MKAB
dbo.atf_FileInfo
dbo.atf_FileAttachment
dbo.atf_FileType
```

Подключение `MisWriteDb` для прикрепления файлов должно иметь `SELECT` на:

```text
dbo.hlt_MKAB
dbo.atf_FileType
dbo.x_DocTypeDef
dbo.x_UserSettings
dbo.atf_FileInfo
dbo.atf_FileAttachment
```

и `INSERT` на:

```text
dbo.atf_FileInfo
dbo.atf_FileAttachment
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

Перед первой записью в рабочую БД рекомендуется выполнить `Sql/04_verify_attachment_convention.sql` на МКАБ, куда файл недавно был прикреплён штатно через десктопную МИС. Скрипт только читает данные и помогает сверить фактическое соглашение об именовании файлов.

Для теста загрузки используйте отдельного пациента/тестовую карту: выберите файл, вид документа и дату, после чего убедитесь, что физический файл появился в хранилище, указанном в `x_UserSettings`, а связанные записи корректно появились/переиспользовались в `atf_FileInfo` и `atf_FileAttachment`.

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

### Путь хранения новых файлов

Отдельный `UploadRoot` не используется. Как и `LocalFileToMkabAttachOperator`, модуль читает базовый путь из:

```text
dbo.x_UserSettings
Property = N'Путь к хранилищу прикреплённых файлов'
```

Имя нового файла — `<uuid4>.<исходное расширение>`. В БД сохраняется путь `<ValueStr>\<uuid>.<ext>`. Для физического доступа IIS может применяться `PathMappings`; итоговый путь обязан находиться внутри `AllowedRoots`.

Сначала файл пишется во временное имя `.uploading`, затем переименовывается в окончательное UUID-имя. При ошибке до успешной записи в БД физический файл удаляется.

### `UploadEnabled`

По умолчанию `false`. После выдачи SQL/SMB-прав и проверки на тестовой МКАБ установите `true`.

### `MaxUploadSizeMb`

Лимит размера загрузки на уровне приложения. У IIS есть отдельный лимит `maxAllowedContentLength`; если нужны файлы больше стандартного лимита IIS, его надо увеличить отдельно.

### `UploadCreateUserId` / `UploadCreateUserName`

Технический пользователь для `rf_CreateUserID` и `CreateUserName`. Текущая версия сайта работает с Anonymous Authentication, поэтому реальная доменная учётная запись посетителя серверу неизвестна. IP клиента при этом записывается в аудит.

## Безопасность

- браузер не получает UNC-путь;
- `File.ashx` принимает идентификатор файла и повторно проверяет его принадлежность `MKABID`;
- путь проходит allow-list `AllowedRoots`;
- путь хранилища из `x_UserSettings` после `PathMappings` обязан находиться внутри `AllowedRoots`;
- вид документа для загрузки выбирается только по существующему активному `FileTypeID`, а не принимается произвольной строкой от браузера;
- `atf_FileInfo` и `atf_FileAttachment` создаются через `MERGE` по тем же ключам, что и в `LocalFileToMkabAttachOperator`;
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

При сборке доступные license/notices копируются в `ViewerDist/licenses`.

## Механизм прикрепления

Реализация повторяет `LocalFileToMkabAttachOperator`: путь хранилища читается из `dbo.x_UserSettings`, `rf_DescTypeGuid` — из `dbo.x_DocTypeDef`, физическое имя генерируется как UUID4 + расширение, `atf_FileInfo` переиспользуется по пациенту/типу/дате, а `atf_FileAttachment` создаётся по `FileInfoID + Path` с заполнением `Num`. Веб-интерфейс намеренно не создаёт новые `atf_FileType`: пользователь выбирает существующий активный тип МИС.