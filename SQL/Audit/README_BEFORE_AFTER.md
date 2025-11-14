# Audit với Before/After Values

## Tổng quan

Để capture được giá trị **before** và **after** khi INSERT/UPDATE, cần sử dụng **triggers** thay vì chỉ dùng FGA policies.

## Cài đặt

### Bước 1: Cập nhật audit table và package
```sql
@SQL/Audit/init/04_audit_with_before_after.sql
```

Script này sẽ:
- Thêm các cột `DML_TYPE`, `OLD_VALUES`, `NEW_VALUES`, `CHANGED_COLUMNS` vào `audit_alert_log`
- Tạo package `audit_dml_pkg` để log với before/after values

### Bước 2: Tạo triggers cho tất cả các bảng

Chạy script master để tạo tất cả triggers:
```sql
@SQL/Audit/init/08_create_all_audit_triggers.sql
```

Hoặc tạo từng trigger riêng lẻ:
```sql
@SQL/Audit/Employee/03_employee_audit_trigger.sql
@SQL/Audit/Customer/03_customer_audit_trigger.sql
@SQL/Audit/Stock/03_stock_in_audit_trigger.sql
-- ... và các trigger khác
```

## Cấu trúc dữ liệu

### audit_alert_log table
- `OLD_VALUES`: CLOB chứa JSON của giá trị cũ (cho UPDATE/DELETE)
- `NEW_VALUES`: CLOB chứa JSON của giá trị mới (cho INSERT/UPDATE)
- `CHANGED_COLUMNS`: Danh sách các cột bị thay đổi (cho UPDATE)
- `DML_TYPE`: 'INSERT', 'UPDATE', hoặc 'DELETE'

### Format JSON
```json
{
  "EMP_ID": 1,
  "FULL_NAME": "Nguyen Van A",
  "USERNAME": "user1",
  "EMAIL": "user1@example.com",
  "PHONE": "0123456789",
  "CREATED_AT": "2025-01-15 10:30:00"
}
```

## Xem audit log với before/after

```sql
SELECT 
  log_id,
  event_ts,
  object_name,
  dml_type,
  changed_columns,
  old_values,
  new_values,
  app_role,
  emp_id
FROM audit_alert_log
WHERE object_name = 'EMPLOYEE'
ORDER BY event_ts DESC;
```

## So sánh FGA vs Triggers

| Tính năng | FGA Policies | Triggers |
|-----------|--------------|----------|
| Capture sự kiện | ✅ | ✅ |
| Capture before values | ❌ | ✅ |
| Capture after values | ❌ | ✅ |
| Track changed columns | ❌ | ✅ |
| Performance | Tốt hơn | Chậm hơn một chút |
| Dễ maintain | Dễ | Phức tạp hơn |

## Khuyến nghị

- **FGA Policies**: Dùng để audit **SELECT** operations (nếu cần)
- **Triggers**: Dùng để audit **INSERT/UPDATE/DELETE** với before/after values

Hoặc có thể dùng **cả hai**:
- FGA để detect và log sự kiện
- Triggers để capture chi tiết before/after values

## Danh sách triggers đã tạo

Các triggers đã được tạo cho các bảng sau:
- ✅ `EMPLOYEE` - `03_employee_audit_trigger.sql`
- ✅ `CUSTOMER` - `03_customer_audit_trigger.sql`
- ✅ `STOCK_IN` - `03_stock_in_audit_trigger.sql`
- ✅ `STOCK_IN_ITEM` - `04_stock_in_item_audit_trigger.sql`
- ✅ `STOCK_OUT` - `05_stock_out_audit_trigger.sql`
- ✅ `STOCK_OUT_ITEM` - `06_stock_out_item_audit_trigger.sql`
- ✅ `PART` - `03_part_audit_trigger.sql`
- ✅ `PART_REQUEST` - `03_partrequest_audit_trigger.sql`
- ✅ `PART_REQUEST_ITEM` - `04_partrequest_item_audit_trigger.sql`
- ✅ `INVOICE` - `03_invoice_audit_trigger.sql`
- ✅ `INVOICE_ITEM` - `04_invoice_item_audit_trigger.sql`
- ✅ `ORDERS` - `03_order_audit_trigger.sql`
- ✅ `CUSTOMER_APPOINTMENT` - `03_appointment_audit_trigger.sql`

## Tạo triggers cho các bảng khác

Mẫu trigger cho bảng mới:

```sql
CREATE OR REPLACE TRIGGER trg_audit_TABLE_NAME
  AFTER INSERT OR UPDATE OR DELETE ON TABLE_NAME
  FOR EACH ROW
DECLARE
  v_old_values CLOB;
  v_new_values CLOB;
  v_changed_cols VARCHAR2(4000);
BEGIN
  -- Build old_values
  IF UPDATING OR DELETING THEN
    v_old_values := '{' ||
      '"COL1":"' || REPLACE(NVL(:OLD.COL1, ''), '"', '\"') || '",' ||
      '"COL2":"' || REPLACE(NVL(:OLD.COL2, ''), '"', '\"') || '"' ||
      '}';
  END IF;
  
  -- Build new_values
  IF INSERTING OR UPDATING THEN
    v_new_values := '{' ||
      '"COL1":"' || REPLACE(NVL(:NEW.COL1, ''), '"', '\"') || '",' ||
      '"COL2":"' || REPLACE(NVL(:NEW.COL2, ''), '"', '\"') || '"' ||
      '}';
  END IF;
  
  -- Track changed columns
  IF UPDATING THEN
    v_changed_cols := '';
    IF (:OLD.COL1 != :NEW.COL1) THEN v_changed_cols := v_changed_cols || 'COL1,'; END IF;
    IF (:OLD.COL2 != :NEW.COL2) THEN v_changed_cols := v_changed_cols || 'COL2,'; END IF;
    IF LENGTH(v_changed_cols) > 0 THEN
      v_changed_cols := SUBSTR(v_changed_cols, 1, LENGTH(v_changed_cols) - 1);
    END IF;
  END IF;
  
  -- Log
  IF INSERTING THEN
    audit_dml_pkg.log_insert('TABLE_NAME', v_new_values);
  ELSIF UPDATING THEN
    audit_dml_pkg.log_update('TABLE_NAME', v_old_values, v_new_values, v_changed_cols);
  ELSIF DELETING THEN
    audit_dml_pkg.log_delete('TABLE_NAME', v_old_values);
  END IF;
END;
/
```

