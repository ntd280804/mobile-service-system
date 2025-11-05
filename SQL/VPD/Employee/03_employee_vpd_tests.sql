-- Test scripts for VPD on EMPLOYEE
-- Run after creating context (SQL/VPD/init/02_app_context.sql), function (01_employee_vpd_function.sql), and policy (02_employee_vpd_add_policy.sql)

-- ADMIN xem full
EXEC APP_CTX_PKG.set_role('ROLE_ADMIN');
SELECT COUNT(*) AS CNT_ADMIN FROM EMPLOYEE;

-- TIEPTAN xem full
EXEC APP_CTX_PKG.set_role('ROLE_TIEPTAN');
SELECT COUNT(*) AS CNT_TIEPTAN FROM EMPLOYEE;

-- KITHUATVIEN không được xem
EXEC APP_CTX_PKG.set_role('ROLE_KITHUATVIEN');
SELECT COUNT(*) AS CNT_KTV FROM EMPLOYEE;

-- THUKHO không được xem
EXEC APP_CTX_PKG.set_role('ROLE_THUKHO');
SELECT COUNT(*) AS CNT_THUKHO FROM EMPLOYEE;

-- KHACHHANG không được xem
EXEC APP_CTX_PKG.set_role('ROLE_KHACHHANG');
SELECT COUNT(*) AS CNT_KHACHHANG FROM EMPLOYEE;

-- Inspect policy
SELECT EMPLOYEE_VPD_PREDICATE(USER,'EMPLOYEE') FROM dual;
SELECT object_owner, object_name, policy_name FROM user_policies WHERE object_name='EMPLOYEE';

