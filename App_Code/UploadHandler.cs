using System;
using System.Configuration;
using System.Globalization;
using System.IO;
using System.Web;
using System.Web.Script.Serialization;

namespace PatientFiles
{
    public sealed class UploadHandler : IHttpHandler
    {
        public bool IsReusable { get { return false; } }

        public void ProcessRequest(HttpContext context)
        {
            context.Response.ContentType = "application/json; charset=utf-8";
            context.Response.Cache.SetCacheability(HttpCacheability.NoCache);
            context.Response.Cache.SetNoStore();
            var js = new JavaScriptSerializer { MaxJsonLength = 1024 * 1024 * 4 };

            string tempPath = null;
            string physicalFinalPath = null;
            bool dbCommitted = false;

            try
            {
                if (!String.Equals(context.Request.HttpMethod, "POST", StringComparison.OrdinalIgnoreCase))
                    throw new HttpException(405, "Разрешён только POST-запрос.");

                if (!ReadBoolSetting("UploadEnabled", false))
                    throw new HttpException(403, "Прикрепление файлов отключено в конфигурации модуля.");

                int mkabId;
                if (!Int32.TryParse(context.Request.Form["mkabid"], out mkabId) || mkabId <= 0)
                    throw new HttpException(400, "Некорректный MKABID.");

                int fileTypeId;
                if (!Int32.TryParse(context.Request.Form["fileTypeId"], out fileTypeId) || fileTypeId <= 0)
                    throw new HttpException(400, "Не выбран вид документа.");

                // В оригинальном операторе один created_at используется одновременно для
                // atf_FileInfo.DateDoc и atf_FileAttachment.DateCreate.
                DateTime createdAt;
                string rawDate = (context.Request.Form["documentDate"] ?? "").Trim();
                if (String.IsNullOrWhiteSpace(rawDate))
                {
                    createdAt = DateTime.Now;
                }
                else
                {
                    DateTime selectedDate;
                    if (!DateTime.TryParseExact(rawDate, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                        DateTimeStyles.None, out selectedDate))
                        throw new HttpException(400, "Некорректная дата документа.");

                    createdAt = selectedDate.Date.Add(DateTime.Now.TimeOfDay);
                }

                string description = (context.Request.Form["description"] ?? "").Trim();
                if (description.Length > 2000)
                    throw new HttpException(400, "Описание слишком длинное.");

                HttpPostedFile posted = context.Request.Files["file"];
                if (posted == null || posted.ContentLength <= 0)
                    throw new HttpException(400, "Файл не выбран.");

                int maxUploadMb = ReadIntSetting("MaxUploadSizeMb", 30, 1, 1024);
                long maxBytes = (long)maxUploadMb * 1024L * 1024L;
                if (posted.ContentLength > maxBytes)
                    throw new HttpException(413, "Размер файла превышает допустимые " + maxUploadMb + " МБ.");

                string originalName = Path.GetFileName(posted.FileName ?? "");
                string extension;
                try
                {
                    extension = FileSecurity.ValidateUploadExtension(originalName);
                }
                catch (InvalidOperationException ex)
                {
                    throw new HttpException(400, ex.Message);
                }

                // Получаем ровно те параметры, которые использует LocalFileToMkabAttachOperator:
                // UGUID пациента, rf_DescTypeGuid и базовый путь из x_UserSettings.
                UploadPreparationDto prep = Repository.PrepareUpload(mkabId, fileTypeId);

                // Оригинальный оператор: uuid4 + исходное расширение.
                Guid storageFileGuid = Guid.NewGuid();
                string dbPath = FileSecurity.BuildStorageDbPath(prep.storageRoot, storageFileGuid, extension);
                physicalFinalPath = FileSecurity.ResolveUploadTarget(dbPath);
                tempPath = physicalFinalPath + ".uploading";

                string directory = Path.GetDirectoryName(physicalFinalPath);
                if (!String.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);

                using (var input = posted.InputStream)
                using (var output = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                    64 * 1024, FileOptions.SequentialScan))
                {
                    input.CopyTo(output, 64 * 1024);
                    output.Flush();
                }

                // В отличие от Airflow-оператора сначала пишем во временное имя и затем атомарно
                // переименовываем на той же шаре. В БД попадает только окончательное UUID-имя.
                File.Move(tempPath, physicalFinalPath);
                tempPath = null;

                int createUserId = ReadIntSetting("UploadCreateUserId", 1, 0, Int32.MaxValue);
                string createUserName = (ConfigurationManager.AppSettings["UploadCreateUserName"] ?? "Администратор").Trim();
                if (createUserName.Length > 255) createUserName = createUserName.Substring(0, 255);

                UploadResultDto result = Repository.AttachFile(
                    prep,
                    createdAt,
                    description,
                    dbPath,
                    createUserId,
                    createUserName);

                dbCommitted = true;
                AuditLog.Upload(context, mkabId, result.attachmentId, result.fileInfoId,
                    result.fileType, extension, posted.ContentLength);

                context.Response.Write(js.Serialize(new
                {
                    ok = true,
                    attachmentId = result.attachmentId,
                    fileInfoId = result.fileInfoId,
                    fileType = result.fileType,
                    format = extension.TrimStart('.').ToUpperInvariant()
                }));
            }
            catch (HttpException ex)
            {
                CleanupUncommittedFile(tempPath, physicalFinalPath, dbCommitted);
                context.Response.StatusCode = ex.GetHttpCode();
                context.Response.TrySkipIisCustomErrors = true;
                context.Response.Write(js.Serialize(new { ok = false, error = ex.Message }));
            }
            catch (Exception ex)
            {
                CleanupUncommittedFile(tempPath, physicalFinalPath, dbCommitted);
                AuditLog.Error(context, ex);
                context.Response.StatusCode = 500;
                context.Response.TrySkipIisCustomErrors = true;
                context.Response.Write(js.Serialize(new
                {
                    ok = false,
                    error = "Не удалось прикрепить файл. Изменения отменены, подробности записаны в журнал."
                }));
            }
        }

        private static int ReadIntSetting(string key, int fallback, int min, int max)
        {
            int value;
            if (!Int32.TryParse(ConfigurationManager.AppSettings[key], out value)) value = fallback;
            if (value < min) value = min;
            if (value > max) value = max;
            return value;
        }

        private static bool ReadBoolSetting(string key, bool fallback)
        {
            bool value;
            return Boolean.TryParse(ConfigurationManager.AppSettings[key], out value) ? value : fallback;
        }

        private static void CleanupUncommittedFile(string tempPath, string finalPath, bool dbCommitted)
        {
            if (dbCommitted) return;
            try { if (!String.IsNullOrEmpty(tempPath) && File.Exists(tempPath)) File.Delete(tempPath); } catch { }
            try { if (!String.IsNullOrEmpty(finalPath) && File.Exists(finalPath)) File.Delete(finalPath); } catch { }
        }
    }
}