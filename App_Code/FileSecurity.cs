using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;

namespace PatientFiles
{
    internal static class FileSecurity
    {
        // Только документы, которые разрешено отдавать из МИС пользователю.
        // Неисполняемые форматы. XPS разрешён для скачивания, но текущий viewer его не показывает.
        private static readonly HashSet<string> AllowedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".pdf",
            ".jpg", ".jpeg", ".png", ".gif", ".webp", ".bmp", ".svg", ".avif", ".tif", ".tiff",
            ".doc", ".docx", ".docm", ".rtf", ".txt",
            ".xls", ".xlsx", ".xlsm", ".csv", ".tsv", ".ods",
            ".ppt", ".pptx", ".pptm", ".ppsx", ".odp",
            ".odt", ".xps"
        };

        public static string ValidateUploadExtension(string fileName)
        {
            if (String.IsNullOrWhiteSpace(fileName))
                throw new InvalidOperationException("Не удалось определить имя файла.");

            string ext = Path.GetExtension(fileName).ToLowerInvariant();
            if (String.IsNullOrWhiteSpace(ext) || !AllowedExtensions.Contains(ext))
                throw new InvalidOperationException("Формат файла не разрешён для прикрепления: " + (ext == "" ? "без расширения" : ext));

            return ext;
        }

        public static string BuildUploadPath(Guid attachmentGuid, string extension)
        {
            if (attachmentGuid == Guid.Empty)
                throw new ArgumentException("Не задан GUID файла.", "attachmentGuid");

            extension = (extension ?? "").ToLowerInvariant();
            if (!AllowedExtensions.Contains(extension))
                throw new InvalidOperationException("Формат файла не разрешён для прикрепления: " + extension);

            string configured = ConfigurationManager.AppSettings["UploadRoot"] ?? "";
            if (String.IsNullOrWhiteSpace(configured))
                throw new InvalidOperationException("UploadRoot не настроен.");

            string uploadRoot = NormalizeRoot(configured);
            if (String.IsNullOrWhiteSpace(uploadRoot))
                throw new InvalidOperationException("UploadRoot должен быть абсолютным локальным или UNC-путём.");

            var roots = GetAllowedRoots();
            if (roots.Length == 0)
                throw new InvalidOperationException("AllowedRoots не настроен.");

            bool allowed = roots.Any(r =>
            {
                string bare = r.TrimEnd('\\');
                string uploadBare = uploadRoot.TrimEnd('\\');
                return uploadBare.Equals(bare, StringComparison.OrdinalIgnoreCase) ||
                       uploadRoot.StartsWith(r, StringComparison.OrdinalIgnoreCase);
            });

            if (!allowed)
                throw new UnauthorizedAccessException("UploadRoot находится вне AllowedRoots.");

            string fileName = attachmentGuid.ToString("D").ToLowerInvariant() + extension;
            string path = Path.GetFullPath(Path.Combine(uploadRoot, fileName));

            // Повторная проверка после Path.Combine/GetFullPath.
            if (!path.StartsWith(uploadRoot, StringComparison.OrdinalIgnoreCase))
                throw new UnauthorizedAccessException("Некорректный путь для загрузки файла.");

            return path;
        }

        public static string ResolveAndValidate(string dbPath)
        {
            if (String.IsNullOrWhiteSpace(dbPath))
                throw new FileNotFoundException("В БД не указан путь к файлу.");

            string path = Environment.ExpandEnvironmentVariables(dbPath.Trim().Trim('"'));
            path = ApplyMappings(path);

            if (!Path.IsPathRooted(path))
            {
                var root = ConfigurationManager.AppSettings["FileRoot"] ?? "";
                if (String.IsNullOrWhiteSpace(root))
                    throw new InvalidOperationException("В БД хранится относительный путь, но FileRoot не настроен.");
                path = Path.Combine(root, path);
            }

            path = Path.GetFullPath(path);
            var ext = Path.GetExtension(path);
            if (!AllowedExtensions.Contains(ext))
                throw new InvalidOperationException("Формат файла не разрешён для выдачи: " + ext);

            var roots = GetAllowedRoots();

            if (roots.Length == 0)
                throw new InvalidOperationException("AllowedRoots не настроен.");

            bool allowed = roots.Any(r =>
            {
                string bare = r.TrimEnd('\\');
                return path.Equals(bare, StringComparison.OrdinalIgnoreCase) ||
                       path.StartsWith(r, StringComparison.OrdinalIgnoreCase);
            });

            if (!allowed)
                throw new UnauthorizedAccessException("Путь файла находится вне разрешённых каталогов.");

            if (!File.Exists(path))
                throw new FileNotFoundException("Файл отсутствует на файловом сервере.", path);

            return path;
        }

        private static string[] GetAllowedRoots()
        {
            return (ConfigurationManager.AppSettings["AllowedRoots"] ?? "")
                .Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(NormalizeRoot)
                .Where(x => !String.IsNullOrWhiteSpace(x))
                .ToArray();
        }

        private static string NormalizeRoot(string value)
        {
            if (String.IsNullOrWhiteSpace(value)) return "";
            string root = Environment.ExpandEnvironmentVariables(value.Trim().Trim('"'));
            root = ApplyMappings(root);
            if (!Path.IsPathRooted(root)) return "";
            root = Path.GetFullPath(root).TrimEnd('\\');
            return root + "\\";
        }

        // Пример в web.config:
        // X:\=>\\FILE_SERVER\SHARE_ROOT|Y:\old\=>\\FILE_SERVER\ARCHIVE_ROOT\
        private static string ApplyMappings(string path)
        {
            string raw = ConfigurationManager.AppSettings["PathMappings"] ?? "";
            foreach (string item in raw.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries))
            {
                int p = item.IndexOf("=>", StringComparison.Ordinal);
                if (p <= 0) continue;
                string from = item.Substring(0, p).Trim().Trim('"');
                string to = item.Substring(p + 2).Trim().Trim('"');
                if (String.IsNullOrEmpty(from) || String.IsNullOrEmpty(to)) continue;
                if (!path.StartsWith(from, StringComparison.OrdinalIgnoreCase)) continue;
                return to.TrimEnd('\\') + "\\" + path.Substring(from.Length).TrimStart('\\');
            }
            return path;
        }
    }
}