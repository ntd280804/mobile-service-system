-- Test scripts for VPD on CUSTOMER
-- Run after creating context (02_app_context.sql), function (03_customer_vpd_function.sql), and policy (04_customer_vpd_add_policy.sql)

-- ADMIN xem full
EXEC APP_CTX_PKG.set_role('ROLE_ADMIN');
SELECT COUNT(*) AS CNT_ADMIN FROM CUSTOMER;

-- TIEPTAN xem full
EXEC APP_CTX_PKG.set_role('ROLE_TIEPTAN');
SELECT COUNT(*) AS CNT_TIEPTAN FROM CUSTOMER;

-- KITHUATVIEN không được xem
EXEC APP_CTX_PKG.set_role('ROLE_KITHUATVIEN');
SELECT COUNT(*) AS CNT_KTV FROM CUSTOMER;

-- THUKHO không được xem
EXEC APP_CTX_PKG.set_role('ROLE_THUKHO');
SELECT COUNT(*) AS CNT_THUKHO FROM CUSTOMER;

-- KHACHHANG xem của cá nhân
EXEC APP_CTX_PKG.set_role('ROLE_KHACHHANG');
EXEC APP_CTX_PKG.set_customer('0911');
SELECT PHONE FROM CUSTOMER ORDER BY PHONE; -- chỉ thấy 0911

-- Inspect policy
SELECT CUSTOMER_VPD_PREDICATE(USER,'CUSTOMER') FROM dual;
SELECT object_owner, object_name, policy_name FROM user_policies WHERE object_name='CUSTOMER';


