SELECT TOP (50)
    m.MKABID,
    LTRIM(RTRIM(ISNULL(m.FAMILY,'') + ' ' + ISNULL(m.NAME,'') + ' ' + ISNULL(m.OT,''))) AS FIO,
    COUNT(*) AS FileCount,
    MAX(COALESCE(aFI.DateDoc, aFA.DateCreate)) AS LastFileDate
FROM dbo.hlt_MKAB m
JOIN dbo.atf_FileInfo aFI ON aFI.DescGuid = m.UGUID
JOIN dbo.atf_FileAttachment aFA ON aFA.rf_FileInfoID = aFI.FileInfoID
WHERE NULLIF(LTRIM(RTRIM(aFA.Path)), '') IS NOT NULL
  AND ISNULL(aFI.x_Status, 1) = 1
  AND ISNULL(aFA.x_Status, 1) = 1
GROUP BY m.MKABID, m.FAMILY, m.NAME, m.OT
ORDER BY COUNT(*) DESC, MAX(COALESCE(aFI.DateDoc, aFA.DateCreate)) DESC;
