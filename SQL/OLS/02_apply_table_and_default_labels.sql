-- OPTIONAL: Apply OLS policy to ORDERS and set default labels
-- Run as schema owner of ORDERS with required OLS grants

BEGIN
  SA_POLICY_ADMIN.APPLY_TABLE_POLICY(
    policy_name  => 'ORDERS_OLS',
    schema_name  => USER,
    table_name   => 'ORDERS',
    table_options => 'READ_CONTROL'
  );
END;
/

-- Example: set default label L0 for existing rows
UPDATE ORDERS SET ORDER_LABEL = CHAR_TO_LABEL('ORDERS_OLS','L0');
COMMIT;


