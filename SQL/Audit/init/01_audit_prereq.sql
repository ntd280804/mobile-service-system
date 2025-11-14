-- Verify that the database is configured to capture traditional audit + FGA details
-- Run as SYS (or any user with access to V$PARAMETER)

SELECT name, value
FROM   v$parameter
WHERE  name = 'audit_trail';

-- If VALUE = NONE, enable it (requires restart):
-- ALTER SYSTEM SET audit_trail = 'DB, EXTENDED' SCOPE=SPFILE;
-- SHUTDOWN IMMEDIATE;
-- STARTUP;

-- Optional: enable unified auditing or write audit rows into a custom table/view
-- as described in Oracle Database Security Guide (Unified Auditing chapter).


