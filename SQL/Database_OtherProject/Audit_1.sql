-- Audit 1. 2. 
SELECT name, value
FROM v$parameter
WHERE name = 'audit_trail';

-- Nếu audit_trail = none thì chỉ cần chạy các câu lệnh sau
-- ALTER SYSTEM SET audit_trail = 'DB, EXTENDED' SCOPE=SPFILE;
-- Sau đó restart lại database Với quyền SYSDBA
-- SHUTDOWN IMMEDIATE;
-- STARTUP;

-- ======================================================================================
-- Ngữ cảnh 1: Sinh viên cố gắng cập nhật tình trạng học vụ (TINHTRANG) của chính mình
-- ======================================================================================
AUDIT UPDATE ON SINHVIEN 
    BY ACCESS
    WHENEVER NOT SUCCESSFUL;

-- Connect as sinh viên
CONNECT SV0001/SV0001;

-- Thử sửa tình trạng học vụ trái phép
UPDATE Admin.SINHVIEN
SET TINHTRANG = N'Đang học lại'
WHERE MASV = 'SV0001';

-- Xem audit trail
-- SELECT username, obj_name, action_name, sql_text, returncode, timestamp
-- FROM dba_audit_trail
-- WHERE obj_name = 'SINHVIEN'
--   AND action_name = 'UPDATE'
--   AND timestamp > SYSDATE - 1;

-- ======================================================================================
-- Ngữ cảnh 2: Giảng viên cố gắng cập nhật điểm sinh viên trong bảng ĐANGKY
-- ======================================================================================
AUDIT UPDATE ON Admin.DANGKY 
    BY ACCESS
    WHENEVER NOT SUCCESSFUL;

CONNECT NV064/NV064;

-- Thử cập nhật điểm trái phép
UPDATE
WHERE MASV = 'SV0001' AND MAMM = 'MMM101'; DANGKY
SET DIEMQT = 7

-- Xem audit trail
-- SELECT username, obj_name, action_name, sql_text, returncode, timestamp
-- FROM dba_audit_trail
-- WHERE obj_name = 'SINHVIEN'
--   AND action_name = 'SELECT'
--   AND timestamp > SYSDATE - 1;

-- ======================================================================================
-- Ngữ cảnh 3: Nhân viên thuộc vai trò NVCB truy cập trái phép dữ liệu NHANVIEN của người khác
-- ======================================================================================
AUDIT SELECT, UPDATE ON Admin.NHANVIEN 
    BY ACCESS
    WHENEVER NOT SUCCESSFUL;

-- Connect as nhân viên cơ bản
CONNECT NV246/NV246;

-- Thử truy cập thông tin người khác
SELECT * FROM NHANVIEN WHERE MANLD = 'NV065';

-- Hoặc thử cập nhật
UPDATE NHANVIEN
SET ĐT = '0909999999'
WHERE MANLD = 'NV065';

-- Xem audit trail
-- SELECT username, obj_name, action_name, sql_text, returncode, timestamp
-- FROM dba_audit_trail
-- WHERE obj_name = 'DANGKY'
--   AND action_name = 'UPDATE'
--   AND timestamp > SYSDATE - 1;

-- ======================================================================================
-- Ngữ cảnh 4: Nhân viên NV PĐT cố tình cập nhật điểm trong bảng ĐANGKY ngoài thời gian 14 ngày đầu học kỳ
-- ======================================================================================
AUDIT UPDATE ON Admin.DANGKY 
    BY ACCESS
    WHENEVER NOT SUCCESSFUL;

UPDATE Admin.DANGKY
SET IS_14DAYS = 0
WHERE MASV = 'SV0001' AND MAMM = 'MM101';

-- Connect as NV_PDT user
CONNECT NV224/NV224;

-- Cố gắng cập nhật điểm sau 14 ngày (giả định đã hết thời gian cho phép)
UPDATE Admin.DANGKY
SET DIEMQT = 8.5
WHERE MASV = 'SV0001' AND MAMM = 'MMM101';

-- Xem audit trail
-- SELECT username, sql_text, obj_name, action_name, returncode, timestamp
-- FROM dba_audit_trail
-- WHERE obj_name = 'DANGKY'
--   AND action_name = 'UPDATE'
--   AND timestamp > SYSDATE - 1;


-- ======================================================================================
-- Ngữ cảnh 5: Giảng viên truy cập thông tin sinh viên ngoài khoa mình
-- ======================================================================================
AUDIT SELECT ON Admin.SINHVIEN 
    BY ACCESS
    WHENEVER NOT SUCCESSFUL;

-- Connect as GV user from Khoa CNTT
CONNECT NV064/NV064;

-- Truy cập sinh viên Khoa Hóa (trái quyền)
SELECT * FROM Admin.SINHVIEN WHERE KHOA = 'HOA_CS2';

-- Xem audit trail
-- SELECT username, sql_text, obj_name, action_name, returncode, timestamp
-- FROM dba_audit_trail
-- WHERE obj_name = 'SINHVIEN'
--   AND action_name = 'SELECT'
--   AND timestamp > SYSDATE - 1;



-- 4.
-- SELECT
--   USERNAME,
--   ACTION_NAME,
--   OBJ_NAME,
--   TO_CHAR(TIMESTAMP,'DD/MM/YYYY HH24:MI:SS') AS AUDIT_TIME,
--   RETURNCODE,  -- Changed from RETURN_CODE to RETURNCODE
--   OS_USERNAME,
--   TERMINAL
-- FROM
--   DBA_AUDIT_TRAIL
-- ORDER BY TIMESTAMP DESC;