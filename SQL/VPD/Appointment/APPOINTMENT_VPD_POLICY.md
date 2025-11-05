## CUSTOMER_APPOINTMENT VPD Policy (Row-Level Security)

### Mục tiêu
- Áp dụng VPD (DBMS_RLS) để kiểm soát quyền xem dữ liệu theo dòng trên bảng `CUSTOMER_APPOINTMENT`.
- Tận dụng Application Context `APP_CTX` đã có: `ROLE_NAME`, `CUSTOMER_PHONE`.

### Phạm vi áp dụng
- Bảng: `CUSTOMER_APPOINTMENT`
- Loại câu lệnh: `SELECT`

### Vai trò và predicate
- **ROLE_ADMIN**: `1=1` (xem tất cả)
- **ROLE_TIEPTAN**: `1=1` (xem tất cả)
- **ROLE_THUKHO**: `1=0` (không xem được)
- **ROLE_KITHUATVIEN**: `1=0` (không xem được)
- **ROLE_KHACHHANG**: `CUSTOMER_PHONE = :CUSTOMER_PHONE`

Ngoài ra (DELETE): Không ROLE nào được phép DELETE (chặn tuyệt đối).

### Thành phần cần có
- Application Context: `APP_CTX` (đã tồn tại – xem `SQL/VPD/init/02_app_context.sql`).
- VPD function: `APPOINTMENT_VPD_PREDICATE(schema, object) return varchar2` (file: `SQL/VPD/Appointment/01_appointment_vpd_function.sql`).
- VPD policy: `APPOINTMENT_VPD` gắn trên `CUSTOMER_APPOINTMENT` (file: `SQL/VPD/Appointment/02_appointment_vpd_add_policy.sql`).
- VPD function (DELETE): `APPOINTMENT_VPD_PREDICATE_DEL(schema, object) return varchar2` (file: `SQL/VPD/Appointment/03_appointment_vpd_function_delete.sql`).
- VPD policy (DELETE): `APPOINTMENT_VPD_DEL` gắn trên `CUSTOMER_APPOINTMENT` cho `DELETE` (file: `SQL/VPD/Appointment/04_appointment_vpd_add_policy_delete.sql`).

### Trình tự cài đặt
1) Đảm bảo bảng `CUSTOMER_APPOINTMENT` tồn tại và có dữ liệu.
2) Chạy `@SQL/VPD/init/02_app_context.sql` dưới schema OWNER của bảng (nếu chưa có).
3) Chạy `@SQL/VPD/Appointment/01_appointment_vpd_function.sql` dưới schema OWNER.
4) Chạy `@SQL/VPD/Appointment/02_appointment_vpd_add_policy.sql` dưới schema OWNER.
5) Chạy `@SQL/VPD/Appointment/03_appointment_vpd_function_delete.sql` dưới schema OWNER.
6) Chạy `@SQL/VPD/Appointment/04_appointment_vpd_add_policy_delete.sql` dưới schema OWNER.

### Script gợi ý

1) Tạo hàm predicate (chạy dưới schema OWNER của bảng)
```sql
CREATE OR REPLACE FUNCTION APPOINTMENT_VPD_PREDICATE(
  p_schema  IN VARCHAR2,
  p_object  IN VARCHAR2
) RETURN VARCHAR2
AS
  v_role VARCHAR2(100) := SYS_CONTEXT('APP_CTX','ROLE_NAME');
  v_cus  VARCHAR2(100) := SYS_CONTEXT('APP_CTX','CUSTOMER_PHONE');
BEGIN
  IF v_role IS NULL THEN
    RETURN '1=0';
  END IF;

  IF v_role IN ('ROLE_ADMIN','ROLE_TIEPTAN') THEN
    RETURN '1=1';
  END IF;

  IF v_role = 'ROLE_KHACHHANG' THEN
    IF v_cus IS NULL THEN
      RETURN '1=0';
    END IF;
    RETURN 'CUSTOMER_PHONE = ''' || REPLACE(v_cus,'''','''''') || '''';
  END IF;

  -- THUKHO, KITHUATVIEN và các role khác: không xem
  RETURN '1=0';
END;
/
```

