## CUSTOMER Audit Policy (INSERT, UPDATE)

### Intent
- Log ALL INSERT and UPDATE operations on CUSTOMER table regardless of role.
- Capture context (app role, employee id, client identifier) for every INSERT and UPDATE.

### Audit scope
- **All roles** - Every INSERT and UPDATE operation is logged

### Install steps
1. Ensure `SQL/Audit/init/02_audit_alert_pkg.sql` has been executed in the owning schema.
2. Run `SQL/Audit/Customer/01_customer_dml_audit.sql`.

### Quick test
```sql
-- Any INSERT or UPDATE will be logged
EXEC APP_CTX_PKG.set_role('ROLE_ADMIN');
INSERT INTO CUSTOMER (PHONE, FULL_NAME, EMAIL) 
VALUES ('1234567890', 'Test Customer', 'test@test.com'); -- Logged

UPDATE CUSTOMER SET FULL_NAME = 'Updated Name' WHERE PHONE = '1234567890'; -- Logged

SELECT event_ts, db_user, app_role, object_name, policy_name
FROM   audit_alert_log
WHERE  policy_name LIKE 'AUD_CUSTOMER%'
ORDER BY log_id DESC;
```

### Remove / disable
```sql
BEGIN
  DBMS_FGA.DROP_POLICY(USER, 'CUSTOMER', 'AUD_CUSTOMER_INSERT');
  DBMS_FGA.DROP_POLICY(USER, 'CUSTOMER', 'AUD_CUSTOMER_UPDATE');
END;
/
```

Or use: `@SQL/Audit/Customer/02_drop_customer_fga_policies.sql`

