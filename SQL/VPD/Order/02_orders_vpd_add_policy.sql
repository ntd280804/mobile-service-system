-- Attach VPD policy to ORDERS
-- Run as schema owner of ORDERS

BEGIN
  DBMS_RLS.ADD_POLICY(
    object_schema   => USER,
    object_name     => 'ORDERS',
    policy_name     => 'ORDERS_VPD',
    function_schema => USER,
    policy_function => 'ORDERS_VPD_PREDICATE',
    statement_types => 'SELECT',
    update_check    => FALSE,
    enable          => TRUE
  );
END;
/


