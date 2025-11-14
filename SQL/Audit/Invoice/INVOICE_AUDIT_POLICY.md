## INVOICE Audit Policy (INSERT, UPDATE)

### Intent
- Log ALL INSERT and UPDATE operations on invoice tables regardless of role.
- Capture context (app role, employee id, client identifier) for every INSERT and UPDATE.

### Audit scope
- **All roles** - Every INSERT and UPDATE operation is logged

### Install steps
1. Ensure `SQL/Audit/init/02_audit_alert_pkg.sql` has been executed in the owning schema.
2. Run `SQL/Audit/Invoice/01_invoice_dml_audit.sql`.

### Quick test
```sql
-- Any INSERT or UPDATE will be logged
EXEC APP_CTX_PKG.set_role('ROLE_ADMIN');
INSERT INTO INVOICE (INVOICE_ID, ORDER_ID, TOTAL_AMOUNT) 
VALUES (1, 1, 1000); -- Logged

UPDATE INVOICE SET TOTAL_AMOUNT = 1500 WHERE INVOICE_ID = 1; -- Logged

SELECT policy_name, app_role, object_name, event_ts
FROM   audit_alert_log
WHERE  policy_name LIKE 'AUD_INVOICE%'
ORDER BY log_id DESC;
```

### Remove / disable
```sql
BEGIN
  DBMS_FGA.DROP_POLICY(USER, 'INVOICE',      'AUD_INVOICE_INSERT');
  DBMS_FGA.DROP_POLICY(USER, 'INVOICE',      'AUD_INVOICE_UPDATE');
  DBMS_FGA.DROP_POLICY(USER, 'INVOICE_ITEM', 'AUD_INVOICE_ITEM_INSERT');
  DBMS_FGA.DROP_POLICY(USER, 'INVOICE_ITEM', 'AUD_INVOICE_ITEM_UPDATE');
END;
/
```

Or use: `@SQL/Audit/Invoice/02_drop_invoice_fga_policies.sql`

