-- OPTIONAL: Assign OLS levels to users and example label mapping
-- Run as LBACSYS or user with SA_USER_ADMIN privileges

-- Admin / Tieptan can read HIGH (implicitly includes LOW depending on dominance rules)
BEGIN
  SA_USER_ADMIN.SET_LEVELS('ORDERS_OLS','U_ADMIN','L1');
  SA_USER_ADMIN.SET_LEVELS('ORDERS_OLS','U_TIEPTAN','L1');
END;
/

-- KTV internal as HIGH
BEGIN
  SA_USER_ADMIN.SET_LEVELS('ORDERS_OLS','U_KTV','L1');
END;
/

-- Customer as LOW only
BEGIN
  SA_USER_ADMIN.SET_LEVELS('ORDERS_OLS','U_KH','L0');
END;
/

-- Thukho: no level assignment -> cannot read


