-- Cost of the only hot write (a status change: Status, UpdatedAt and the rowversion change). Rolled back.
-- Run: sqlcmd -S "(localdb)\MSSQLLocalDB" -d RequestTracker -i perf/write-cost.sql
-- Compare with IX_Requests_CreatedAt disabled (ALTER INDEX IX_Requests_CreatedAt ON dbo.Requests DISABLE; ... REBUILD)
-- to see what the covering index adds: it now stores Status, UpdatedAt and RowVersion, so it must be maintained too.
SET NOCOUNT ON;
BEGIN TRAN;
SET STATISTICS IO ON;
UPDATE dbo.Requests SET Status = 3, UpdatedAt = SYSUTCDATETIME() WHERE Id = 50000;
SET STATISTICS IO OFF;
ROLLBACK;
