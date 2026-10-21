/*
  ШАБЛОН минимальных прав для функции просмотра и прикрепления.
  Все имена учётных записей и базы ниже условные.

  Рекомендуется использовать две учётные записи:
  1) read-only для MisDb;
  2) отдельную для MisWriteDb.
*/

-- Пример для MisWriteDb (Windows Login):
-- CREATE LOGIN [DOMAIN\SERVICE_ACCOUNT] FROM WINDOWS;
-- GO
-- USE [YOUR_DATABASE];
-- GO
-- CREATE USER [DOMAIN\SERVICE_ACCOUNT] FOR LOGIN [DOMAIN\SERVICE_ACCOUNT];
-- GO

-- Чтение параметров и проверка пациента/типа файла:
-- GRANT SELECT ON dbo.hlt_MKAB TO [DOMAIN\SERVICE_ACCOUNT];
-- GRANT SELECT ON dbo.atf_FileType TO [DOMAIN\SERVICE_ACCOUNT];
-- GRANT SELECT ON dbo.x_DocTypeDef TO [DOMAIN\SERVICE_ACCOUNT];
-- GRANT SELECT ON dbo.x_UserSettings TO [DOMAIN\SERVICE_ACCOUNT];

-- MERGE + последующий SELECT FileInfoID/FileAttachmentID:
-- GRANT SELECT, INSERT ON dbo.atf_FileInfo TO [DOMAIN\SERVICE_ACCOUNT];
-- GRANT SELECT, INSERT ON dbo.atf_FileAttachment TO [DOMAIN\SERVICE_ACCOUNT];
-- GO

-- UPDATE и DELETE модулю прикрепления не требуются.
-- Новые atf_FileType веб-модуль не создаёт: пользователь выбирает существующий тип.