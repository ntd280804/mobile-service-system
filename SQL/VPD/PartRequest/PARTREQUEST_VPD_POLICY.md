## PART_REQUEST VPD Policy

Roles:
- Admin, Thukho: full
- Kithuatvien: only own (`REQUEST_EMP_ID = :EMP_ID`)
- Tieptan, KhachHang: no access

Files:
- SQL/VPD/PartRequest/01_partrequest_vpd_function.sql
- SQL/VPD/PartRequest/02_partrequest_vpd_add_policy.sql
- SQL/VPD/PartRequest/03_partrequest_vpd_tests.sql

Note: Adjust owner column if different from `REQUEST_EMP_ID`.