2) Gắn policy vào bảng (chạy dưới schema OWNER của bảng)
```sql
BEGIN
  DBMS_RLS.ADD_POLICY(
    object_schema   => USER, -- hoặc 'APP' nếu OWNER là APP
    object_name     => 'CUSTOMER_APPOINTMENT',
    policy_name     => 'APPOINTMENT_VPD',
    function_schema => USER, -- hoặc 'APP'
    policy_function => 'APPOINTMENT_VPD_PREDICATE',
    statement_types => 'SELECT',
    enable          => TRUE
  );
END;
/
```

### Cách sử dụng trong session (test nhanh)
- Sau khi login, app đã gọi `APP.APP_CTX_PKG.set_role(...)` và (nếu là khách hàng) `APP.APP_CTX_PKG.set_customer(...)` trên CÙNG session.
- Kiểm tra nhanh:
```sql
SELECT SYS_CONTEXT('APP_CTX','ROLE_NAME') AS ROLE_NAME,
       SYS_CONTEXT('APP_CTX','CUSTOMER_PHONE') AS CUSTOMER_PHONE
FROM dual;

-- Admin / Tieptan: xem tất cả
EXEC APP.APP_CTX_PKG.set_role('ROLE_ADMIN');
SELECT COUNT(*) FROM CUSTOMER_APPOINTMENT;
EXEC APP.APP_CTX_PKG.set_role('ROLE_TIEPTAN');
SELECT COUNT(*) FROM CUSTOMER_APPOINTMENT;

-- Khách hàng: chỉ lịch của chính mình
EXEC APP.APP_CTX_PKG.set_role('ROLE_KHACHHANG');
EXEC APP.APP_CTX_PKG.set_customer('0911');
SELECT APPOINTMENT_ID, CUSTOMER_PHONE FROM CUSTOMER_APPOINTMENT ORDER BY APPOINTMENT_ID;

-- Thủ kho / Kỹ thuật viên: không xem được
EXEC APP.APP_CTX_PKG.set_role('ROLE_THUKHO');
SELECT COUNT(*) FROM CUSTOMER_APPOINTMENT; -- 0
EXEC APP.APP_CTX_PKG.set_role('ROLE_KITHUATVIEN');
SELECT COUNT(*) FROM CUSTOMER_APPOINTMENT; -- 0

-- DELETE test: mọi role đều bị chặn
EXEC APP.APP_CTX_PKG.set_role('ROLE_ADMIN');
DELETE FROM CUSTOMER_APPOINTMENT WHERE 1=1; -- expect ORA-28115 (policy violation)
```

### Tích hợp với ứng dụng
- Khi login:
  - Nhân viên (Admin/Tieptan/Kithuatvien/Thukho): set `ROLE_NAME` theo vai trò chính (đã có sẵn trong luồng login nhân viên).
  - Khách hàng: set `ROLE_NAME = ROLE_KHACHHANG`, và set `CUSTOMER_PHONE` bằng số điện thoại đăng nhập (đã có sẵn trong luồng login khách hàng).
- Các request tiếp theo tái sử dụng cùng Oracle session để policy có hiệu lực.

### Troubleshooting
- Không thấy dữ liệu dù là Admin/Tieptan: kiểm tra context và policy gắn đúng schema chưa:
```sql
SELECT APPOINTMENT_VPD_PREDICATE(USER,'CUSTOMER_APPOINTMENT') FROM dual;
SELECT object_owner, object_name, policy_name FROM user_policies WHERE object_name='CUSTOMER_APPOINTMENT';
```
- ORA-942/01031: chạy script dưới schema owner, đảm bảo quyền `EXECUTE` trên package/context.

### Vô hiệu hóa / Gỡ policy
```sql
BEGIN
  DBMS_RLS.ENABLE_POLICY(object_schema => '<OWNER>', object_name => 'CUSTOMER_APPOINTMENT', policy_name => 'APPOINTMENT_VPD', enable => FALSE);
END;
/
BEGIN
  DBMS_RLS.ENABLE_POLICY(object_schema => '<OWNER>', object_name => 'CUSTOMER_APPOINTMENT', policy_name => 'APPOINTMENT_VPD', enable => TRUE);
END;
/
BEGIN
  DBMS_RLS.DROP_POLICY(object_schema => '<OWNER>', object_name => 'CUSTOMER_APPOINTMENT', policy_name => 'APPOINTMENT_VPD');
END;
/
```


