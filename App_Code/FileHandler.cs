using System;
using System.Configuration;
using System.IO;
using System.Web;

namespace PatientFiles
{
    public sealed class FileHandler : IHttpHandler
    {
        public bool IsReusable { get { return false; } }

        public void ProcessRequest(HttpContext context)
        {
            try
            {
                int id;
                int mkabId;
                if (!Int32.TryParse(context.Request.QueryString["id"], out id) || id <= 0)
                    throw new HttpException(400, "Некорректный идентификатор файла.");
                if (!Int32.TryParse(context.Request.QueryString["mkabid"], out mkabId) || mkabId <= 0)
                    throw new HttpException(400, "Некорректный MKABID.");

                string mode = (context.Request.QueryString["mode"] ?? "preview").ToLowerInvariant();
                if (mode == "raw") mode = "preview";
                if (mode != "preview" && mode != "download")
                    throw new HttpException(400, "Некорректный режим.");

                // Важно: проверяем одновременно FileAttachmentID и MKABID,
                // чтобы нельзя было получить файл другого пациента простой подстановкой id.
                var rec = Repository.GetFile(id, mkabId);
                if (rec == null) throw new HttpException(404, "Запись файла у этого пациента не найдена.");

                string source = FileSecurity.ResolveAndValidate(rec.path);
                var fi = new FileInfo(source);

                if (mode == "preview")
                {
                    int maxMb;
                    if (!Int32.TryParse(ConfigurationManager.AppSettings["MaxPreviewSizeMb"], out maxMb)) maxMb = 100;
                    if (maxMb > 0 && fi.Length > (long)maxMb * 1024L * 1024L)
                        throw new HttpException(413, "Файл слишком большой для предпросмотра. Используйте «Скачать оригинал».");
                }

                AuditLog.View(context, rec, mode);
                Send(context, source, GetMime(Path.GetExtension(source)), mode == "preview", Path.GetFileName(source));
            }
            catch (HttpException ex)
            {
                Error(context, ex.GetHttpCode(), ex.Message);
            }
            catch (FileNotFoundException ex)
            {
                AuditLog.Error(context, ex);
                Error(context, 404, ex.Message);
            }
            catch (UnauthorizedAccessException ex)
            {
                AuditLog.Error(context, ex);
                Error(context, 403, ex.Message);
            }
            catch (Exception ex)
            {
                AuditLog.Error(context, ex);
                Error(context, 500, "Не удалось открыть документ. См. журнал модуля.");
            }
        }

        private static void Send(HttpContext context, string file, string mime, bool inline, string downloadName)
        {
            var fi = new FileInfo(file);
            context.Response.Clear();
            context.Response.BufferOutput = false;
            context.Response.ContentType = mime;
            context.Response.Cache.SetCacheability(HttpCacheability.NoCache);
            context.Response.Cache.SetNoStore();
            context.Response.AddHeader("X-Content-Type-Options", "nosniff");
            context.Response.AddHeader("Content-Length", fi.Length.ToString());

            string disposition = inline ? "inline" : "attachment";
            string safe = (downloadName ?? "file").Replace("\"", "'");
            context.Response.AddHeader("Content-Disposition", disposition + "; filename*=UTF-8''" + Uri.EscapeDataString(safe));

            // Открываем файл непосредственно по UNC от имени identity пула приложений.
            // Никаких перечислений каталога: только точный путь из БД.
            using (var fs = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 64 * 1024, FileOptions.SequentialScan))
            {
                fs.CopyTo(context.Response.OutputStream, 64 * 1024);
            }
            context.ApplicationInstance.CompleteRequest();
        }

        private static string GetMime(string ext)
        {
            switch ((ext ?? "").ToLowerInvariant())
            {
                case ".pdf": return "application/pdf";
                case ".jpg": case ".jpeg": return "image/jpeg";
                case ".png": return "image/png";
                case ".gif": return "image/gif";
                case ".webp": return "image/webp";
                case ".bmp": return "image/bmp";
                case ".svg": return "image/svg+xml";
                case ".avif": return "image/avif";
                case ".tif": case ".tiff": return "image/tiff";
                case ".doc": return "application/msword";
                case ".docx": return "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
                case ".docm": return "application/vnd.ms-word.document.macroEnabled.12";
                case ".rtf": return "application/rtf";
                case ".txt": return "text/plain; charset=utf-8";
                case ".xls": return "application/vnd.ms-excel";
                case ".xlsx": return "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
                case ".xlsm": return "application/vnd.ms-excel.sheet.macroEnabled.12";
                case ".csv": return "text/csv; charset=utf-8";
                case ".tsv": return "text/tab-separated-values; charset=utf-8";
                case ".ods": return "application/vnd.oasis.opendocument.spreadsheet";
                case ".ppt": return "application/vnd.ms-powerpoint";
                case ".pptx": return "application/vnd.openxmlformats-officedocument.presentationml.presentation";
                case ".pptm": return "application/vnd.ms-powerpoint.presentation.macroEnabled.12";
                case ".ppsx": return "application/vnd.openxmlformats-officedocument.presentationml.slideshow";
                case ".odt": return "application/vnd.oasis.opendocument.text";
                case ".odp": return "application/vnd.oasis.opendocument.presentation";
                case ".xps": return "application/oxps";
                default: return "application/octet-stream";
            }
        }

        private static void Error(HttpContext context, int status, string message)
        {
            context.Response.Clear();
            context.Response.StatusCode = status;
            context.Response.TrySkipIisCustomErrors = true;
            context.Response.ContentType = "text/plain; charset=utf-8";
            context.Response.Write(message);
        }
    }
}
