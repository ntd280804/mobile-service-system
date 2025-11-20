## CUSTOMER_APPOINTMENT Audit Policy (INSERT, UPDATE)

### Intent
- Log ALL INSERT and UPDATE operations on CUSTOMER_APPOINTMENT table regardless of role.
- Capture context (app role, employee id, client identifier) for every INSERT and UPDATE.

### Audit scope
- **All roles** - Every INSERT and UPDATE operation is logged

### Install steps
1. Ensure `SQL/Audit/init/02_audit_alert_pkg.sql` has been executed in the owning schema.
2. Run `SQL/Audit/Appointment/01_appointment_dml_audit.sql`.

### Quick test
```sql
-- Any INSERT or UPDATE will be logged
EXEC APP_CTX_PKG.set_role('ROLE_ADMIN');
INSERT INTO CUSTOMER_APPOINTMENT (APPOINTMENT_ID, CUSTOMER_PHONE, APPOINTMENT_DATE, STATUS) 
VALUES (1, '1234567890', SYSDATE, 'SCHEDULED'); -- Logged

UPDATE CUSTOMER_APPOINTMENT SET STATUS = 'COMPLETED' WHERE APPOINTMENT_ID = 1; -- Logged

SELECT event_ts, db_user, app_role, object_name, policy_name
FROM   audit_alert_log
WHERE  policy_name LIKE 'AUD_CUSTOMER_APPOINTMENT%'
ORDER BY log_id DESC;
```

### Remove / disable
```sql
BEGIN
  DBMS_FGA.DROP_POLICY(USER, 'CUSTOMER_APPOINTMENT', 'AUD_CUSTOMER_APPOINTMENT_INSERT');
  DBMS_FGA.DROP_POLICY(USER, 'CUSTOMER_APPOINTMENT', 'AUD_CUSTOMER_APPOINTMENT_UPDATE');
END;
/
```

Or use: `@SQL/Audit/Appointment/02_drop_appointment_fga_policies.sql`

