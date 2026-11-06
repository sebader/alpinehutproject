SET XACT_ABORT ON;
SET LOCK_TIMEOUT 5000;

IF DB_NAME() <> N'alpinehutsdb'
    THROW 51000, 'Unexpected database; retired huts were not updated.', 1;

DECLARE @Removed TABLE (Id int PRIMARY KEY, Link nvarchar(2048));
INSERT INTO @Removed (Id, Link) VALUES
    (10141, N'https://www.huetten-holiday.com/huts/veitenhof-im-kaisertal'),
    (10164, N'https://www.huetten-holiday.com/huts/sadnighaus'),
    (10167, N'https://www.huetten-holiday.com/huts/faschingsalm-loft');

BEGIN TRANSACTION;

IF (
    SELECT COUNT(*)
    FROM dbo.Huts h WITH (UPDLOCK, HOLDLOCK)
    JOIN @Removed r ON h.Id = r.Id AND h.Link = r.Link
    WHERE h.Source = N'HuettenHoliday'
      AND (h.Enabled = 0 OR h.LastUpdated < CONVERT(datetime2, '2026-11-05T00:00:00'))
) <> 3
    THROW 51000, 'Provider records changed; review before retiring any huts.', 1;

-- These URLs return 404 and the complete provider catalog no longer lists them.
-- Preserve manual edits and all historical availability; change eligibility only.
UPDATE h
SET Enabled = 0
OUTPUT inserted.Id, deleted.Enabled AS WasEnabled, inserted.Enabled AS IsEnabled
FROM dbo.Huts h
JOIN @Removed r ON h.Id = r.Id
WHERE h.Enabled = 1;

COMMIT TRANSACTION;
