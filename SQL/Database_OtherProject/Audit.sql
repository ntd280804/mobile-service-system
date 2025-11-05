CREATE OR REPLACE FUNCTION GET_USER_ROLE (
    P_USERNAME IN VARCHAR2
) 
RETURN VARCHAR2 
IS
    PRAGMA AUTONOMOUS_TRANSACTION;
    ROLE_USER VARCHAR2(10);
    v_count NUMBER;
BEGIN
    -- Xác định vai trò người dùng trong bảng NHANVIEN
    BEGIN
        SELECT VAITRO INTO ROLE_USER 
        FROM NHANVIEN
        WHERE MANLD = P_USERNAME;
    EXCEPTION
        WHEN NO_DATA_FOUND THEN
            -- Nếu không tìm thấy trong bảng NHANVIEN, kiểm tra trong bảng SINHVIEN
            SELECT COUNT(*) INTO v_count FROM SINHVIEN WHERE MASV = P_USERNAME;
            IF v_count > 0 THEN
                ROLE_USER := 'SV';  -- Nếu là sinh viên
            ELSE
                ROLE_USER := 'OTHER'; -- Nếu không phải là sinh viên hoặc nhân viên
            END IF;
    END;
    
    RETURN ROLE_USER;
END;
/


-- 3.a Hành vi cập nhật quan hệ ĐANGKY tại các trường liên quan đến điểm số nhưng người đó không thuộc vai trò “NV PKT”.
BEGIN
    DBMS_FGA.ADD_POLICY(
        object_schema      => 'ADMIN',
        object_name        => 'DANGKY',
        policy_name        => 'FGA_UPDATE_DIEM_NON_NVPKT',
        audit_column       => 'DIEMTH,DIEMQT,DIEMCK,DIEMTK',
        audit_condition    => 'GET_USER_ROLE(SYS_CONTEXT(''USERENV'', ''SESSION_USER'')) != ''NVPKT''',
        handler_schema     => 'ADMIN',
        handler_module     => 'FGA_SEND_MAIL',
        enable             => TRUE,
        statement_types    => 'UPDATE',
        audit_trail        => DBMS_FGA.DB + DBMS_FGA.EXTENDED,
        audit_column_opts  => DBMS_FGA.ANY_COLUMNS
    );
END;
/


-- 3.b Hành vi của người dùng (không thuộc vai trò “NV TCHC”) có thể đọc trên trường LUONG, PHUCAP của người khác hoặc cập nhật ở quan hệ NHANVIEN.
BEGIN
    DBMS_FGA.ADD_POLICY(
        object_schema      => 'ADMIN',
        object_name        => 'NHANVIEN',
        policy_name        => 'FGA_SELECT_LUONG_PHUCAP_NON_NVTCHC',
        audit_column       => 'LUONG,PHUCAP',
        audit_condition    => 'GET_USER_ROLE(SYS_CONTEXT(''USERENV'', ''SESSION_USER'')) != ''NVTCHC''' || 'AND SYS_CONTEXT(''USERENV'', ''SESSION_USER'') != MANLD',
        handler_schema     => 'ADMIN',
        handler_module     => 'FGA_SEND_MAIL',
        statement_types    => 'SELECT',
        audit_trail        => DBMS_FGA.DB + DBMS_FGA.EXTENDED,
        audit_column_opts  => DBMS_FGA.ANY_COLUMNS
    );
END;
/
BEGIN
    DBMS_FGA.ADD_POLICY(
        object_schema      => 'ADMIN',
        object_name        => 'NHANVIEN',
        policy_name        => 'FGA_UPDATE_NHANVIEN_NON_NVTCHC',
        audit_condition    => 'GET_USER_ROLE(SYS_CONTEXT(''USERENV'', ''SESSION_USER'')) != ''NVTCHC''',
        handler_schema     => 'ADMIN',
        handler_module     => 'FGA_SEND_MAIL',
        statement_types    => 'UPDATE',
        audit_trail        => DBMS_FGA.DB + DBMS_FGA.EXTENDED,
        audit_column_opts  => DBMS_FGA.ANY_COLUMNS
    );
