-- Attach VPD SELECT policies to stock tables
-- Run as schema owner (e.g., APP)

BEGIN
  DBMS_RLS.ADD_POLICY(
    object_schema   => USER,
    object_name     => 'STOCK_IN',
    policy_name     => 'STOCK_IN_VPD',
    function_schema => USER,
    policy_function => 'STOCK_VPD_PREDICATE',
    statement_types => 'SELECT',
    update_check    => FALSE,
    enable          => TRUE
  );

  DBMS_RLS.ADD_POLICY(
    object_schema   => USER,
    object_name     => 'STOCK_IN_ITEM',
    policy_name     => 'STOCK_IN_ITEM_VPD',
    function_schema => USER,
    policy_function => 'STOCK_VPD_PREDICATE',
    statement_types => 'SELECT',
    update_check    => FALSE,
    enable          => TRUE
  );

  DBMS_RLS.ADD_POLICY(
    object_schema   => USER,
    object_name     => 'STOCK_OUT',
    policy_name     => 'STOCK_OUT_VPD',
    function_schema => USER,
    policy_function => 'STOCK_VPD_PREDICATE',
    statement_types => 'SELECT',
    update_check    => FALSE,
    enable          => TRUE
  );

  DBMS_RLS.ADD_POLICY(
    object_schema   => USER,
    object_name     => 'STOCK_OUT_ITEM',
    policy_name     => 'STOCK_OUT_ITEM_VPD',
    function_schema => USER,
    policy_function => 'STOCK_VPD_PREDICATE',
    statement_types => 'SELECT',
    update_check    => FALSE,
    enable          => TRUE
  );
END;
/


