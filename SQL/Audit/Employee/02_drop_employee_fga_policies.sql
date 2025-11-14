-- Drop FGA audit policies for EMPLOYEE table
-- Run as schema owner (e.g., APP)

BEGIN
  BEGIN
    DBMS_FGA.DROP_POLICY(
      object_schema => USER,
      object_name   => 'EMPLOYEE',
      policy_name   => 'AUD_EMPLOYEE_INSERT'
    );
    DBMS_OUTPUT.PUT_LINE('Dropped: AUD_EMPLOYEE_INSERT');
  EXCEPTION
    WHEN OTHERS THEN
      DBMS_OUTPUT.PUT_LINE('Error dropping AUD_EMPLOYEE_INSERT: ' || SQLERRM);
  END;

  BEGIN
    DBMS_FGA.DROP_POLICY(
      object_schema => USER,
      object_name   => 'EMPLOYEE',
      policy_name   => 'AUD_EMPLOYEE_UPDATE'
    );
    DBMS_OUTPUT.PUT_LINE('Dropped: AUD_EMPLOYEE_UPDATE');
  EXCEPTION
    WHEN OTHERS THEN
      DBMS_OUTPUT.PUT_LINE('Error dropping AUD_EMPLOYEE_UPDATE: ' || SQLERRM);
  END;

  DBMS_OUTPUT.PUT_LINE('=== Finished dropping EMPLOYEE FGA policies ===');
END;
/

