using System;
using System.Web;
using System.Web.Script.Serialization;

namespace PatientFiles
{
    public sealed class ApiHandler : IHttpHandler
    {
        public bool IsReusable { get { return false; } }
        public void ProcessRequest(HttpContext context)
        {
            context.Response.ContentType = "application/json; charset=utf-8";
            context.Response.Cache.SetCacheability(HttpCacheability.NoCache);
            context.Response.Cache.SetNoStore();
            var js = new JavaScriptSerializer { MaxJsonLength = 1024 * 1024 * 20 };
            try
            {
                if (!String.Equals(context.Request.QueryString["action"], "list", StringComparison.OrdinalIgnoreCase))
                    throw new HttpException(400, "Неизвестное действие.");
                int mkabId;
                if (!Int32.TryParse(context.Request.QueryString["mkabid"], out mkabId) || mkabId <= 0)
                    throw new HttpException(400, "Некорректный MKABID.");
                var patient = Repository.GetPatient(mkabId);
                if (patient == null) throw new HttpException(404, "Пациент не найден.");
                var files = Repository.GetFiles(mkabId);
                AuditLog.List(context, mkabId, files.Count);
                context.Response.Write(js.Serialize(new { ok = true, patient = patient, files = files }));
            }
            catch (HttpException ex)
            {
                context.Response.StatusCode = ex.GetHttpCode();
                context.Response.TrySkipIisCustomErrors = true;
                context.Response.Write(js.Serialize(new { ok = false, error = ex.Message }));
            }
            catch (Exception ex)
            {
                AuditLog.Error(context, ex);
                context.Response.StatusCode = 500;
                context.Response.TrySkipIisCustomErrors = true;
                context.Response.Write(js.Serialize(new { ok = false, error = "Внутренняя ошибка модуля. Подробности записаны в журнал." }));
            }
        }
    }
}
