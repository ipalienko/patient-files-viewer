using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.IO;

namespace PatientFiles
{
    internal static class Repository
    {
        public static PatientDto GetPatient(int mkabId)
        {
            const string sql = @"
SELECT TOP (1)
       m.MKABID,
       LTRIM(RTRIM(ISNULL(m.FAMILY,'') + ' ' + ISNULL(m.NAME,'') + ' ' + ISNULL(m.OT,''))) AS FIO,
       m.DATE_BD
FROM dbo.hlt_MKAB m
WHERE m.MKABID = @MKABID;";
            using (var cn = Db.Open())
            using (var cmd = new SqlCommand(sql, cn))
            {
                cmd.Parameters.Add("@MKABID", SqlDbType.Int).Value = mkabId;
                using (var rd = cmd.ExecuteReader())
                {
                    if (!rd.Read()) return null;
                    return new PatientDto
                    {
                        mkabid = rd.GetInt32(0),
                        fio = rd.IsDBNull(1) ? "" : rd.GetString(1),
                        birthDate = rd.IsDBNull(2) ? (DateTime?)null : rd.GetDateTime(2)
                    };
                }
            }
        }

        public static List<FileDto> GetFiles(int mkabId)
        {
            const string sql = @"
SELECT
    aFA.FileAttachmentID,
    aFI.FileInfoID,
    aFI.DateDoc,
    aFA.DateCreate,
    aFT.Name AS FileTypeName,
    aFT.Code AS FileTypeCode,
    NULLIF(LTRIM(RTRIM(COALESCE(NULLIF(aFA.Description,''), NULLIF(aFI.Description,''), ''))), '') AS Description,
    NULLIF(LTRIM(RTRIM(aFA.CreateUserName)), '') AS CreateUserName,
    NULLIF(LTRIM(RTRIM(aFI.DoctorName)), '') AS DoctorName,
    aFA.Path
FROM dbo.hlt_MKAB m
JOIN dbo.atf_FileInfo aFI
  ON aFI.DescGuid = m.UGUID
JOIN dbo.atf_FileAttachment aFA
  ON aFA.rf_FileInfoID = aFI.FileInfoID
LEFT JOIN dbo.atf_FileType aFT
  ON aFT.FileTypeID = aFI.rf_FileTypeID
WHERE m.MKABID = @MKABID
  AND NULLIF(LTRIM(RTRIM(aFA.Path)), '') IS NOT NULL
  AND ISNULL(aFI.x_Status, 1) = 1
  AND ISNULL(aFA.x_Status, 1) = 1
ORDER BY COALESCE(aFI.DateDoc, aFA.DateCreate) DESC, aFA.FileAttachmentID DESC;";
            var result = new List<FileDto>();
            using (var cn = Db.Open())
            using (var cmd = new SqlCommand(sql, cn))
            {
                cmd.Parameters.Add("@MKABID", SqlDbType.Int).Value = mkabId;
                using (var rd = cmd.ExecuteReader())
                {
                    while (rd.Read())
                    {
                        var path = rd.IsDBNull(9) ? "" : rd.GetString(9);
                        result.Add(new FileDto
                        {
                            attachmentId = rd.GetInt32(0),
                            fileInfoId = rd.GetInt32(1),
                            documentDate = rd.IsDBNull(2) ? (DateTime?)null : rd.GetDateTime(2),
                            createdAt = rd.IsDBNull(3) ? (DateTime?)null : rd.GetDateTime(3),
                            fileType = rd.IsDBNull(4) ? "" : rd.GetString(4),
                            fileTypeCode = rd.IsDBNull(5) ? "" : rd.GetString(5),
                            description = rd.IsDBNull(6) ? "" : rd.GetString(6),
                            createUserName = rd.IsDBNull(7) ? "" : rd.GetString(7),
                            doctorName = rd.IsDBNull(8) ? "" : rd.GetString(8),
                            fileName = Path.GetFileName(path),
                            extension = Path.GetExtension(path).ToLowerInvariant()
                        });
                    }
                }
            }
            return result;
        }

