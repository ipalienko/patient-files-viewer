/*
  Диагностика перед включением веб-загрузки.
  Укажите MKABID пациента, которому недавно прикрепляли файл штатно через десктопную МИС.

  Скрипт ничего не изменяет. Он показывает:
  - фактический Path;
  - GUID записи atf_FileAttachment;
  - GUID atf_FileInfo;
  - имя файла из Path;
  - UUID-основу имени файла, если она распознаётся.

  Это позволяет сверить соглашение об именовании файлов в конкретной инсталляции МИС.
*/
DECLARE @MKABID int = 123456;

;WITH X AS
(
    SELECT TOP (50)
        m.MKABID,
        aFA.FileAttachmentID,
        aFA.Guid AS AttachmentGuid,
        aFI.FileInfoID,
        aFI.Guid AS FileInfoGuid,
        aFT.FileTypeID,
        aFT.Code AS FileTypeCode,
        aFT.Name AS FileTypeName,
        aFA.DateCreate,
        aFI.DateDoc,
        aFA.Path,
        RIGHT(aFA.Path, CHARINDEX('\', REVERSE(aFA.Path) + '\') - 1) AS FileName
    FROM dbo.hlt_MKAB m
    JOIN dbo.atf_FileInfo aFI
      ON aFI.DescGuid = m.UGUID
    JOIN dbo.atf_FileAttachment aFA
      ON aFA.rf_FileInfoID = aFI.FileInfoID
    LEFT JOIN dbo.atf_FileType aFT
      ON aFT.FileTypeID = aFI.rf_FileTypeID
    WHERE m.MKABID = @MKABID
    ORDER BY aFA.FileAttachmentID DESC
), Y AS
(
    SELECT *,
        CASE
            WHEN CHARINDEX('.', REVERSE(FileName)) > 0
            THEN LEFT(FileName, LEN(FileName) - CHARINDEX('.', REVERSE(FileName)))
            ELSE FileName
        END AS FileBaseName
    FROM X
)
SELECT
    MKABID,
    FileAttachmentID,
    AttachmentGuid,
    FileInfoID,
    FileInfoGuid,
    FileTypeID,
    FileTypeCode,
    FileTypeName,
    DateCreate,
    DateDoc,
    Path,
    FileName,
    FileBaseName,
    TRY_CONVERT(uniqueidentifier, FileBaseName) AS FileNameGuid,
    CASE WHEN TRY_CONVERT(uniqueidentifier, FileBaseName) = AttachmentGuid THEN 1 ELSE 0 END AS FileNameEqualsAttachmentGuid,
    CASE WHEN TRY_CONVERT(uniqueidentifier, FileBaseName) = FileInfoGuid THEN 1 ELSE 0 END AS FileNameEqualsFileInfoGuid
FROM Y
ORDER BY FileAttachmentID DESC;