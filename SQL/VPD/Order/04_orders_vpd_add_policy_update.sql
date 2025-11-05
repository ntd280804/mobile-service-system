-- Attach VPD UPDATE policy to ORDERS
-- Run as schema owner of ORDERS

BEGIN
  DBMS_RLS.ADD_POLICY(
    object_schema   => USER,              -- or 'APP' if different
    object_name     => 'ORDERS',
    policy_name     => 'ORDERS_VPD_UPD',
    function_schema => USER,              -- or 'APP'
    policy_function => 'ORDERS_VPD_PREDICATE_UPD',
    statement_types => 'UPDATE',
    update_check    => TRUE,
    enable          => TRUE
  );
END;
/


