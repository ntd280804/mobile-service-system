## ORDERS Audit Policy (INSERT, UPDATE)

### Intent
- Log ALL INSERT and UPDATE operations on ORDERS table regardless of role.
- Capture context (app role, employee id, client identifier) for every INSERT and UPDATE.

### Audit scope
- **All roles** - Every INSERT and UPDATE operation is logged

### Install steps
1. Ensure `SQL/Audit/init/02_audit_alert_pkg.sql` has been executed in the owning schema.
2. Run `SQL/Audit/Order/01_order_dml_audit.sql`.

### Quick test
```sql
-- Any INSERT or UPDATE will be logged
EXEC APP_CTX_PKG.set_role('ROLE_ADMIN');
INSERT INTO ORDERS (ORDER_ID, CUSTOMER_PHONE, RECEIVER_EMP, HANDLER_EMP, ORDER_TYPE, RECEIVED_DATE, STATUS) 
VALUES (1, '1234567890', 1, 1, 'REPAIR', SYSDATE, 'PENDING'); -- Logged

UPDATE ORDERS SET STATUS = 'IN_PROGRESS' WHERE ORDER_ID = 1; -- Logged

SELECT policy_name, app_role, object_name, event_ts
FROM   audit_alert_log
WHERE  policy_name LIKE 'AUD_ORDERS%'
ORDER BY log_id DESC;
```

### Remove / disable
```sql
BEGIN
  DBMS_FGA.DROP_POLICY(USER, 'ORDERS', 'AUD_ORDERS_INSERT');
  DBMS_FGA.DROP_POLICY(USER, 'ORDERS', 'AUD_ORDERS_UPDATE');
END;
/
```

Or use: `@SQL/Audit/Order/02_drop_order_fga_policies.sql`

