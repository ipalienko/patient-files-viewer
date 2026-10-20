using System;
using System.IO;
using System.Text;
using System.Web;

namespace PatientFiles
{
    internal static class AuditLog
    {
        private static readonly object Sync = new object();
        public static void List(HttpContext ctx, int mkabId, int count)
        {
            Write("LIST", ctx, "mkabid=" + mkabId + " count=" + count);
        }
        public static void View(HttpContext ctx, FileRecord f, string mode)
        {
            Write("VIEW", ctx, "mode=" + mode + " mkabid=" + f.mkabid + " attachment=" + f.attachmentId + " fileInfo=" + f.fileInfoId);
        }
        public static void Error(HttpContext ctx, Exception ex)
        {
            Write("ERROR", ctx, ex.GetType().Name + ": " + ex.Message);
        }
        private static void Write(string kind, HttpContext ctx, string text)
        {
            try
            {
                string dir = ctx.Server.MapPath("~/App_Data/Logs"); Directory.CreateDirectory(dir);
                string file = Path.Combine(dir, "audit-" + DateTime.Now.ToString("yyyyMMdd") + ".log");
                string user = (ctx.User != null && ctx.User.Identity != null) ? ctx.User.Identity.Name : "";
                string line = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "\t" + kind + "\t" + user + "\t" + text + Environment.NewLine;
                lock (Sync) File.AppendAllText(file, line, Encoding.UTF8);
            } catch { }
        }
    }
}
