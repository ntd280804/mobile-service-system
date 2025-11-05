## INVOICE VPD Policy (Row-Level Security)

Roles:
- Admin, Tieptan: full
- KhachHang: only own rows (by CUSTOMER_PHONE)
- Thukho, Kithuatvien: no access

Files:
- SQL/VPD/Invoice/01_invoice_vpd_function.sql
- SQL/VPD/Invoice/02_invoice_vpd_add_policy.sql
- SQL/VPD/Invoice/03_invoice_vpd_tests.sql

Note: Adjust column name if not `CUSTOMER_PHONE`.


