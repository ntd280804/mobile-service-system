-- Attach VPD policy to EMPLOYEE
-- Run as schema owner of EMPLOYEE (e.g., APP)

BEGIN
  DBMS_RLS.ADD_POLICY(
    object_schema   => USER,            -- or explicit owner
    object_name     => 'EMPLOYEE',
    policy_name     => 'EMPLOYEE_VPD',
    function_schema => USER,            -- or explicit owner
    policy_function => 'EMPLOYEE_VPD_PREDICATE',
    statement_types => 'SELECT',
    update_check    => FALSE,
    enable          => TRUE
  );
END;
/

