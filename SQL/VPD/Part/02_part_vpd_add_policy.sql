-- Attach VPD to PART

BEGIN
  DBMS_RLS.ADD_POLICY(
    object_schema   => USER,
    object_name     => 'PART',
    policy_name     => 'PART_VPD',
    function_schema => USER,
    policy_function => 'PART_VPD_PREDICATE',
    statement_types => 'SELECT',
    update_check    => FALSE,
    enable          => TRUE
  );
END;
/


