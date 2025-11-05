-- Tạo role cho các loại người dùng
CREATE ROLE role_admin;
CREATE ROLE role_nvcb;
CREATE ROLE role_gv;
CREATE ROLE role_nvctsv;
CREATE ROLE role_nvpdt;
CREATE ROLE role_nvpkt;
CREATE ROLE role_tchc;
CREATE ROLE role_trgdv;
CREATE ROLE role_sv;

-- Cấp quyền cơ bản cho các role
GRANT CREATE SESSION, RESTRICTED SESSION TO role_sv, role_nvpdt, role_nvpkt, role_gv, role_admin, role_trgdv, role_nvctsv, role_tchc, role_nvcb;

-- Cấp quyền cho role admin
GRANT ALL PRIVILEGES ON ADMIN.DANGKY TO role_admin;

-- Cấp quyền xem thông báo cho tất cả các role
GRANT SELECT ON ADMIN.THONGBAO TO role_nvcb, role_sv;

-- Cấp quyền cho sinh viên
GRANT SELECT, INSERT, UPDATE, DELETE ON ADMIN.DANGKY TO role_sv;
GRANT SELECT, UPDATE ON ADMIN.SINHVIEN TO role_sv;
GRANT SELECT ON ADMIN.V_MOMON_SV TO role_sv;

-- Cấp quyền cho nhân viên cơ bản
GRANT SELECT, UPDATE(DT) ON ADMIN.V_NVCB TO role_nvcb;

-- Cấp quyền cho giảng viên
GRANT SELECT ON ADMIN.DANGKY TO role_gv;
GRANT SELECT ON ADMIN.SINHVIEN TO role_gv;
GRANT SELECT ON ADMIN.V_MOMON_GV TO role_gv;

-- Cấp quyền cho nhân viên công tác sinh viên
GRANT SELECT ON ADMIN.DONVI TO role_nvctsv;
GRANT SELECT, INSERT, UPDATE, DELETE ON ADMIN.SINHVIEN TO role_nvctsv;

-- Cấp quyền cho nhân viên tổ chức hành chính
GRANT SELECT, INSERT, UPDATE, DELETE ON ADMIN.NHANVIEN TO role_tchc;

-- Cấp quyền cho trưởng đơn vị
GRANT SELECT ON ADMIN.V_MOMON_TRGDV TO role_trgdv;
GRANT SELECT ON ADMIN.V_NHANVIEN_TRGDV TO role_trgdv;

-- Cấp quyền cho nhân viên phòng khảo thí
GRANT SELECT, UPDATE ON ADMIN.DANGKY TO role_nvpkt;

-- Cấp quyền cho nhân viên phòng đào tạo
GRANT SELECT, UPDATE ON ADMIN.SINHVIEN TO role_nvpdt;
GRANT SELECT, UPDATE, INSERT, DELETE ON ADMIN.DANGKY TO role_nvpdt;
GRANT SELECT, INSERT, UPDATE, DELETE ON ADMIN.V_MOMON_NVPDT TO role_nvpdt;