        public static List<FileTypeDto> GetFileTypes()
        {
            const string sql = @"
SELECT
    ft.FileTypeID,
    ft.Code,
    ft.Name
FROM dbo.atf_FileType ft
WHERE ISNULL(ft.x_Status, 1) = 1
  AND ISNULL(ft.DateBegin, CONVERT(datetime, '19000101', 112)) <= GETDATE()
  AND ISNULL(ft.DateEnd, CONVERT(datetime, '22220101', 112)) >= CONVERT(date, GETDATE())
ORDER BY ft.Name, ft.Code, ft.FileTypeID;";

            var result = new List<FileTypeDto>();
            using (var cn = Db.Open())
            using (var cmd = new SqlCommand(sql, cn))
            using (var rd = cmd.ExecuteReader())
            {
                while (rd.Read())
                {
                    result.Add(new FileTypeDto
                    {
                        fileTypeId = rd.GetInt32(0),
                        code = rd.IsDBNull(1) ? "" : rd.GetString(1),
                        name = rd.IsDBNull(2) ? "" : rd.GetString(2)
                    });
                }
            }
            return result;
        }

        public static FileRecord GetFile(int attachmentId, int mkabId)
        {
            const string sql = @"
SELECT TOP (1)
    m.MKABID,
    aFA.FileAttachmentID,
    aFI.FileInfoID,
    aFI.DateDoc,
    aFA.DateCreate,
    aFT.Name,
    aFT.Code,
    NULLIF(LTRIM(RTRIM(COALESCE(NULLIF(aFA.Description,''), NULLIF(aFI.Description,''), ''))), ''),
    NULLIF(LTRIM(RTRIM(aFA.CreateUserName)), ''),
    NULLIF(LTRIM(RTRIM(aFI.DoctorName)), ''),
    aFA.Path
FROM dbo.atf_FileAttachment aFA
JOIN dbo.atf_FileInfo aFI ON aFI.FileInfoID = aFA.rf_FileInfoID
JOIN dbo.hlt_MKAB m ON m.UGUID = aFI.DescGuid
LEFT JOIN dbo.atf_FileType aFT ON aFT.FileTypeID = aFI.rf_FileTypeID
WHERE aFA.FileAttachmentID = @ID
  AND m.MKABID = @MKABID
  AND ISNULL(aFI.x_Status, 1) = 1
  AND ISNULL(aFA.x_Status, 1) = 1;";
            using (var cn = Db.Open())
            using (var cmd = new SqlCommand(sql, cn))
            {
                cmd.Parameters.Add("@ID", SqlDbType.Int).Value = attachmentId;
                cmd.Parameters.Add("@MKABID", SqlDbType.Int).Value = mkabId;
                using (var rd = cmd.ExecuteReader())
                {
                    if (!rd.Read()) return null;
                    var path = rd.IsDBNull(10) ? "" : rd.GetString(10);
                    return new FileRecord
                    {
                        mkabid = rd.GetInt32(0),
                        attachmentId = rd.GetInt32(1),
                        fileInfoId = rd.GetInt32(2),
                        documentDate = rd.IsDBNull(3) ? (DateTime?)null : rd.GetDateTime(3),
                        createdAt = rd.IsDBNull(4) ? (DateTime?)null : rd.GetDateTime(4),
                        fileType = rd.IsDBNull(5) ? "" : rd.GetString(5),
                        fileTypeCode = rd.IsDBNull(6) ? "" : rd.GetString(6),
                        description = rd.IsDBNull(7) ? "" : rd.GetString(7),
                        createUserName = rd.IsDBNull(8) ? "" : rd.GetString(8),
                        doctorName = rd.IsDBNull(9) ? "" : rd.GetString(9),
                        path = path,
                        fileName = Path.GetFileName(path),
                        extension = Path.GetExtension(path).ToLowerInvariant()
                    };
                }
            }
        }

