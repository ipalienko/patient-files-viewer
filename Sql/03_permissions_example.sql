/*
  Рекомендуемая схема прав:

  1) MisDb — существующая read-only учётная запись для просмотра.
  2) MisWriteDb — отдельная минимально-привилегированная учётная запись только для прикрепления файлов.

  Все имена ниже условные. Перед применением согласуйте с DBA.
*/

-- ===== READ-ONLY account: MisDb =====
-- GRANT SELECT ON dbo.hlt_MKAB TO [READ_ACCOUNT];
-- GRANT SELECT ON dbo.atf_FileInfo TO [READ_ACCOUNT];
-- GRANT SELECT ON dbo.atf_FileAttachment TO [READ_ACCOUNT];
-- GRANT SELECT ON dbo.atf_FileType TO [READ_ACCOUNT];
-- GO

-- ===== WRITE account: MisWriteDb =====
-- Для проверки пациента и выбранного типа файла:
-- GRANT SELECT ON dbo.hlt_MKAB TO [WRITE_ACCOUNT];
-- GRANT SELECT ON dbo.atf_FileType TO [WRITE_ACCOUNT];

-- Для создания связки файла:
-- GRANT INSERT ON dbo.atf_FileInfo TO [WRITE_ACCOUNT];
-- GRANT INSERT ON dbo.atf_FileAttachment TO [WRITE_ACCOUNT];
-- GO

/*
  UPDATE/DELETE приложению для загрузки не нужны.
  Если триггеры МИС требуют дополнительных прав, это будет видно при тестовой вставке;
  не выдавайте db_owner/db_datawriter целиком без необходимости.
*/