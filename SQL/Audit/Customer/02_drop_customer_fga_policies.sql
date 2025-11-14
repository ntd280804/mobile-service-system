-- Drop FGA audit policies for CUSTOMER table
-- Run as schema owner (e.g., APP)

BEGIN
  BEGIN
    DBMS_FGA.DROP_POLICY(
      object_schema => USER,
      object_name   => 'CUSTOMER',
      policy_name   => 'AUD_CUSTOMER_INSERT'
    );
    DBMS_OUTPUT.PUT_LINE('Dropped: AUD_CUSTOMER_INSERT');
  EXCEPTION
    WHEN OTHERS THEN
      DBMS_OUTPUT.PUT_LINE('Error dropping AUD_CUSTOMER_INSERT: ' || SQLERRM);
  END;

  BEGIN
    DBMS_FGA.DROP_POLICY(
      object_schema => USER,
      object_name   => 'CUSTOMER',
      policy_name   => 'AUD_CUSTOMER_UPDATE'
    );
    DBMS_OUTPUT.PUT_LINE('Dropped: AUD_CUSTOMER_UPDATE');
  EXCEPTION
    WHEN OTHERS THEN
      DBMS_OUTPUT.PUT_LINE('Error dropping AUD_CUSTOMER_UPDATE: ' || SQLERRM);
  END;

  DBMS_OUTPUT.PUT_LINE('=== Finished dropping CUSTOMER FGA policies ===');
END;
/

