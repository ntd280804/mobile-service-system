-- FGA policies for logging INSERT and UPDATE operations on stock tables
-- Logs every INSERT and UPDATE operation regardless of role

BEGIN
  -- STOCK_IN
  DBMS_FGA.ADD_POLICY(
    object_schema      => USER,
    object_name        => 'STOCK_IN',
    policy_name        => 'AUD_STOCK_IN_INSERT',
    handler_schema     => USER,
    handler_module     => 'AUDIT_ALERT_PKG.LOG_EVENT',
    enable             => TRUE,
    statement_types    => 'INSERT',
    audit_trail        => DBMS_FGA.DB + DBMS_FGA.EXTENDED,
    audit_column_opts  => DBMS_FGA.ALL_COLUMNS
  );

  DBMS_FGA.ADD_POLICY(
    object_schema      => USER,
    object_name        => 'STOCK_IN',
    policy_name        => 'AUD_STOCK_IN_UPDATE',
    handler_schema     => USER,
    handler_module     => 'AUDIT_ALERT_PKG.LOG_EVENT',
    enable             => TRUE,
    statement_types    => 'UPDATE',
    audit_trail        => DBMS_FGA.DB + DBMS_FGA.EXTENDED,
    audit_column_opts  => DBMS_FGA.ALL_COLUMNS
  );

  -- STOCK_IN_ITEM
  DBMS_FGA.ADD_POLICY(
    object_schema      => USER,
    object_name        => 'STOCK_IN_ITEM',
    policy_name        => 'AUD_STOCK_IN_ITEM_INSERT',
    handler_schema     => USER,
    handler_module     => 'AUDIT_ALERT_PKG.LOG_EVENT',
    enable             => TRUE,
    statement_types    => 'INSERT',
    audit_trail        => DBMS_FGA.DB + DBMS_FGA.EXTENDED,
    audit_column_opts  => DBMS_FGA.ALL_COLUMNS
  );

  DBMS_FGA.ADD_POLICY(
    object_schema      => USER,
    object_name        => 'STOCK_IN_ITEM',
    policy_name        => 'AUD_STOCK_IN_ITEM_UPDATE',
    handler_schema     => USER,
    handler_module     => 'AUDIT_ALERT_PKG.LOG_EVENT',
    enable             => TRUE,
    statement_types    => 'UPDATE',
    audit_trail        => DBMS_FGA.DB + DBMS_FGA.EXTENDED,
    audit_column_opts  => DBMS_FGA.ALL_COLUMNS
  );

  -- STOCK_OUT
  DBMS_FGA.ADD_POLICY(
    object_schema      => USER,
    object_name        => 'STOCK_OUT',
    policy_name        => 'AUD_STOCK_OUT_INSERT',
    handler_schema     => USER,
    handler_module     => 'AUDIT_ALERT_PKG.LOG_EVENT',
    enable             => TRUE,
    statement_types    => 'INSERT',
    audit_trail        => DBMS_FGA.DB + DBMS_FGA.EXTENDED,
    audit_column_opts  => DBMS_FGA.ALL_COLUMNS
  );

  DBMS_FGA.ADD_POLICY(
    object_schema      => USER,
    object_name        => 'STOCK_OUT',
    policy_name        => 'AUD_STOCK_OUT_UPDATE',
    handler_schema     => USER,
    handler_module     => 'AUDIT_ALERT_PKG.LOG_EVENT',
    enable             => TRUE,
    statement_types    => 'UPDATE',
    audit_trail        => DBMS_FGA.DB + DBMS_FGA.EXTENDED,
    audit_column_opts  => DBMS_FGA.ALL_COLUMNS
  );

  -- STOCK_OUT_ITEM
  DBMS_FGA.ADD_POLICY(
    object_schema      => USER,
    object_name        => 'STOCK_OUT_ITEM',
    policy_name        => 'AUD_STOCK_OUT_ITEM_INSERT',
    handler_schema     => USER,
    handler_module     => 'AUDIT_ALERT_PKG.LOG_EVENT',
    enable             => TRUE,
    statement_types    => 'INSERT',
    audit_trail        => DBMS_FGA.DB + DBMS_FGA.EXTENDED,
    audit_column_opts  => DBMS_FGA.ALL_COLUMNS
  );

  DBMS_FGA.ADD_POLICY(
    object_schema      => USER,
    object_name        => 'STOCK_OUT_ITEM',
    policy_name        => 'AUD_STOCK_OUT_ITEM_UPDATE',
    handler_schema     => USER,
    handler_module     => 'AUDIT_ALERT_PKG.LOG_EVENT',
    enable             => TRUE,
    statement_types    => 'UPDATE',
    audit_trail        => DBMS_FGA.DB + DBMS_FGA.EXTENDED,
    audit_column_opts  => DBMS_FGA.ALL_COLUMNS
  );
END;
/

