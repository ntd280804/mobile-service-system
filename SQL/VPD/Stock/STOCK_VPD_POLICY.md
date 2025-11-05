## STOCK (IN/OUT) VPD Policy (Row-Level Security)

### Mục tiêu
- Áp dụng VPD (DBMS_RLS) để kiểm soát quyền xem dữ liệu theo dòng trên các bảng kho: `STOCK_IN`, `STOCK_IN_ITEM`, `STOCK_OUT`, `STOCK_OUT_ITEM`.
- Tận dụng Application Context `APP_CTX` đã có: `ROLE_NAME`.

### Phạm vi áp dụng
- Bảng: `STOCK_IN`, `STOCK_IN_ITEM`, `STOCK_OUT`, `STOCK_OUT_ITEM`
- Loại câu lệnh: `SELECT`

### Vai trò và predicate
- **ROLE_ADMIN**: `1=1` (xem tất cả)
- **ROLE_THUKHO**: `1=1` (xem tất cả)
- **ROLE_TIEPTAN**: `1=0` (không xem được)
- **ROLE_KITHUATVIEN**: `1=0` (không xem được)
- **ROLE_KHACHHANG**: `1=0` (không xem được)

### Thành phần cần có
- Application Context: `APP_CTX` (đã tồn tại – xem `SQL/VPD/init/02_app_context.sql`).
- VPD function: `STOCK_VPD_PREDICATE(schema, object) return varchar2` (file: `SQL/VPD/Stock/01_stock_vpd_function.sql`).
- VPD policies: gắn `STOCK_VPD_PREDICATE` cho 4 bảng (file: `SQL/VPD/Stock/02_stock_vpd_add_policy.sql`).

### File liên quan
1. `SQL/VPD/init/01_roles_users_demo.sql` – Tạo role và user demo (tùy chọn cho test).
2. `SQL/VPD/init/02_app_context.sql` – Tạo `APP_CTX` và package `APP_CTX_PKG` (đã có sẵn).
3. `SQL/VPD/Stock/01_stock_vpd_function.sql` – Hàm predicate dùng chung.
4. `SQL/VPD/Stock/02_stock_vpd_add_policy.sql` – Gắn policy SELECT cho 4 bảng.
5. `SQL/VPD/Stock/03_stock_vpd_tests.sql` – Script test role.

> Lưu ý: Chạy các file (2→3→4) dưới schema OWNER thật sự của các bảng kho.

### Trình tự cài đặt (install)
1) Chắc chắn các bảng `STOCK_IN`, `STOCK_IN_ITEM`, `STOCK_OUT`, `STOCK_OUT_ITEM` tồn tại.
2) Chạy `SQL/VPD/init/02_app_context.sql` dưới schema owner (nếu chưa có).
3) Chạy `SQL/VPD/Stock/01_stock_vpd_function.sql` dưới schema owner.
4) Chạy `SQL/VPD/Stock/02_stock_vpd_add_policy.sql` dưới schema owner.

### Cách sử dụng trong session (test nhanh)
Thiết lập context trong CÙNG session trước khi query:

```sql
-- Admin xem full
EXEC APP_CTX_PKG.set_role('ROLE_ADMIN');
SELECT COUNT(*) FROM STOCK_IN;
SELECT COUNT(*) FROM STOCK_OUT;

-- Thủ kho xem full
EXEC APP_CTX_PKG.set_role('ROLE_THUKHO');
SELECT COUNT(*) FROM STOCK_IN_ITEM;
SELECT COUNT(*) FROM STOCK_OUT_ITEM;

-- Các role khác không thấy dữ liệu
EXEC APP_CTX_PKG.set_role('ROLE_TIEPTAN');
SELECT COUNT(*) FROM STOCK_IN; -- 0
EXEC APP_CTX_PKG.set_role('ROLE_KITHUATVIEN');
SELECT COUNT(*) FROM STOCK_OUT; -- 0
EXEC APP_CTX_PKG.set_role('ROLE_KHACHHANG');
SELECT COUNT(*) FROM STOCK_IN_ITEM; -- 0
```

### Troubleshooting
- **COUNT = 0 dù là Admin/Thukho**:
  - Chưa set context trong cùng session, hoặc policy gắn nhầm schema.
  - Kiểm tra:  
    `SELECT STOCK_VPD_PREDICATE(USER,'STOCK_IN') FROM dual;`  
    `SELECT object_owner, object_name, policy_name FROM user_policies WHERE object_name IN ('STOCK_IN','STOCK_IN_ITEM','STOCK_OUT','STOCK_OUT_ITEM');`
- **ORA-00942 / 01031**: chạy script dưới schema owner và bảo đảm quyền tạo policy.
- **ORA-40442/DBMS_RLS errors**: xem lại `object_schema`, `function_schema`.

### Vô hiệu hóa / Gỡ policy
```sql
BEGIN
  DBMS_RLS.ENABLE_POLICY(object_schema => '<OWNER>', object_name => 'STOCK_IN', policy_name => 'STOCK_IN_VPD', enable => FALSE);
  DBMS_RLS.ENABLE_POLICY(object_schema => '<OWNER>', object_name => 'STOCK_IN_ITEM', policy_name => 'STOCK_IN_ITEM_VPD', enable => FALSE);
  DBMS_RLS.ENABLE_POLICY(object_schema => '<OWNER>', object_name => 'STOCK_OUT', policy_name => 'STOCK_OUT_VPD', enable => FALSE);
  DBMS_RLS.ENABLE_POLICY(object_schema => '<OWNER>', object_name => 'STOCK_OUT_ITEM', policy_name => 'STOCK_OUT_ITEM_VPD', enable => FALSE);
END;
/
```


