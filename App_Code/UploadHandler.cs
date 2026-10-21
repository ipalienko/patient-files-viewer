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
            string finalPath = null;
            bool dbCommitted = false;

            try
            {
                if (!String.Equals(context.Request.HttpMethod, "POST", StringComparison.OrdinalIgnoreCase))
                    throw new HttpException(405, "Разрешён только POST-запрос.");

                int mkabId;
                if (!Int32.TryParse(context.Request.Form["mkabid"], out mkabId) || mkabId <= 0)
                    throw new HttpException(400, "Некорректный MKABID.");

                int fileTypeId;
                if (!Int32.TryParse(context.Request.Form["fileTypeId"], out fileTypeId) || fileTypeId <= 0)
                    throw new HttpException(400, "Не выбран вид документа.");

                DateTime documentDate;
                string rawDate = (context.Request.Form["documentDate"] ?? "").Trim();
                if (String.IsNullOrWhiteSpace(rawDate))
                {
                    documentDate = DateTime.Today;
                }
                else if (!DateTime.TryParseExact(rawDate, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out documentDate))
                {
                    throw new HttpException(400, "Некорректная дата документа.");
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

                // В существующем файловом хранилище имена файлов имеют UUID-вид.
                // GUID самой записи atf_FileAttachment оставляем БД — там настроен штатный default newsequentialid().
                Guid storageFileGuid = Guid.NewGuid();
                finalPath = FileSecurity.BuildUploadPath(storageFileGuid, extension);
                tempPath = finalPath + ".uploading";

                Directory.CreateDirectory(Path.GetDirectoryName(finalPath));
                using (var input = posted.InputStream)
                using (var output = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 64 * 1024, FileOptions.SequentialScan))
                {
                    input.CopyTo(output, 64 * 1024);
                    output.Flush();
                }

                // Переименование внутри одной SMB-шары атомарнее, чем писать сразу в окончательное имя.
                File.Move(tempPath, finalPath);
                tempPath = null;

                int createUserId = ReadIntSetting("UploadCreateUserId", 1, 0, Int32.MaxValue);
                string createUserName = (ConfigurationManager.AppSettings["UploadCreateUserName"] ?? "Веб-модуль").Trim();
                if (createUserName.Length > 255) createUserName = createUserName.Substring(0, 255);

                UploadResultDto result = Repository.AttachFile(
                    mkabId,
                    fileTypeId,
                    documentDate,
                    description,
                    finalPath,
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
                CleanupUncommittedFile(tempPath, finalPath, dbCommitted);
                context.Response.StatusCode = ex.GetHttpCode();
                context.Response.TrySkipIisCustomErrors = true;
                context.Response.Write(js.Serialize(new { ok = false, error = ex.Message }));
            }
            catch (Exception ex)
            {
                CleanupUncommittedFile(tempPath, finalPath, dbCommitted);
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

        private static void CleanupUncommittedFile(string tempPath, string finalPath, bool dbCommitted)
        {
            if (dbCommitted) return;
            try { if (!String.IsNullOrEmpty(tempPath) && File.Exists(tempPath)) File.Delete(tempPath); } catch { }
            try { if (!String.IsNullOrEmpty(finalPath) && File.Exists(finalPath)) File.Delete(finalPath); } catch { }
        }
    }
}