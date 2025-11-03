-- OPTIONAL: Oracle Label Security (OLS) policy and levels for ORDERS
-- Run as LBACSYS (requires OLS installed and privileges)

BEGIN
  SA_SYSDBA.CREATE_POLICY(policy_name => 'ORDERS_OLS', column_name => 'ORDER_LABEL');
END;
/

BEGIN
  SA_COMPONENTS.CREATE_LEVEL('ORDERS_OLS', 10, 'L0', 'LOW');
  SA_COMPONENTS.CREATE_LEVEL('ORDERS_OLS', 20, 'L1', 'HIGH');
END;
/


