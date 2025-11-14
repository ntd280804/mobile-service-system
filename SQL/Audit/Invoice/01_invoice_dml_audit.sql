-- FGA policies for logging INSERT and UPDATE operations on invoice tables
-- Logs every INSERT and UPDATE operation regardless of role

BEGIN
  DBMS_FGA.ADD_POLICY(
    object_schema      => USER,
    object_name        => 'INVOICE',
    policy_name        => 'AUD_INVOICE_INSERT',
    handler_schema     => USER,
    handler_module     => 'AUDIT_ALERT_PKG.LOG_EVENT',
    enable             => TRUE,
    statement_types    => 'INSERT',
    audit_trail        => DBMS_FGA.DB + DBMS_FGA.EXTENDED,
    audit_column_opts  => DBMS_FGA.ALL_COLUMNS
  );

  DBMS_FGA.ADD_POLICY(
    object_schema      => USER,
    object_name        => 'INVOICE',
    policy_name        => 'AUD_INVOICE_UPDATE',
    handler_schema     => USER,
    handler_module     => 'AUDIT_ALERT_PKG.LOG_EVENT',
    enable             => TRUE,
    statement_types    => 'UPDATE',
    audit_trail        => DBMS_FGA.DB + DBMS_FGA.EXTENDED,
    audit_column_opts  => DBMS_FGA.ALL_COLUMNS
  );

  DBMS_FGA.ADD_POLICY(
    object_schema      => USER,
    object_name        => 'INVOICE_ITEM',
    policy_name        => 'AUD_INVOICE_ITEM_INSERT',
    handler_schema     => USER,
    handler_module     => 'AUDIT_ALERT_PKG.LOG_EVENT',
    enable             => TRUE,
    statement_types    => 'INSERT',
    audit_trail        => DBMS_FGA.DB + DBMS_FGA.EXTENDED,
    audit_column_opts  => DBMS_FGA.ALL_COLUMNS
  );

  DBMS_FGA.ADD_POLICY(
    object_schema      => USER,
    object_name        => 'INVOICE_ITEM',
    policy_name        => 'AUD_INVOICE_ITEM_UPDATE',
    handler_schema     => USER,
    handler_module     => 'AUDIT_ALERT_PKG.LOG_EVENT',
    enable             => TRUE,
    statement_types    => 'UPDATE',
    audit_trail        => DBMS_FGA.DB + DBMS_FGA.EXTENDED,
    audit_column_opts  => DBMS_FGA.ALL_COLUMNS
  );
END;
/

