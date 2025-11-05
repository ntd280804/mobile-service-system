-- Attach VPD policy to CUSTOMER
-- Run as schema owner of CUSTOMER (e.g., APP)

BEGIN
  DBMS_RLS.ADD_POLICY(
    object_schema   => USER,            -- or explicit owner
    object_name     => 'CUSTOMER',
    policy_name     => 'CUSTOMER_VPD',
    function_schema => USER,            -- or explicit owner
    policy_function => 'CUSTOMER_VPD_PREDICATE',
    statement_types => 'SELECT',
    update_check    => FALSE,
    enable          => TRUE
  );
END;
/


