BEGIN
  -- EMPLOYEE
  DBMS_FGA.ADD_POLICY(
    object_schema      => USER,
    object_name        => 'EMPLOYEE',
    policy_name        => 'FGA_EMPLOYEE_DML',
    statement_types    => 'INSERT,UPDATE,DELETE',
    audit_trail        => DBMS_FGA.DB + DBMS_FGA.EXTENDED,
    audit_column_opts  => DBMS_FGA.ALL_COLUMNS,
    enable             => TRUE
  );

  -- CUSTOMER
  DBMS_FGA.ADD_POLICY(
    object_schema      => USER,
    object_name        => 'CUSTOMER',
    policy_name        => 'FGA_CUSTOMER_DML',
    statement_types    => 'INSERT,UPDATE,DELETE',
    audit_trail        => DBMS_FGA.DB + DBMS_FGA.EXTENDED,
    audit_column_opts  => DBMS_FGA.ALL_COLUMNS,
    enable             => TRUE
  );

  -- ORDERS
  DBMS_FGA.ADD_POLICY(
    object_schema      => USER,
    object_name        => 'ORDERS',
    policy_name        => 'FGA_ORDERS_DML',
    statement_types    => 'INSERT,UPDATE,DELETE',
    audit_trail        => DBMS_FGA.DB + DBMS_FGA.EXTENDED,
    audit_column_opts  => DBMS_FGA.ALL_COLUMNS,
    enable             => TRUE
  );

  -- STOCK_IN
  DBMS_FGA.ADD_POLICY(
    object_schema      => USER,
    object_name        => 'STOCK_IN',
    policy_name        => 'FGA_STOCK_IN_DML',
    statement_types    => 'INSERT,UPDATE,DELETE',
    audit_trail        => DBMS_FGA.DB + DBMS_FGA.EXTENDED,
    audit_column_opts  => DBMS_FGA.ALL_COLUMNS,
    enable             => TRUE
  );

  -- STOCK_IN_ITEM
  DBMS_FGA.ADD_POLICY(
    object_schema      => USER,
    object_name        => 'STOCK_IN_ITEM',
    policy_name        => 'FGA_STOCK_IN_ITEM_DML',
    statement_types    => 'INSERT,UPDATE,DELETE',
    audit_trail        => DBMS_FGA.DB + DBMS_FGA.EXTENDED,
    audit_column_opts  => DBMS_FGA.ALL_COLUMNS,
    enable             => TRUE
  );

  -- PART
  DBMS_FGA.ADD_POLICY(
    object_schema      => USER,
    object_name        => 'PART',
    policy_name        => 'FGA_PART_DML',
    statement_types    => 'INSERT,UPDATE,DELETE',
    audit_trail        => DBMS_FGA.DB + DBMS_FGA.EXTENDED,
    audit_column_opts  => DBMS_FGA.ALL_COLUMNS,
    enable             => TRUE
  );

  -- STOCK_OUT
  DBMS_FGA.ADD_POLICY(
    object_schema      => USER,
    object_name        => 'STOCK_OUT',
    policy_name        => 'FGA_STOCK_OUT_DML',
    statement_types    => 'INSERT,UPDATE,DELETE',
    audit_trail        => DBMS_FGA.DB + DBMS_FGA.EXTENDED,
    audit_column_opts  => DBMS_FGA.ALL_COLUMNS,
    enable             => TRUE
  );

  -- STOCK_OUT_ITEM
  DBMS_FGA.ADD_POLICY(
    object_schema      => USER,
    object_name        => 'STOCK_OUT_ITEM',
    policy_name        => 'FGA_STOCK_OUT_ITEM_DML',
    statement_types    => 'INSERT,UPDATE,DELETE',
    audit_trail        => DBMS_FGA.DB + DBMS_FGA.EXTENDED,
    audit_column_opts  => DBMS_FGA.ALL_COLUMNS,
    enable             => TRUE
  );

  -- PART_REQUEST
  DBMS_FGA.ADD_POLICY(
    object_schema      => USER,
    object_name        => 'PART_REQUEST',
    policy_name        => 'FGA_PART_REQUEST_DML',
    statement_types    => 'INSERT,UPDATE,DELETE',
    audit_trail        => DBMS_FGA.DB + DBMS_FGA.EXTENDED,
    audit_column_opts  => DBMS_FGA.ALL_COLUMNS,
    enable             => TRUE
  );

  -- PART_REQUEST_ITEM
  DBMS_FGA.ADD_POLICY(
    object_schema      => USER,
    object_name        => 'PART_REQUEST_ITEM',
    policy_name        => 'FGA_PART_REQUEST_ITEM_DML',
    statement_types    => 'INSERT,UPDATE,DELETE',
    audit_trail        => DBMS_FGA.DB + DBMS_FGA.EXTENDED,
    audit_column_opts  => DBMS_FGA.ALL_COLUMNS,
    enable             => TRUE
  );

  -- USER_OTP_LOG
  DBMS_FGA.ADD_POLICY(
    object_schema      => USER,
    object_name        => 'USER_OTP_LOG',
    policy_name        => 'FGA_USER_OTP_LOG_DML',
    statement_types    => 'INSERT,UPDATE,DELETE',
    audit_trail        => DBMS_FGA.DB + DBMS_FGA.EXTENDED,
    audit_column_opts  => DBMS_FGA.ALL_COLUMNS,
    enable             => TRUE
  );

  -- EMPLOYEE_SHIFT
  DBMS_FGA.ADD_POLICY(
    object_schema      => USER,
    object_name        => 'EMPLOYEE_SHIFT',
    policy_name        => 'FGA_EMPLOYEE_SHIFT_DML',
    statement_types    => 'INSERT,UPDATE,DELETE',
    audit_trail        => DBMS_FGA.DB + DBMS_FGA.EXTENDED,
    audit_column_opts  => DBMS_FGA.ALL_COLUMNS,
    enable             => TRUE
  );

  -- CUSTOMER_APPOINTMENT
  DBMS_FGA.ADD_POLICY(
    object_schema      => USER,
    object_name        => 'CUSTOMER_APPOINTMENT',
    policy_name        => 'FGA_CUSTOMER_APPOINTMENT_DML',
    statement_types    => 'INSERT,UPDATE,DELETE',
    audit_trail        => DBMS_FGA.DB + DBMS_FGA.EXTENDED,
    audit_column_opts  => DBMS_FGA.ALL_COLUMNS,
    enable             => TRUE
  );

  -- INVOICE
  DBMS_FGA.ADD_POLICY(
    object_schema      => USER,
    object_name        => 'INVOICE',
    policy_name        => 'FGA_INVOICE_DML',
    statement_types    => 'INSERT,UPDATE,DELETE',
    audit_trail        => DBMS_FGA.DB + DBMS_FGA.EXTENDED,
    audit_column_opts  => DBMS_FGA.ALL_COLUMNS,
    enable             => TRUE
  );

  -- INVOICE_ITEM
  DBMS_FGA.ADD_POLICY(
    object_schema      => USER,
    object_name        => 'INVOICE_ITEM',
    policy_name        => 'FGA_INVOICE_ITEM_DML',
    statement_types    => 'INSERT,UPDATE,DELETE',
    audit_trail        => DBMS_FGA.DB + DBMS_FGA.EXTENDED,
    audit_column_opts  => DBMS_FGA.ALL_COLUMNS,
    enable             => TRUE
  );

  -- SERVICE
  DBMS_FGA.ADD_POLICY(
    object_schema      => USER,
    object_name        => 'SERVICE',
    policy_name        => 'FGA_SERVICE_DML',
    statement_types    => 'INSERT,UPDATE,DELETE',
    audit_trail        => DBMS_FGA.DB + DBMS_FGA.EXTENDED,
    audit_column_opts  => DBMS_FGA.ALL_COLUMNS,
    enable             => TRUE
  );

  -- ORDER_SERVICE
  DBMS_FGA.ADD_POLICY(
    object_schema      => USER,
    object_name        => 'ORDER_SERVICE',
    policy_name        => 'FGA_ORDER_SERVICE_DML',
    statement_types    => 'INSERT,UPDATE,DELETE',
    audit_trail        => DBMS_FGA.DB + DBMS_FGA.EXTENDED,
    audit_column_opts  => DBMS_FGA.ALL_COLUMNS,
    enable             => TRUE
  );

  -- INVOICE_SERVICE
  DBMS_FGA.ADD_POLICY(
    object_schema      => USER,
    object_name        => 'INVOICE_SERVICE',
    policy_name        => 'FGA_INVOICE_SERVICE_DML',
    statement_types    => 'INSERT,UPDATE,DELETE',
    audit_trail        => DBMS_FGA.DB + DBMS_FGA.EXTENDED,
    audit_column_opts  => DBMS_FGA.ALL_COLUMNS,
    enable             => TRUE
  );

END;
/
