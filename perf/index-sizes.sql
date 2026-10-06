-- Size of every index on dbo.Requests (8 KB pages). Run: sqlcmd -S "(localdb)\MSSQLLocalDB" -d RequestTracker -W -i perf/index-sizes.sql
SELECT i.name AS [index], SUM(s.used_page_count) AS pages, CAST(SUM(s.used_page_count) * 8 / 1024.0 AS decimal(8,1)) AS mb,
       MAX(s.row_count) AS [rows]
FROM sys.dm_db_partition_stats s
JOIN sys.indexes i ON i.object_id = s.object_id AND i.index_id = s.index_id
WHERE s.object_id = OBJECT_ID('dbo.Requests')
GROUP BY i.name
ORDER BY pages DESC;
