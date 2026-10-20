/*
  ШАБЛОН минимальных прав для Windows service account.
  Все значения ниже условные. Перед применением согласуйте с DBA.

  Замените DOMAIN\\SERVICE_ACCOUNT и имя базы локальными значениями.
*/

-- CREATE LOGIN [DOMAIN\SERVICE_ACCOUNT] FROM WINDOWS;
-- GO
-- USE [YOUR_DATABASE];
-- GO
-- CREATE USER [DOMAIN\SERVICE_ACCOUNT] FOR LOGIN [DOMAIN\SERVICE_ACCOUNT];
-- GO
-- GRANT SELECT ON dbo.hlt_MKAB TO [DOMAIN\SERVICE_ACCOUNT];
-- GRANT SELECT ON dbo.atf_FileInfo TO [DOMAIN\SERVICE_ACCOUNT];
-- GRANT SELECT ON dbo.atf_FileAttachment TO [DOMAIN\SERVICE_ACCOUNT];
-- GRANT SELECT ON dbo.atf_FileType TO [DOMAIN\SERVICE_ACCOUNT];
-- GO
