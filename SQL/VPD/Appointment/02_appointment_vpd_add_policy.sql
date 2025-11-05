-- Attach VPD policy to CUSTOMER_APPOINTMENT
-- Run as schema owner of CUSTOMER_APPOINTMENT

BEGIN
  DBMS_RLS.ADD_POLICY(
    object_schema   => USER,              -- or 'APP' if different
    object_name     => 'CUSTOMER_APPOINTMENT',
    policy_name     => 'APPOINTMENT_VPD',
    function_schema => USER,              -- or 'APP'
    policy_function => 'APPOINTMENT_VPD_PREDICATE',
    statement_types => 'SELECT',
    update_check    => FALSE,
    enable          => TRUE
  );
END;
/


