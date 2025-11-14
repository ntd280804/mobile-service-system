-- Drop FGA policies for Stock tables (INSERT, UPDATE)
-- Run as schema owner (e.g., APP)

BEGIN
  -- STOCK_IN policies
  BEGIN
    DBMS_FGA.DROP_POLICY(
      object_schema => USER,
      object_name   => 'STOCK_IN',
      policy_name   => 'AUD_STOCK_IN_INSERT'
    );
    DBMS_OUTPUT.PUT_LINE('Dropped: AUD_STOCK_IN_INSERT');
  EXCEPTION
    WHEN OTHERS THEN
      DBMS_OUTPUT.PUT_LINE('Error dropping AUD_STOCK_IN_INSERT: ' || SQLERRM);
  END;

  BEGIN
    DBMS_FGA.DROP_POLICY(
      object_schema => USER,
      object_name   => 'STOCK_IN',
      policy_name   => 'AUD_STOCK_IN_UPDATE'
    );
    DBMS_OUTPUT.PUT_LINE('Dropped: AUD_STOCK_IN_UPDATE');
  EXCEPTION
    WHEN OTHERS THEN
      DBMS_OUTPUT.PUT_LINE('Error dropping AUD_STOCK_IN_UPDATE: ' || SQLERRM);
  END;

  -- STOCK_IN_ITEM policies
  BEGIN
    DBMS_FGA.DROP_POLICY(
      object_schema => USER,
      object_name   => 'STOCK_IN_ITEM',
      policy_name   => 'AUD_STOCK_IN_ITEM_INSERT'
    );
    DBMS_OUTPUT.PUT_LINE('Dropped: AUD_STOCK_IN_ITEM_INSERT');
  EXCEPTION
    WHEN OTHERS THEN
      DBMS_OUTPUT.PUT_LINE('Error dropping AUD_STOCK_IN_ITEM_INSERT: ' || SQLERRM);
  END;

  BEGIN
    DBMS_FGA.DROP_POLICY(
      object_schema => USER,
      object_name   => 'STOCK_IN_ITEM',
      policy_name   => 'AUD_STOCK_IN_ITEM_UPDATE'
    );
    DBMS_OUTPUT.PUT_LINE('Dropped: AUD_STOCK_IN_ITEM_UPDATE');
  EXCEPTION
    WHEN OTHERS THEN
      DBMS_OUTPUT.PUT_LINE('Error dropping AUD_STOCK_IN_ITEM_UPDATE: ' || SQLERRM);
  END;

  -- STOCK_OUT policies
  BEGIN
    DBMS_FGA.DROP_POLICY(
      object_schema => USER,
      object_name   => 'STOCK_OUT',
      policy_name   => 'AUD_STOCK_OUT_INSERT'
    );
    DBMS_OUTPUT.PUT_LINE('Dropped: AUD_STOCK_OUT_INSERT');
  EXCEPTION
    WHEN OTHERS THEN
      DBMS_OUTPUT.PUT_LINE('Error dropping AUD_STOCK_OUT_INSERT: ' || SQLERRM);
  END;

  BEGIN
    DBMS_FGA.DROP_POLICY(
      object_schema => USER,
      object_name   => 'STOCK_OUT',
      policy_name   => 'AUD_STOCK_OUT_UPDATE'
    );
    DBMS_OUTPUT.PUT_LINE('Dropped: AUD_STOCK_OUT_UPDATE');
  EXCEPTION
    WHEN OTHERS THEN
      DBMS_OUTPUT.PUT_LINE('Error dropping AUD_STOCK_OUT_UPDATE: ' || SQLERRM);
  END;

  -- STOCK_OUT_ITEM policies
  BEGIN
    DBMS_FGA.DROP_POLICY(
      object_schema => USER,
      object_name   => 'STOCK_OUT_ITEM',
      policy_name   => 'AUD_STOCK_OUT_ITEM_INSERT'
    );
    DBMS_OUTPUT.PUT_LINE('Dropped: AUD_STOCK_OUT_ITEM_INSERT');
  EXCEPTION
    WHEN OTHERS THEN
      DBMS_OUTPUT.PUT_LINE('Error dropping AUD_STOCK_OUT_ITEM_INSERT: ' || SQLERRM);
  END;

  BEGIN
    DBMS_FGA.DROP_POLICY(
      object_schema => USER,
      object_name   => 'STOCK_OUT_ITEM',
      policy_name   => 'AUD_STOCK_OUT_ITEM_UPDATE'
    );
    DBMS_OUTPUT.PUT_LINE('Dropped: AUD_STOCK_OUT_ITEM_UPDATE');
  EXCEPTION
    WHEN OTHERS THEN
      DBMS_OUTPUT.PUT_LINE('Error dropping AUD_STOCK_OUT_ITEM_UPDATE: ' || SQLERRM);
  END;

  DBMS_OUTPUT.PUT_LINE('=== Finished dropping Stock FGA policies ===');
END;
/

