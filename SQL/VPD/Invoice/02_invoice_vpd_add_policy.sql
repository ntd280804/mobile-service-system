-- Attach VPD SELECT policies to INVOICE tables

BEGIN
  DBMS_RLS.ADD_POLICY(
    object_schema   => USER,
    object_name     => 'INVOICE',
    policy_name     => 'INVOICE_VPD',
    function_schema => USER,
    policy_function => 'INVOICE_VPD_PREDICATE',
    statement_types => 'SELECT',
    update_check    => FALSE,
    enable          => TRUE
  );

  DBMS_RLS.ADD_POLICY(
    object_schema   => USER,
    object_name     => 'INVOICE_ITEM',
    policy_name     => 'INVOICE_ITEM_VPD',
    function_schema => USER,
    policy_function => 'INVOICE_VPD_PREDICATE',
    statement_types => 'SELECT',
    update_check    => FALSE,
    enable          => TRUE
  );
END;
/


