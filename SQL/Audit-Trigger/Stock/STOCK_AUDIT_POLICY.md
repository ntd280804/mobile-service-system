## STOCK Audit Policy (INSERT, UPDATE)

### Intent
- Log ALL INSERT and UPDATE operations on stock tables regardless of role.
- Capture context (app role, employee id, client identifier) for every INSERT and UPDATE on:
  - `STOCK_IN`
  - `STOCK_IN_ITEM`
  - `STOCK_OUT`
  - `STOCK_OUT_ITEM`

### Audit scope
- **All roles** - Every INSERT and UPDATE operation is logged

### Install steps
1. Ensure `SQL/Audit/init/02_audit_alert_pkg.sql` has been executed in the owning schema.
2. Run `SQL/Audit/Stock/01_stock_dml_audit.sql`.

### Quick test
```sql
-- Any INSERT or UPDATE will be logged
EXEC APP_CTX_PKG.set_role('ROLE_ADMIN');
INSERT INTO STOCK_IN (EMP_ID, IN_DATE) VALUES (1, SYSDATE); -- Logged
UPDATE STOCK_IN SET NOTE = 'Updated' WHERE STOCKIN_ID = 1; -- Logged

SELECT event_ts, db_user, app_role, object_name, policy_name
FROM   audit_alert_log
WHERE  policy_name LIKE 'AUD_STOCK%'
ORDER BY log_id DESC;
```

### Remove / disable
```sql
BEGIN
  DBMS_FGA.DROP_POLICY(USER, 'STOCK_IN',      'AUD_STOCK_IN_INSERT');
  DBMS_FGA.DROP_POLICY(USER, 'STOCK_IN',      'AUD_STOCK_IN_UPDATE');
  DBMS_FGA.DROP_POLICY(USER, 'STOCK_IN_ITEM', 'AUD_STOCK_IN_ITEM_INSERT');
  DBMS_FGA.DROP_POLICY(USER, 'STOCK_IN_ITEM', 'AUD_STOCK_IN_ITEM_UPDATE');
  DBMS_FGA.DROP_POLICY(USER, 'STOCK_OUT',     'AUD_STOCK_OUT_INSERT');
  DBMS_FGA.DROP_POLICY(USER, 'STOCK_OUT',     'AUD_STOCK_OUT_UPDATE');
  DBMS_FGA.DROP_POLICY(USER, 'STOCK_OUT_ITEM','AUD_STOCK_OUT_ITEM_INSERT');
  DBMS_FGA.DROP_POLICY(USER, 'STOCK_OUT_ITEM','AUD_STOCK_OUT_ITEM_UPDATE');
END;
/
```

Or use: `@SQL/Audit/Stock/02_drop_stock_fga_policies.sql`

