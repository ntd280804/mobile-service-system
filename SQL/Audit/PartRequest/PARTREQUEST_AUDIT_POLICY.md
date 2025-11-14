## PART_REQUEST Audit Policy (INSERT, UPDATE)

### Intent
- Log ALL INSERT and UPDATE operations on part request tables regardless of role.
- Capture context (app role, employee id, client identifier) for every INSERT and UPDATE.

### Audit scope
- **All roles** - Every INSERT and UPDATE operation is logged

### Install steps
1. Ensure `SQL/Audit/init/02_audit_alert_pkg.sql` has been executed in the owning schema.
2. Run `SQL/Audit/PartRequest/01_partrequest_dml_audit.sql`.

### Quick test
```sql
-- Any INSERT or UPDATE will be logged
EXEC APP_CTX_PKG.set_role('ROLE_ADMIN');
INSERT INTO PART_REQUEST (PART_REQUEST_ID, ORDER_ID, STATUS) 
VALUES (1, 1, 'PENDING'); -- Logged

UPDATE PART_REQUEST SET STATUS = 'APPROVED' WHERE PART_REQUEST_ID = 1; -- Logged

SELECT app_role, object_name, policy_name, event_ts
FROM   audit_alert_log
WHERE  policy_name LIKE 'AUD_PART_REQUEST%'
ORDER BY log_id DESC;
```

### Remove / disable
```sql
BEGIN
  DBMS_FGA.DROP_POLICY(USER, 'PART_REQUEST',      'AUD_PART_REQUEST_INSERT');
  DBMS_FGA.DROP_POLICY(USER, 'PART_REQUEST',      'AUD_PART_REQUEST_UPDATE');
  DBMS_FGA.DROP_POLICY(USER, 'PART_REQUEST_ITEM', 'AUD_PART_REQUEST_ITEM_INSERT');
  DBMS_FGA.DROP_POLICY(USER, 'PART_REQUEST_ITEM', 'AUD_PART_REQUEST_ITEM_UPDATE');
END;
/
```

Or use: `@SQL/Audit/PartRequest/02_drop_partrequest_fga_policies.sql`

