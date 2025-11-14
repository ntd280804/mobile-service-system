## EMPLOYEE Audit Policy (INSERT, UPDATE)

### Intent
- Log ALL INSERT and UPDATE operations on EMPLOYEE table regardless of role.
- Capture context (app role, employee id, client identifier) for every INSERT and UPDATE.

### Audit scope
- **All roles** - Every INSERT and UPDATE operation is logged

### Install steps
1. Ensure `SQL/Audit/init/02_audit_alert_pkg.sql` has been executed in the owning schema.
2. Run `SQL/Audit/Employee/01_employee_dml_audit.sql`.

### Quick test
```sql
-- Any INSERT or UPDATE will be logged
EXEC APP_CTX_PKG.set_role('ROLE_ADMIN');
INSERT INTO EMPLOYEE (FULL_NAME, USERNAME, PASSWORD_HASH, EMAIL, PHONE) 
VALUES ('Test User', 'testuser', 'hash', 'test@test.com', '1234567890'); -- Logged

UPDATE EMPLOYEE SET FULL_NAME = 'Updated Name' WHERE USERNAME = 'testuser'; -- Logged

SELECT event_ts, db_user, app_role, object_name, policy_name
FROM   audit_alert_log
WHERE  policy_name LIKE 'AUD_EMPLOYEE%'
ORDER BY log_id DESC;
```

### Remove / disable
```sql
BEGIN
  DBMS_FGA.DROP_POLICY(USER, 'EMPLOYEE', 'AUD_EMPLOYEE_INSERT');
  DBMS_FGA.DROP_POLICY(USER, 'EMPLOYEE', 'AUD_EMPLOYEE_UPDATE');
END;
/
```

Or use: `@SQL/Audit/Employee/02_drop_employee_fga_policies.sql`

