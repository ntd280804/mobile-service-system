-- Tạo VPD cho bảng DANGKY
CREATE OR REPLACE FUNCTION DANGKY_EXCEPT_SINHVIEN_POLICY(
    P_SCHEMA IN VARCHAR2,
    P_OBJECT IN VARCHAR2
)
RETURN VARCHAR2
AS
    USERNAME   VARCHAR2(30) := SYS_CONTEXT('USERENV', 'SESSION_USER');
    PREFIX     CHAR(2)      := SUBSTR(USERNAME, 1, 2);
    VAITRO     VARCHAR2(10);
    KHOA       VARCHAR2(10);
    PREDICATE  VARCHAR2(2000);
BEGIN
    IF PREFIX = 'NV' THEN
        BEGIN
            SELECT VAITRO, MADV INTO VAITRO, KHOA
            FROM NHANVIEN
            WHERE MANLD = USERNAME;

            IF VAITRO = 'GV' THEN
                PREDICATE := 'MAMM IN (SELECT MAMM FROM MOMON WHERE MAGV = ''' || USERNAME || ''')';
            ELSIF VAITRO = 'NVPDT' THEN
                PREDICATE := 'IS_14DAYS = 1';
            ELSIF VAITRO = 'NVPKT' THEN
                    PREDICATE := 'IS_14DAYS = 0';
            ELSE
                PREDICATE := '1=1';
            END IF;

        EXCEPTION
            WHEN NO_DATA_FOUND THEN
                PREDICATE := '1=1';
        END;

    ELSE
        PREDICATE := '1=1';
    END IF;

    RETURN PREDICATE;
END;
/

CREATE OR REPLACE FUNCTION DANGKY_SELECT_SINHVIEN_POLICY(
    P_SCHEMA IN VARCHAR2,
    P_OBJECT IN VARCHAR2
)
RETURN VARCHAR2
AS
    USERNAME   VARCHAR2(30) := SYS_CONTEXT('USERENV', 'SESSION_USER');
    PREFIX     CHAR(2)      := SUBSTR(USERNAME, 1, 2);
    VAITRO     VARCHAR2(10);
    KHOA       VARCHAR2(10);
    PREDICATE  VARCHAR2(2000);
BEGIN
    IF PREFIX = 'SV' THEN
        PREDICATE := 'MASV = ''' || USERNAME || '''';
    ELSE
        PREDICATE := '1=1';
    END IF;

    RETURN PREDICATE;
END;
/

CREATE OR REPLACE FUNCTION DANGKY_DML_SINHVIEN_POLICY(
    P_SCHEMA IN VARCHAR2,
    P_OBJECT IN VARCHAR2
)
RETURN VARCHAR2
AS
    USERNAME   VARCHAR2(30) := SYS_CONTEXT('USERENV', 'SESSION_USER');
    PREFIX     CHAR(2)      := SUBSTR(USERNAME, 1, 2);
    VAITRO     VARCHAR2(10);
    KHOA       VARCHAR2(10);
    PREDICATE  VARCHAR2(2000);
BEGIN
    IF PREFIX = 'SV' THEN
        PREDICATE := 'IS_14DAYS = 1 AND MASV = ''' || USERNAME || '''';
    ELSE
        PREDICATE := '1=1';
    END IF;

    RETURN PREDICATE;
END;
/
BEGIN
    -- POLICY CHO SELECT
    DBMS_RLS.ADD_POLICY(
        OBJECT_SCHEMA => 'ADMIN',
        OBJECT_NAME => 'DANGKY',
        POLICY_NAME => 'DANGKY_EXCEPT_SINHVIEN_POLICY',
        FUNCTION_SCHEMA => 'ADMIN',
        POLICY_FUNCTION => 'DANGKY_EXCEPT_SINHVIEN_POLICY'
    );

    DBMS_RLS.ADD_POLICY(
        OBJECT_SCHEMA => 'ADMIN',
        OBJECT_NAME => 'DANGKY',
        POLICY_NAME => 'DANGKY_SELECT_SINHVIEN_POLICY',
        FUNCTION_SCHEMA => 'ADMIN',
        POLICY_FUNCTION => 'DANGKY_SELECT_SINHVIEN_POLICY',
        STATEMENT_TYPES => 'SELECT'
    );
    
    -- POLICY CHO INSERT, UPDATE, DELETE
    DBMS_RLS.ADD_POLICY(
        OBJECT_SCHEMA => 'ADMIN',
        OBJECT_NAME => 'DANGKY',
        POLICY_NAME => 'DANGKY_DML_SINHVIEN_POLICY',
        FUNCTION_SCHEMA => 'ADMIN',
        POLICY_FUNCTION => 'DANGKY_DML_SINHVIEN_POLICY',
        STATEMENT_TYPES => 'INSERT,UPDATE,DELETE',
        UPDATE_CHECK => TRUE
    );
END;
/



-- ==================================
-- Trigger cho bảng DANGKY  
CREATE OR REPLACE TRIGGER TRG_DANGKY_UPDATE_DIEM
BEFORE INSERT OR UPDATE OR DELETE ON DANGKY
FOR EACH ROW
DECLARE
    v_username VARCHAR2(30) := SYS_CONTEXT('USERENV', 'SESSION_USER');
    PREFIX     CHAR(2)      := SUBSTR(v_username, 1, 2);
    ROLE_USER  VARCHAR2(10) := 'SV';
    v_max_hk   NUMBER(1);
    v_max_nam  NUMBER(4);
    v_count    NUMBER;
BEGIN
    -- Xác định vai trò người dùng
    IF PREFIX = 'NV' THEN
        BEGIN
            SELECT VAITRO INTO ROLE_USER
            FROM NHANVIEN
            WHERE MANLD = v_username;
        END;
    END IF;

    BEGIN
        SELECT HK, NAM INTO v_max_hk, v_max_nam
        FROM (
        SELECT HK, NAM
        FROM MOMON
        ORDER BY NAM DESC, HK DESC
        )
        WHERE ROWNUM = 1;
    EXCEPTION
        WHEN NO_DATA_FOUND THEN
        v_max_hk := 2;
        v_max_nam := 2024;
    END;

    -- 1. Kiểm tra quyền của Sinh viên (SV)
    IF ROLE_USER IN ('SV', 'NVPDT') THEN
        IF INSERTING THEN
            -- Kiểm tra MAMM thuộc học kỳ mới nhất
            SELECT COUNT(*)
            INTO v_count
            FROM MOMON
            WHERE MAMM = :NEW.MAMM
            AND HK = v_max_hk
            AND NAM = v_max_nam;

            IF v_count = 0 THEN
                RAISE_APPLICATION_ERROR(-20011, 'Chỉ được đăng ký môn học trong học kỳ mới nhất');
            END IF;
            -- Điểm phải là NULL khi thêm
            IF :NEW.DIEMTH IS NOT NULL OR 
               :NEW.DIEMQT IS NOT NULL OR 
               :NEW.DIEMCK IS NOT NULL OR 
               :NEW.DIEMTK IS NOT NULL THEN
                RAISE_APPLICATION_ERROR(-20010, 'Không được nhập điểm số');
            END IF;
        ELSIF UPDATING THEN
            -- Điểm phải là NULL khi cập nhật
            IF :NEW.DIEMTH IS NOT NULL OR 
               :NEW.DIEMQT IS NOT NULL OR 
               :NEW.DIEMCK IS NOT NULL OR 
               :NEW.DIEMTK IS NOT NULL THEN
                RAISE_APPLICATION_ERROR(-20010, 'Không được nhập điểm số');
            END IF;
        END IF;

    -- 3. Kiểm tra quyền của NV PKT
    ELSIF ROLE_USER = 'NVPKT' THEN
        -- Chỉ được cập nhật điểm, không được thay đổi MASV, MAMM, IS_14DAYS
        IF UPDATING THEN
            IF (:NEW.MASV != :OLD.MASV) OR
               (:NEW.MAMM != :OLD.MAMM) OR
               (:NEW.IS_14DAYS != :OLD.IS_14DAYS) THEN
                RAISE_APPLICATION_ERROR(-20012, 'NV PKT chỉ được cập nhật điểm số');
            END IF;
        END IF;
    END IF;
END;
/

-- ==================================
-- Tạo VPD cho bảng SINHVIEN
CREATE OR REPLACE FUNCTION FN_SV_POLICY (
    schema_name  IN VARCHAR2,
    object_name  IN VARCHAR2
) RETURN VARCHAR2
AS
    v_user VARCHAR2(30) := SYS_CONTEXT('USERENV', 'SESSION_USER');
    v_prefix CHAR(2):= SUBSTR(v_user, 1, 2);
    v_khoa VARCHAR2(10);
    v_vaitro VARCHAR(10);
BEGIN
    IF v_prefix = 'SV' THEN
        RETURN 'MASV = ''' || v_user || '''';
    ELSE
        IF v_prefix = 'NV' THEN 
            SELECT VAITRO INTO v_vaitro FROM NHANVIEN WHERE MANLD = v_user;
            IF v_vaitro = 'GV' THEN 
                SELECT MADV INTO v_khoa FROM NHANVIEN WHERE MANLD = v_user;
                RETURN 'KHOA = ''' || v_khoa || '''';
            ELSIF v_vaitro = 'NVCTSV' THEN 
                RETURN '1=1';
            ELSIF v_vaitro = 'NVPDT' THEN 
                RETURN '1=1';
            ELSE RETURN '1=0';
            END IF;
        ELSE
            RETURN '1=0';
        END IF;
    END IF;
END;
/

BEGIN
    DBMS_RLS.ADD_POLICY(
        object_schema   => 'ADMIN',
        object_name     => 'SINHVIEN',
        policy_name     => 'SV_ROW_POLICY',
        function_schema => 'ADMIN',
        policy_function => 'FN_SV_POLICY',
        statement_types => 'SELECT, INSERT, UPDATE, DELETE',
        update_check    => TRUE
    );
END;
/

CREATE OR REPLACE TRIGGER trg_SV_UPDATE
BEFORE UPDATE ON SINHVIEN
FOR EACH ROW
DECLARE
    v_user VARCHAR2(30) := SYS_CONTEXT('USERENV', 'SESSION_USER');
    v_prefix VARCHAR(10) := SUBSTR(v_user, 1, 2);
    v_vaitro VARCHAR(10);
BEGIN
    -- SV can only update ?CHI, ?T
    IF v_prefix = 'SV' THEN
        IF :NEW.DCHI != :OLD.DCHI OR :NEW.DT != :OLD.DT THEN
            NULL;
        ELSE
            RAISE_APPLICATION_ERROR(-20001, 'SV ch? ???c s?a ?CHI v� ?T.');
        END IF;
    END IF;

    -- NV_PCTSV cannot update TINHTRANG
    IF v_prefix = 'NV' THEN 
        SELECT VAITRO INTO v_vaitro FROM NHANVIEN WHERE MANLD = v_user;
            IF v_vaitro = 'NVCTSV' THEN 
                IF :NEW.TINHTRANG != :OLD.TINHTRANG THEN
                    RAISE_APPLICATION_ERROR(-20002, 'NV_PCTSV kh�ng ???c s?a TINHTRANG.');
                END IF;
            ELSIF v_vaitro = 'NVPDT' THEN 
                IF :NEW.TINHTRANG != :OLD.TINHTRANG AND
                   :NEW.MASV = :OLD.MASV AND
                   :NEW.HOTEN = :OLD.HOTEN AND
                   :NEW.PHAI = :OLD.PHAI AND
                   :NEW.NGSINH = :OLD.NGSINH AND
                   :NEW.DCHI = :OLD.DCHI AND
                   :NEW.DT = :OLD.DT AND
                   :NEW.KHOA = :OLD.KHOA THEN
                    NULL;
                ELSE
                    RAISE_APPLICATION_ERROR(-20003, 'NV_PDT ch? ???c cập nhật TINHTRANG.');
                END IF;
            ELSE 
                RAISE_APPLICATION_ERROR(-20003, 'Không có quyền cập nhật.');
            END IF;
    END IF;
END;
/
