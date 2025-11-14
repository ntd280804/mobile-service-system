## EMPLOYEE VPD Policy (Row-Level Security)

### Mục tiêu
- Áp dụng VPD (DBMS_RLS) để kiểm soát quyền xem dữ liệu theo dòng trên bảng `EMPLOYEE`.
- Tận dụng Application Context `APP_CTX` đã có: `ROLE_NAME`.

### Phạm vi áp dụng
- Bảng: `EMPLOYEE`
- Loại câu lệnh: `SELECT`

### Vai trò và predicate
- **ROLE_ADMIN**: `1=1` (xem tất cả)
- **ROLE_TIEPTAN**: `1=1` (xem tất cả)
- **ROLE_THUKHO**: only own rows 
- **ROLE_KITHUATVIEN**:only own rows 
- **ROLE_KHACHHANG**: only own rows 

### Thành phần cần có
- Application Context: `APP_CTX` (đã tồn tại – xem `SQL/VPD/init/02_app_context.sql`).
- VPD function: `EMPLOYEE_VPD_PREDICATE(schema, object) return varchar2` (file: `SQL/VPD/Employee/01_employee_vpd_function.sql`).
- VPD policy: `EMPLOYEE_VPD` gắn trên `EMPLOYEE` (file: `SQL/VPD/Employee/02_employee_vpd_add_policy.sql`).

### File liên quan
1. `SQL/VPD/init/01_roles_users_demo.sql` – Tạo role và user demo (tùy chọn cho test), grant `SELECT` trên `EMPLOYEE` cho role.
2. `SQL/VPD/init/02_app_context.sql` – Tạo `APP_CTX` và package `APP_CTX_PKG` (đã có sẵn).
3. `SQL/VPD/Employee/01_employee_vpd_function.sql` – Hàm `EMPLOYEE_VPD_PREDICATE` sinh predicate theo context.
4. `SQL/VPD/Employee/02_employee_vpd_add_policy.sql` – Gắn policy `EMPLOYEE_VPD` vào bảng `EMPLOYEE`.
5. `SQL/VPD/Employee/03_employee_vpd_tests.sql` – Script test các role.

> Lưu ý: Chạy các file (2→3→4) dưới schema OWNER thật sự của bảng `EMPLOYEE`.

### Trình tự cài đặt (install)
1) Đảm bảo bảng `EMPLOYEE` đã tồn tại và có dữ liệu.
2) Chạy `SQL/VPD/init/02_app_context.sql` dưới schema owner của `EMPLOYEE` (nếu chưa có).
3) Chạy `SQL/VPD/Employee/01_employee_vpd_function.sql` dưới schema owner của `EMPLOYEE`.
4) Chạy `SQL/VPD/Employee/02_employee_vpd_add_policy.sql` dưới schema owner của `EMPLOYEE`.

### Cách sử dụng trong session (test nhanh)
Thiết lập context trong CÙNG session trước khi query:

```sql
-- Xem nhanh context hiện tại
SELECT SYS_CONTEXT('APP_CTX','ROLE_NAME') AS ROLE_NAME
FROM dual;

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
```

### Tích hợp với ứng dụng
- Sau khi xác thực, ứng dụng cần gọi tương đương:
  - `APP_CTX_PKG.set_role('<ROLE_NAME>')` theo vai trò hiện tại của user.
- Các lời gọi này phải diễn ra trong cùng session DB dùng để query.

### Troubleshooting
- **COUNT = 0 dù là Admin/Tieptan**:
  - Chưa set context trong cùng session, hoặc policy gắn nhầm schema.
  - Kiểm tra:  
    `SELECT EMPLOYEE_VPD_PREDICATE(USER,'EMPLOYEE') FROM dual;`  
    `SELECT object_owner, object_name, policy_name FROM user_policies WHERE object_name='EMPLOYEE';`
- **ORA-00942 (table or view does not exist)**: chạy script dưới schema không phải owner `EMPLOYEE`.
- **ORA-01031 (insufficient privileges)**: thiếu quyền tạo context/package/policy. Chạy bằng schema owner có quyền cần thiết.
- **ORA-40442/DBMS_RLS errors**: xem lại tham số `object_schema`, `function_schema`. Thay `USER` bằng tên schema tường minh nếu cần.

### Vô hiệu hóa / Gỡ policy
```sql
-- Tạm tắt policy
BEGIN
  DBMS_RLS.ENABLE_POLICY(
    object_schema => '<OWNER_SCHEMA>',
    object_name   => 'EMPLOYEE',
    policy_name   => 'EMPLOYEE_VPD',
    enable        => FALSE
  );
END;
/

-- Bật lại policy
BEGIN
  DBMS_RLS.ENABLE_POLICY(
    object_schema => '<OWNER_SCHEMA>',
    object_name   => 'EMPLOYEE',
    policy_name   => 'EMPLOYEE_VPD',
    enable        => TRUE
  );
END;
/

-- Gỡ hẳn policy
BEGIN
  DBMS_RLS.DROP_POLICY(
    object_schema => '<OWNER_SCHEMA>',
    object_name   => 'EMPLOYEE',
    policy_name   => 'EMPLOYEE_VPD'
  );
END;
/
```

### Ghi chú bảo mật
- Không xây predicate dựa trên DB role đang bật; dùng Application Context để an toàn và linh hoạt.
- Chỉ expose các thủ tục context cần thiết; không để lộ thông tin nhạy cảm qua context.
- Bảng `EMPLOYEE` chỉ được xem bởi ADMIN và TIEPTAN để bảo vệ thông tin nhân viên.

### (Tùy chọn) OLS
- Có thể kết hợp OLS (SA policy) để phân tầng nhãn truy cập kết hợp với VPD theo vai trò. Cần quyền LBACSYS và cấu hình riêng.

