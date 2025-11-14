-- Drop FGA audit policies for CUSTOMER_APPOINTMENT table
-- Run as schema owner (e.g., APP)

BEGIN
  BEGIN
    DBMS_FGA.DROP_POLICY(
      object_schema => USER,
      object_name   => 'CUSTOMER_APPOINTMENT',
      policy_name   => 'AUD_CUSTOMER_APPOINTMENT_INSERT'
    );
    DBMS_OUTPUT.PUT_LINE('Dropped: AUD_CUSTOMER_APPOINTMENT_INSERT');
  EXCEPTION
    WHEN OTHERS THEN
      DBMS_OUTPUT.PUT_LINE('Error dropping AUD_CUSTOMER_APPOINTMENT_INSERT: ' || SQLERRM);
  END;

  BEGIN
    DBMS_FGA.DROP_POLICY(
      object_schema => USER,
      object_name   => 'CUSTOMER_APPOINTMENT',
      policy_name   => 'AUD_CUSTOMER_APPOINTMENT_UPDATE'
    );
    DBMS_OUTPUT.PUT_LINE('Dropped: AUD_CUSTOMER_APPOINTMENT_UPDATE');
  EXCEPTION
    WHEN OTHERS THEN
      DBMS_OUTPUT.PUT_LINE('Error dropping AUD_CUSTOMER_APPOINTMENT_UPDATE: ' || SQLERRM);
  END;

  DBMS_OUTPUT.PUT_LINE('=== Finished dropping CUSTOMER_APPOINTMENT FGA policies ===');
END;
/

