-- Attach VPD DELETE policy to CUSTOMER_APPOINTMENT (deny all deletes)
-- Run as schema owner of CUSTOMER_APPOINTMENT

BEGIN
  DBMS_RLS.ADD_POLICY(
    object_schema   => USER,              -- or 'APP' if different
    object_name     => 'CUSTOMER_APPOINTMENT',
    policy_name     => 'APPOINTMENT_VPD_DEL',
    function_schema => USER,              -- or 'APP'
    policy_function => 'APPOINTMENT_VPD_PREDICATE_DEL',
    statement_types => 'DELETE',
    update_check    => FALSE,
    enable          => TRUE
  );
END;
/


