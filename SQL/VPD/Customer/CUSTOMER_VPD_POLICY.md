## CUSTOMER VPD Policy (Row-Level Security)

### Mục tiêu
- Áp dụng VPD (DBMS_RLS) để kiểm soát quyền xem dữ liệu theo dòng trên bảng `CUSTOMER`.
- Tận dụng Application Context `APP_CTX` đã có: `ROLE_NAME`, `CUSTOMER_PHONE`.

### Phạm vi áp dụng
- Bảng: `CUSTOMER`
- Loại câu lệnh: `SELECT`

### Vai trò và predicate
- **ROLE_ADMIN**: `1=1` (xem tất cả)
- **ROLE_TIEPTAN**: `1=1` (xem tất cả)
- **ROLE_THUKHO**: `1=0` (không xem được)
- **ROLE_KITHUATVIEN**: `1=0` (không xem được)
- **ROLE_KHACHHANG**: `PHONE = :CUSTOMER_PHONE` (chỉ xem của cá nhân)

### Thành phần cần có
- Application Context: `APP_CTX` (đã tồn tại – xem `SQL/VPD/init/02_app_context.sql`).
- VPD function: `CUSTOMER_VPD_PREDICATE(schema, object) return varchar2` (file: `SQL/VPD/Customer/01_customer_vpd_function.sql`).
- VPD policy: `CUSTOMER_VPD` gắn trên `CUSTOMER` (file: `SQL/VPD/Customer/02_customer_vpd_add_policy.sql`).

### File liên quan
1. `SQL/VPD/init/01_roles_users_demo.sql` – Tạo role và user demo (tùy chọn cho test), grant `SELECT` trên `CUSTOMER` cho role.
2. `SQL/VPD/init/02_app_context.sql` – Tạo `APP_CTX` và package `APP_CTX_PKG` (đã có sẵn).
3. `SQL/VPD/Customer/01_customer_vpd_function.sql` – Hàm `CUSTOMER_VPD_PREDICATE` sinh predicate theo context.
4. `SQL/VPD/Customer/02_customer_vpd_add_policy.sql` – Gắn policy `CUSTOMER_VPD` vào bảng `CUSTOMER`.
5. `SQL/VPD/Customer/03_customer_vpd_tests.sql` – Script test các role.

> Lưu ý: Chạy các file (2→3→4) dưới schema OWNER thật sự của bảng `CUSTOMER`.

### Trình tự cài đặt (install)
1) Đảm bảo bảng `CUSTOMER` đã tồn tại và có dữ liệu.
2) Chạy `SQL/VPD/init/02_app_context.sql` dưới schema owner của `CUSTOMER` (nếu chưa có).
3) Chạy `SQL/VPD/Customer/01_customer_vpd_function.sql` dưới schema owner của `CUSTOMER`.
4) Chạy `SQL/VPD/Customer/02_customer_vpd_add_policy.sql` dưới schema owner của `CUSTOMER`.

### Cách sử dụng trong session (test nhanh)
Thiết lập context trong CÙNG session trước khi query:

```sql
-- Xem nhanh context hiện tại
SELECT SYS_CONTEXT('APP_CTX','ROLE_NAME') AS ROLE_NAME,
       SYS_CONTEXT('APP_CTX','CUSTOMER_PHONE') AS CUSTOMER_PHONE
FROM dual;

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
```

### Tích hợp với ứng dụng
- Sau khi xác thực, ứng dụng cần gọi tương đương:
  - `APP_CTX_PKG.set_role('<ROLE_NAME>')` theo vai trò hiện tại của user.
  - Nếu là khách hàng: `APP_CTX_PKG.set_customer('<CUSTOMER_PHONE>')`.
- Các lời gọi này phải diễn ra trong cùng session DB dùng để query.

### Troubleshooting
- **COUNT = 0 dù là Admin/Tieptan**:
  - Chưa set context trong cùng session, hoặc policy gắn nhầm schema.
  - Kiểm tra:  
    `SELECT CUSTOMER_VPD_PREDICATE(USER,'CUSTOMER') FROM dual;`  
    `SELECT object_owner, object_name, policy_name FROM user_policies WHERE object_name='CUSTOMER';`
- **ORA-00942 (table or view does not exist)**: chạy script dưới schema không phải owner `CUSTOMER`.
- **ORA-01031 (insufficient privileges)**: thiếu quyền tạo context/package/policy. Chạy bằng schema owner có quyền cần thiết.
- **ORA-40442/DBMS_RLS errors**: xem lại tham số `object_schema`, `function_schema`. Thay `USER` bằng tên schema tường minh nếu cần.
- **KHACHHANG không thấy dữ liệu**: kiểm tra đã set `CUSTOMER_PHONE` trong context chưa, và cột `PHONE` trên bảng `CUSTOMER` có đúng tên không.

### Vô hiệu hóa / Gỡ policy
```sql
-- Tạm tắt policy
BEGIN
  DBMS_RLS.ENABLE_POLICY(
    object_schema => '<OWNER_SCHEMA>',
    object_name   => 'CUSTOMER',
    policy_name   => 'CUSTOMER_VPD',
    enable        => FALSE
  );
END;
/

-- Bật lại policy
BEGIN
  DBMS_RLS.ENABLE_POLICY(
    object_schema => '<OWNER_SCHEMA>',
    object_name   => 'CUSTOMER',
    policy_name   => 'CUSTOMER_VPD',
    enable        => TRUE
  );
END;
/

-- Gỡ hẳn policy
BEGIN
  DBMS_RLS.DROP_POLICY(
    object_schema => '<OWNER_SCHEMA>',
    object_name   => 'CUSTOMER',
    policy_name   => 'CUSTOMER_VPD'
  );
END;
/
```

### Ghi chú bảo mật
- Không xây predicate dựa trên DB role đang bật; dùng Application Context để an toàn và linh hoạt.
- Escape giá trị chuỗi khi build predicate (đã dùng `REPLACE(..., '''', '''''')`).
- Chỉ expose các thủ tục context cần thiết; không để lộ thông tin nhạy cảm qua context.
- Đảm bảo cột `PHONE` trên bảng `CUSTOMER` đúng tên (nếu khác, cần chỉnh hàm predicate).

### (Tùy chọn) OLS
- Có thể kết hợp OLS (SA policy) để phân tầng nhãn truy cập kết hợp với VPD theo vai trò. Cần quyền LBACSYS và cấu hình riêng.
