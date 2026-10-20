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
    }
}
