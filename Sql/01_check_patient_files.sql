DECLARE @MKABID int = 123456;

SELECT
    m.MKABID,
    m.UGUID AS MKABGuid,
    aFA.FileAttachmentID,
    aFI.FileInfoID,
    aFI.DateDoc,
    aFA.DateCreate,
    aFT.Name AS FileTypeName,
    aFT.Code AS FileTypeCode,
    aFI.Description AS FileInfoDescription,
    aFA.Description AS AttachmentDescription,
    aFA.CreateUserName,
    aFI.DoctorName,
    aFA.Path,
    aFI.x_Status AS FileInfoStatus,
    aFA.x_Status AS AttachmentStatus
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
ORDER BY COALESCE(aFI.DateDoc, aFA.DateCreate) DESC, aFA.FileAttachmentID DESC;
