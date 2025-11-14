## PART Audit Policy (INSERT, UPDATE)

### Intent
- Log ALL INSERT and UPDATE operations on PART table regardless of role.
- Capture context (app role, employee id, client identifier) for every INSERT and UPDATE.

### Audit scope
- **All roles** - Every INSERT and UPDATE operation is logged

### Install steps
1. Ensure `SQL/Audit/init/02_audit_alert_pkg.sql` has been executed in the owning schema.
2. Run `SQL/Audit/Part/01_part_dml_audit.sql`.

### Quick test
```sql
-- Any INSERT or UPDATE will be logged
EXEC APP_CTX_PKG.set_role('ROLE_ADMIN');
INSERT INTO PART (PART_ID, NAME, SERIAL, STATUS, STOCK_IN_ID) 
VALUES (1, 'Test Part', 'SERIAL001', 'AVAILABLE', 1); -- Logged

UPDATE PART SET STATUS = 'USED' WHERE PART_ID = 1; -- Logged

SELECT event_ts, db_user, app_role, object_name, policy_name
FROM   audit_alert_log
WHERE  policy_name LIKE 'AUD_PART%'
ORDER BY log_id DESC;
```

### Remove / disable
```sql
BEGIN
  DBMS_FGA.DROP_POLICY(USER, 'PART', 'AUD_PART_INSERT');
  DBMS_FGA.DROP_POLICY(USER, 'PART', 'AUD_PART_UPDATE');
END;
/
```

Or use: `@SQL/Audit/Part/02_drop_part_fga_policies.sql`

