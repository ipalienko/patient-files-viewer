using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;

namespace PatientFiles
{
    internal static class FileSecurity
    {
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

        // LocalFileToMkabAttachOperator кладёт файл в корень Samba под именем UUID.ext,
        // а в БД сохраняет <x_UserSettings.ValueStr>\UUID.ext.
        public static string BuildStorageDbPath(string storageRoot, Guid fileGuid, string extension)
        {
            if (fileGuid == Guid.Empty)
                throw new ArgumentException("Не задан GUID файла.", "fileGuid");

            extension = (extension ?? "").ToLowerInvariant();
            if (!AllowedExtensions.Contains(extension))
                throw new InvalidOperationException("Формат файла не разрешён для прикрепления: " + extension);

            if (String.IsNullOrWhiteSpace(storageRoot))
                throw new InvalidOperationException("В МИС не настроен путь к хранилищу прикреплённых файлов.");

            string fileName = fileGuid.ToString("D").ToLowerInvariant() + extension;
            return storageRoot.Trim().Trim('"').TrimEnd('\\') + "\\" + fileName;
        }

        // Превращает путь, который будет записан в БД, в физический путь для IIS.
        // Если в окружении путь отличается, используется PathMappings.
        public static string ResolveUploadTarget(string dbPath)
        {
            if (String.IsNullOrWhiteSpace(dbPath))
                throw new InvalidOperationException("Не задан путь к создаваемому файлу.");

            string path = Environment.ExpandEnvironmentVariables(dbPath.Trim().Trim('"'));
            path = ApplyMappings(path);

            if (!Path.IsPathRooted(path))
            {
                string root = ConfigurationManager.AppSettings["FileRoot"] ?? "";
                if (String.IsNullOrWhiteSpace(root))
                    throw new InvalidOperationException("Путь хранилища МИС относительный, но FileRoot не настроен.");
                path = Path.Combine(root, path);
            }

            path = Path.GetFullPath(path);
            string ext = Path.GetExtension(path);
            if (!AllowedExtensions.Contains(ext))
                throw new InvalidOperationException("Формат файла не разрешён для прикрепления: " + ext);

            EnsureAllowedRoot(path);
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
                string root = ConfigurationManager.AppSettings["FileRoot"] ?? "";
                if (String.IsNullOrWhiteSpace(root))
                    throw new InvalidOperationException("В БД хранится относительный путь, но FileRoot не настроен.");
                path = Path.Combine(root, path);
            }

            path = Path.GetFullPath(path);
            string ext = Path.GetExtension(path);
            if (!AllowedExtensions.Contains(ext))
                throw new InvalidOperationException("Формат файла не разрешён для выдачи: " + ext);

            EnsureAllowedRoot(path);

            if (!File.Exists(path))
                throw new FileNotFoundException("Файл отсутствует на файловом сервере.", path);

            return path;
        }

        private static void EnsureAllowedRoot(string path)
        {
            string[] roots = GetAllowedRoots();
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

        // Пример: X:\=>\\FILE_SERVER\SHARE_ROOT|Y:\old\=>\\FILE_SERVER\ARCHIVE_ROOT\
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