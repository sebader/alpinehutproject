SET XACT_ABORT ON;
SET LOCK_TIMEOUT 5000;

IF DB_NAME() NOT IN (N'alpinehutsdb', N'alpinehutsdb_dev')
    THROW 51000, 'Unexpected database; HutWebsite migration was not applied.', 1;

IF NOT EXISTS (
    SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID(N'dbo.Huts')
      AND name = N'HutWebsite'
      AND TYPE_NAME(user_type_id) = N'nvarchar'
      AND is_nullable = 1
      AND collation_name = N'SQL_Latin1_General_CP1_CI_AS'
)
    THROW 51000, 'Unexpected HutWebsite definition; review before changing it.', 1;

BEGIN TRANSACTION;

IF EXISTS (
    SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID(N'dbo.Huts')
      AND name = N'HutWebsite'
      AND max_length <> -1
      AND max_length < 4096
)
    ALTER TABLE dbo.Huts
        ALTER COLUMN HutWebsite nvarchar(2048) COLLATE SQL_Latin1_General_CP1_CI_AS NULL;

COMMIT TRANSACTION;