        // Повторяет параметры, которые LocalFileToMkabAttachOperator получает перед созданием записей:
        // UGUID МКАБ, rf_DescTypeGuid для hlt_MKAB и путь хранилища из x_UserSettings.
        public static UploadPreparationDto PrepareUpload(int mkabId, int fileTypeId)
        {
            using (var cn = Db.OpenWrite())
            {
                Guid mkabGuid;
                using (var cmd = new SqlCommand(@"
SELECT TOP (1) m.UGUID
FROM dbo.hlt_MKAB m
WHERE m.MKABID = @MKABID;", cn))
                {
                    cmd.Parameters.Add("@MKABID", SqlDbType.Int).Value = mkabId;
                    object value = cmd.ExecuteScalar();
                    if (value == null || value == DBNull.Value)
                        throw new InvalidOperationException("Пациент с указанным MKABID не найден.");
                    mkabGuid = (Guid)value;
                }

                string fileTypeName;
                string fileTypeCode;
                using (var cmd = new SqlCommand(@"
SELECT TOP (1) ft.Name, ft.Code
FROM dbo.atf_FileType ft
WHERE ft.FileTypeID = @FileTypeID
  AND ISNULL(ft.x_Status, 1) = 1
  AND ISNULL(ft.DateBegin, CONVERT(datetime, '19000101', 112)) <= GETDATE()
  AND ISNULL(ft.DateEnd, CONVERT(datetime, '22220101', 112)) >= CONVERT(date, GETDATE());", cn))
                {
                    cmd.Parameters.Add("@FileTypeID", SqlDbType.Int).Value = fileTypeId;
                    using (var rd = cmd.ExecuteReader())
                    {
                        if (!rd.Read())
                            throw new InvalidOperationException("Выбранный вид документа не найден или неактивен.");
                        fileTypeName = rd.IsDBNull(0) ? "" : rd.GetString(0);
                        fileTypeCode = rd.IsDBNull(1) ? "" : rd.GetString(1);
                    }
                }

                Guid descTypeGuid;
                using (var cmd = new SqlCommand(@"
SELECT TOP (1) Guid
FROM dbo.x_DocTypeDef
WHERE HeadTable = 'hlt_MKAB'
ORDER BY Guid ASC;", cn))
                {
                    object value = cmd.ExecuteScalar();
                    if (value == null || value == DBNull.Value)
                        throw new InvalidOperationException("В x_DocTypeDef не найден тип документа для hlt_MKAB.");
                    descTypeGuid = (Guid)value;
                }

                string storageRoot;
                using (var cmd = new SqlCommand(@"
SELECT TOP (1) ValueStr
FROM dbo.x_UserSettings
WHERE Property = N'Путь к хранилищу прикреплённых файлов';", cn))
                {
                    object value = cmd.ExecuteScalar();
                    storageRoot = value == null || value == DBNull.Value ? "" : Convert.ToString(value);
                    if (String.IsNullOrWhiteSpace(storageRoot))
                        throw new InvalidOperationException("В x_UserSettings не настроен путь к хранилищу прикреплённых файлов.");
                }

                return new UploadPreparationDto
                {
                    mkabId = mkabId,
                    mkabGuid = mkabGuid,
                    fileTypeId = fileTypeId,
                    fileTypeCode = fileTypeCode,
                    fileTypeName = fileTypeName,
                    descTypeGuid = descTypeGuid,
                    storageRoot = storageRoot
                };
            }
        }

        // Логика ниже повторяет LocalFileToMkabAttachOperator:
        // 1) FileInfo MERGE по типу + rf_DescTypeGuid + МКАБ + календарной дате;
        // 2) Attachment MERGE по FileInfoID + Path;
        // 3) Num = количество уже существующих вложений у этого FileInfo.
        public static UploadResultDto AttachFile(
            UploadPreparationDto prep,
            DateTime createdAt,
            string description,
            string dbPath,
            int createUserId,
            string createUserName)
        {
            if (prep == null) throw new ArgumentNullException("prep");
            if (String.IsNullOrWhiteSpace(dbPath))
                throw new ArgumentException("Не задан путь к файлу.", "dbPath");
            if (dbPath.Length > 500)
                throw new InvalidOperationException("Путь к файлу длиннее 500 символов и не помещается в atf_FileAttachment.Path.");

            description = description ?? "";
            createUserName = createUserName ?? "";

            using (var cn = Db.OpenWrite())
            using (var tx = cn.BeginTransaction(IsolationLevel.ReadCommitted))
            {
                try
                {
                    int fileInfoId;
                    using (var cmd = new SqlCommand(@"
MERGE dbo.atf_FileInfo WITH (HOLDLOCK) AS target
USING
(
    SELECT @FileTypeID AS rf_FileTypeID,
           @DescTypeGuid AS rf_DescTypeGuid,
           @MkabGuid AS DescGuid,
           @CreatedAt AS DateDoc
) AS source
ON target.rf_FileTypeID = source.rf_FileTypeID
   AND target.rf_DescTypeGuid = source.rf_DescTypeGuid
   AND target.DescGuid = source.DescGuid
   AND CAST(target.DateDoc AS date) = CAST(source.DateDoc AS date)
WHEN NOT MATCHED THEN
    INSERT (rf_FileTypeID, rf_DescTypeGuid, DescGuid, DateDoc)
    VALUES (source.rf_FileTypeID, source.rf_DescTypeGuid, source.DescGuid, source.DateDoc);

SELECT TOP (1) FileInfoID
FROM dbo.atf_FileInfo
WHERE rf_FileTypeID = @FileTypeID
  AND rf_DescTypeGuid = @DescTypeGuid
  AND DescGuid = @MkabGuid
  AND CAST(DateDoc AS date) = CAST(@CreatedAt AS date)
ORDER BY FileInfoID;", cn, tx))
                    {
                        cmd.Parameters.Add("@FileTypeID", SqlDbType.Int).Value = prep.fileTypeId;
                        cmd.Parameters.Add("@DescTypeGuid", SqlDbType.UniqueIdentifier).Value = prep.descTypeGuid;
                        cmd.Parameters.Add("@MkabGuid", SqlDbType.UniqueIdentifier).Value = prep.mkabGuid;
                        cmd.Parameters.Add("@CreatedAt", SqlDbType.DateTime).Value = createdAt;
                        object value = cmd.ExecuteScalar();
                        if (value == null || value == DBNull.Value)
                            throw new InvalidOperationException("Не удалось получить FileInfoID после MERGE atf_FileInfo.");
                        fileInfoId = Convert.ToInt32(value);
                    }

                    int attachmentId;
                    using (var cmd = new SqlCommand(@"
MERGE dbo.atf_FileAttachment WITH (HOLDLOCK) AS target
USING
(
    SELECT @FileInfoID AS rf_FileInfoID,
           @Path AS Path,
           @CreateUserID AS rf_CreateUserID,
           @CreateUserName AS CreateUserName,
           @Description AS Description,
           @CreatedAt AS DateCreate
) AS source
ON target.rf_FileInfoID = source.rf_FileInfoID
   AND target.Path = source.Path
WHEN NOT MATCHED THEN
    INSERT (rf_CreateUserID, CreateUserName, rf_FileInfoID, Path, Description, DateCreate, Num)
    VALUES
    (
        source.rf_CreateUserID,
        source.CreateUserName,
        source.rf_FileInfoID,
        source.Path,
        source.Description,
        source.DateCreate,
        (SELECT COUNT(*) FROM dbo.atf_FileAttachment A2 WHERE A2.rf_FileInfoID = source.rf_FileInfoID)
    );

SELECT TOP (1) FileAttachmentID
FROM dbo.atf_FileAttachment
WHERE rf_FileInfoID = @FileInfoID
  AND Path = @Path
ORDER BY FileAttachmentID;", cn, tx))
                    {
                        cmd.Parameters.Add("@FileInfoID", SqlDbType.Int).Value = fileInfoId;
                        cmd.Parameters.Add("@Path", SqlDbType.VarChar, 500).Value = dbPath;
                        cmd.Parameters.Add("@CreateUserID", SqlDbType.Int).Value = createUserId;
                        cmd.Parameters.Add("@CreateUserName", SqlDbType.VarChar, 255).Value = createUserName;
                        cmd.Parameters.Add("@Description", SqlDbType.VarChar, -1).Value = description;
                        cmd.Parameters.Add("@CreatedAt", SqlDbType.DateTime).Value = createdAt;
                        object value = cmd.ExecuteScalar();
                        if (value == null || value == DBNull.Value)
                            throw new InvalidOperationException("Не удалось получить FileAttachmentID после MERGE atf_FileAttachment.");
                        attachmentId = Convert.ToInt32(value);
                    }

                    tx.Commit();
                    return new UploadResultDto
                    {
                        attachmentId = attachmentId,
                        fileInfoId = fileInfoId,
                        fileType = prep.fileTypeName,
                        fileTypeCode = prep.fileTypeCode
                    };
                }
                catch
                {
                    try { tx.Rollback(); } catch { }
                    throw;
                }
            }
        }
    }
}