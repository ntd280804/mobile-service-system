-- Drop FGA audit policies for PART table
-- Run as schema owner (e.g., APP)

BEGIN
  BEGIN
    DBMS_FGA.DROP_POLICY(
      object_schema => USER,
      object_name   => 'PART',
      policy_name   => 'AUD_PART_INSERT'
    );
    DBMS_OUTPUT.PUT_LINE('Dropped: AUD_PART_INSERT');
  EXCEPTION
    WHEN OTHERS THEN
      DBMS_OUTPUT.PUT_LINE('Error dropping AUD_PART_INSERT: ' || SQLERRM);
  END;

  BEGIN
    DBMS_FGA.DROP_POLICY(
      object_schema => USER,
      object_name   => 'PART',
      policy_name   => 'AUD_PART_UPDATE'
    );
    DBMS_OUTPUT.PUT_LINE('Dropped: AUD_PART_UPDATE');
  EXCEPTION
    WHEN OTHERS THEN
      DBMS_OUTPUT.PUT_LINE('Error dropping AUD_PART_UPDATE: ' || SQLERRM);
  END;

  DBMS_OUTPUT.PUT_LINE('=== Finished dropping PART FGA policies ===');
END;
/

