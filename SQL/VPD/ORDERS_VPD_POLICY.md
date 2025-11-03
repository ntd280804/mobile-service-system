## ORDERS VPD Policy (Row-Level Security)

### Mục tiêu
- Áp dụng VPD (DBMS_RLS) để kiểm soát quyền xem dữ liệu theo dòng trên bảng `ORDERS`.
- Quy định theo vai trò người dùng trong ứng dụng (Application Context), không phụ thuộc DB role đang bật.

### Phạm vi áp dụng
- Bảng: `ORDERS`
- Loại câu lệnh: `SELECT`

### Vai trò và predicate
- **Admin**: xem tất cả (predicate: `1=1`)
- **Tieptan**: xem tất cả (predicate: `1=1`)
- **Kithuatvien**: chỉ xem các dòng có `HANDLER_EMP = :EMP_ID` (đặt qua context)
- **KhachHang**: chỉ xem các dòng có `CUSTOMER_PHONE = :CUSTOMER_PHONE` (đặt qua context)
- **Thukho**: không xem được (predicate: `1=0`)

### Thành phần tạo ra
- Application Context: `APP_CTX`
- Package context: `APP_CTX_PKG` (thủ tục `set_role`, `set_emp`, `set_customer`)
- VPD function: `ORDERS_VPD_PREDICATE(schema, object) return varchar2`
- VPD policy: `ORDERS_VPD` gắn trên `ORDERS`

### File liên quan
1. `SQL/VPD/01_roles_users_demo.sql` – Tạo role và user demo (tùy chọn cho test), grant `SELECT` trên `ORDERS` cho role.
2. `SQL/VPD/02_app_context.sql` – Tạo `APP_CTX` và package `APP_CTX_PKG`.
3. `SQL/VPD/03_orders_vpd_function.sql` – Hàm `ORDERS_VPD_PREDICATE` sinh predicate theo context.
4. `SQL/VPD/04_orders_vpd_add_policy.sql` – Gắn policy `ORDERS_VPD` vào bảng `ORDERS`.
5. `SQL/VPD/05_orders_vpd_tests.sql` - hàm test

> Lưu ý: Chạy các file (2→3→4) dưới schema OWNER thật sự của bảng `ORDERS`.

### Trình tự cài đặt (install)
1) Đảm bảo bảng `ORDERS` đã tồn tại và có dữ liệu.
2) Chạy `02_app_context.sql` dưới schema owner của `ORDERS`.
3) Chạy `03_orders_vpd_function.sql` dưới schema owner của `ORDERS`.
4) Chạy `04_orders_vpd_add_policy.sql` dưới schema owner của `ORDERS`.
5) (Tùy chọn) Chạy `01_roles_users_demo.sql` nếu cần tạo user/role demo để thử nhanh.

### Cách sử dụng trong session (test nhanh)
Thiết lập context trong CÙNG session trước khi query:

```sql
-- Xem nhanh context hiện tại
SELECT SYS_CONTEXT('APP_CTX','ROLE_NAME') AS ROLE_NAME,
       SYS_CONTEXT('APP_CTX','EMP_ID') AS EMP_ID,
       SYS_CONTEXT('APP_CTX','CUSTOMER_PHONE') AS CUSTOMER_PHONE
FROM dual;

-- Admin: xem tất cả
EXEC APP_CTX_PKG.set_role('ROLE_ADMIN');
SELECT COUNT(*) FROM ORDERS;

-- Tieptan: xem tất cả
EXEC APP_CTX_PKG.set_role('ROLE_TIEPTAN');
SELECT COUNT(*) FROM ORDERS;

-- Kithuatvien: chỉ HANDLER_EMP = 101 (ví dụ)
EXEC APP_CTX_PKG.set_role('ROLE_KITHUATVIEN');
EXEC APP_CTX_PKG.set_emp(101);
SELECT ORDER_ID, HANDLER_EMP FROM ORDERS ORDER BY ORDER_ID;

-- KhachHang: chỉ CUSTOMER_PHONE = '0911' (ví dụ)
EXEC APP_CTX_PKG.set_role('ROLE_KHACHHANG');
EXEC APP_CTX_PKG.set_customer('0911');
SELECT ORDER_ID, CUSTOMER_PHONE FROM ORDERS ORDER BY ORDER_ID;

-- Thukho: không xem được (0 dòng)
EXEC APP_CTX_PKG.set_role('ROLE_THUKHO');
SELECT COUNT(*) FROM ORDERS;
```

### Tích hợp với ứng dụng
- Sau khi xác thực, ứng dụng cần gọi tương đương:
  - `APP_CTX_PKG.set_role('<ROLE_NAME>')` theo vai trò hiện tại của user.
  - Nếu là kỹ thuật viên: `APP_CTX_PKG.set_emp(<EMP_ID>)`.
  - Nếu là khách hàng: `APP_CTX_PKG.set_customer('<CUSTOMER_PHONE>')`.
- Các lời gọi này phải diễn ra trong cùng session DB dùng để query.

### Troubleshooting
- **COUNT = 0 dù là Admin/Tieptan**:
  - Chưa set context trong cùng session, hoặc policy gắn nhầm schema.
  - Kiểm tra:  
    `SELECT ORDERS_VPD_PREDICATE(USER,'ORDERS') FROM dual;`  
    `SELECT object_owner, object_name, policy_name FROM user_policies WHERE object_name='ORDERS';`
- **ORA-00942 (table or view does not exist)**: chạy script dưới schema không phải owner `ORDERS`.
- **ORA-01031 (insufficient privileges)**: thiếu quyền tạo context/package/policy. Chạy bằng schema owner có quyền cần thiết.
- **ORA-40442/DBMS_RLS errors**: xem lại tham số `object_schema`, `function_schema`. Thay `USER` bằng tên schema tường minh nếu cần.
- **ORA-00904: ORA_ROWSCN invalid identifier**: không dùng `ORA_ROWSCN` trong test; chỉ query cột có thật hoặc expose qua view.

### Vô hiệu hóa / Gỡ policy
```sql
-- Tạm tắt policy
BEGIN
  DBMS_RLS.ENABLE_POLICY(
    object_schema => '<OWNER_SCHEMA>',
    object_name   => 'ORDERS',
    policy_name   => 'ORDERS_VPD',
    enable        => FALSE
  );
END;
/

-- Bật lại policy
BEGIN
  DBMS_RLS.ENABLE_POLICY(
    object_schema => '<OWNER_SCHEMA>',
    object_name   => 'ORDERS',
    policy_name   => 'ORDERS_VPD',
    enable        => TRUE
  );
END;
/

-- Gỡ hẳn policy
BEGIN
  DBMS_RLS.DROP_POLICY(
    object_schema => '<OWNER_SCHEMA>',
    object_name   => 'ORDERS',
    policy_name   => 'ORDERS_VPD'
  );
END;
/
```

### Ghi chú bảo mật
- Không xây predicate dựa trên DB role đang bật; dùng Application Context để an toàn và linh hoạt.
- Escape giá trị chuỗi khi build predicate (đã dùng `REPLACE(..., '''', '''''')`).
- Chỉ expose các thủ tục context cần thiết; không để lộ thông tin nhạy cảm qua context.

### (Tùy chọn) OLS
- Có thể kết hợp OLS (SA policy) để phân tầng nhãn truy cập kết hợp với VPD theo vai trò. Cần quyền LBACSYS và cấu hình riêng.


