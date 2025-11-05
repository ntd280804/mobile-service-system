-- Attach VPD DELETE policy to ORDERS (deny all deletes)
-- Run as schema owner of ORDERS

BEGIN
  DBMS_RLS.ADD_POLICY(
    object_schema   => USER,              -- or 'APP' if different
    object_name     => 'ORDERS',
    policy_name     => 'ORDERS_VPD_DEL',
    function_schema => USER,              -- or 'APP'
    policy_function => 'ORDERS_VPD_PREDICATE_DEL',
    statement_types => 'DELETE',
    update_check    => FALSE,
    enable          => TRUE
  );
END;
/