END;
/
-- 3.c Hành vi thêm, xóa, sửa trên quan hệ DANGKY của sinh viên nhưng trên dòng dữ liệu của sinh viên khác hoặc thực hiện hiệu chỉnh đăng ký học phần ngoài thời gian cho phép hiệu chỉnh đăng ký học phần.
BEGIN    
     DBMS_FGA.ADD_POLICY(
        object_schema      => 'ADMIN',
        object_name        => 'DANGKY',
        policy_name        => 'FGA_INSERT_DELETE_UPDATE_DANGKY',
        audit_condition    => 'MASV != SYS_CONTEXT(''USERENV'', ''SESSION_USER'')',
        handler_schema     => 'ADMIN',
        handler_module     => 'FGA_SEND_MAIL',
        enable             => TRUE,
        statement_types    => 'INSERT,DELETE,UPDATE',
        audit_trail        => DBMS_FGA.DB + DBMS_FGA.EXTENDED,
        audit_column_opts  => DBMS_FGA.ANY_COLUMNS
    );
END;
/

BEGIN    
     DBMS_FGA.ADD_POLICY(
        object_schema      => 'ADMIN',
        object_name        => 'DANGKY',
        policy_name        => 'FGA_INSERT_DELETE_UPDATE_DANGKY_NO_IS_14DAYS',
        audit_condition    => 'IS_14DAYS = 0',
        handler_schema     => 'ADMIN',
        handler_module     => 'FGA_SEND_MAIL',
        enable             => TRUE,
        statement_types    => 'INSERT,DELETE,UPDATE',
        audit_trail        => DBMS_FGA.DB + DBMS_FGA.EXTENDED,
        audit_column_opts  => DBMS_FGA.ANY_COLUMNS
    );
END;
/

CREATE OR REPLACE PROCEDURE FGA_SEND_MAIL (
    p_object_schema VARCHAR2,
    p_object_name VARCHAR2,
    p_policy_name VARCHAR2
) AS
BEGIN
    DBMS_OUTPUT.PUT_LINE('CẢNH BÁO FGA: Có truy vấn xem dữ liệu nhạy cảm!');
END;
/

-- ==================================
-- BEGIN
--     DBMS_RLS.DROP_POLICY(
--         OBJECT_SCHEMA => 'ADMIN',
--         OBJECT_NAME => 'DANGKY',
--         POLICY_NAME => 'DANGKY_EXCEPT_SINHVIEN_POLICY'
--     );
-- END;
-- /
-- BEGIN 
--     DBMS_RLS.DROP_POLICY(
--         OBJECT_SCHEMA => 'ADMIN',
--         OBJECT_NAME => 'DANGKY',
--         POLICY_NAME => 'DANGKY_SELECT_SINHVIEN_POLICY'
--     );
-- END;
-- /
-- BEGIN
--     DBMS_RLS.DROP_POLICY(
--         OBJECT_SCHEMA => 'ADMIN',
--         OBJECT_NAME => 'DANGKY',
--         POLICY_NAME => 'DANGKY_DML_SINHVIEN_POLICY'
--     );
-- END;
-- /

-- DROP TRIGGER TRG_DANGKY_UPDATE_DIEM;

     
-- -- AUDIT 1
-- UPDATE ADMIN.DANGKY
-- SET DIEMTH = 10
-- WHERE MASV = 'SV0001' AND MAMM = 'MM101';


-- -- AUDIT 2
-- SELECT MANLD, LUONG, PHUCAP FROM ADMIN.NHANVIEN;

-- UPDATE ADMIN.NHANVIEN
-- SET HOTEN = 'Nguyen Van A'
-- WHERE MANLD = 'NV001';


-- -- Audit 3 Sinh Viên
-- CONNECT SV0001/SV0001;
-- UPDATE ADMIN.DANGKY 
-- SET MAMM = 'MM100' 
-- WHERE MASV = 'SV0003' AND MAMM = 'MM003';

-- -- -- Audit Test
-- SELECT 

--     timestamp, db_user, sql_text, object_name, policy_name 
-- FROM 
--      DBA_FGA_AUDIT_TRAIL 
-- WHERE 
--     policy_name IN (
--     'FGA_UPDATE_DIEM_NON_NVPKT', 
--     'FGA_SELECT_LUONG_PHUCAP_NON_NVTCHC',
--     'FGA_UPDATE_NHANVIEN_NON_NVTCHC',
--     'FGA_INSERT_DELETE_UPDATE_DANGKY', 
--     'FGA_INSERT_DELETE_UPDATE_DANGKY_NO_IS_14DAYS');





