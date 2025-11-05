-- Attach VPD to PART_REQUEST and PART_REQUEST_ITEM

BEGIN
  DBMS_RLS.ADD_POLICY(
    object_schema   => USER,
    object_name     => 'PART_REQUEST',
    policy_name     => 'PART_REQUEST_VPD',
    function_schema => USER,
    policy_function => 'PARTREQUEST_VPD_PREDICATE',
    statement_types => 'SELECT',
    update_check    => FALSE,
    enable          => TRUE
  );

  DBMS_RLS.ADD_POLICY(
    object_schema   => USER,
    object_name     => 'PART_REQUEST_ITEM',
    policy_name     => 'PART_REQUEST_ITEM_VPD',
    function_schema => USER,
    policy_function => 'PARTREQUEST_VPD_PREDICATE',
    statement_types => 'SELECT',
    update_check    => FALSE,
    enable          => TRUE
  );
END;
/


