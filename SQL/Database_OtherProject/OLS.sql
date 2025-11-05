-- Kiểm tra OLS dã bật chưa (sử dụng SYS USER)
-- SELECT VALUE FROM v$option WHERE parameter = 'Oracle Label Security';
-- SELECT status FROM dba_ols_status WHERE name = 'OLS_CONFIGURE_STATUS'; 

-- Bật OLS (nếu chưa bật)
-- EXEC LBACSYS.CONFIGURE_OLS;
-- EXEC LBACSYS.OLS_ENFORCEMENT.ENABLE_OLS;

-- Unlock LBACSYS (sử dụng SYS USER)
ALTER USER lbacsys IDENTIFIED BY lbacsys ACCOUNT UNLOCK CONTAINER ; 

-- 
-- SHUTDOWN IMMEDIATE;
-- STARTUP MOUNT;

-- Sử dụng ADMIN để tạo OLS
-- Tạo một policy OLS cho bảng THONGBAO
SET ROLE ALL; 


BEGIN
    SA_SYSDBA.CREATE_POLICY(
        policy_name => 'OLS_POLICY',
        column_name => 'OLS_LABEL'
    );
    SA_SYSDBA.ENABLE_POLICY ('OLS_POLICY');
END;
/

-- Khởi động lại SQL Developer để tạo các component cho OLS
-- Tạo Levels
BEGIN
    SA_COMPONENTS.CREATE_LEVEL(
        policy_name => 'OLS_POLICY',
        level_num => 100,
        short_name => 'SV',
        long_name => 'Sinh vien'
    );
END;
/
BEGIN
    SA_COMPONENTS.CREATE_LEVEL(
        policy_name => 'OLS_POLICY',
        level_num => 200,
        short_name => 'NV',
        long_name => 'Nhan vien'
    );
END;
/
BEGIN
    SA_COMPONENTS.CREATE_LEVEL(
        policy_name => 'OLS_POLICY',
        level_num => 300,
        short_name => 'TDV',
        long_name => 'Truong don vi'
    );
END;
/
-- Tạo Compartments
BEGIN
    SA_COMPONENTS.CREATE_COMPARTMENT(
        policy_name => 'OLS_POLICY',
        comp_num => 1,
        short_name => 'T',
        long_name => 'Toan'
    );
END;
/
BEGIN
    SA_COMPONENTS.CREATE_COMPARTMENT(
        policy_name => 'OLS_POLICY',
        comp_num => 2,
        short_name => 'L',
        long_name => 'Ly'
    );
END;
/
BEGIN
    SA_COMPONENTS.CREATE_COMPARTMENT(
        policy_name => 'OLS_POLICY',
        comp_num => 3,
        short_name => 'H',
        long_name => 'Hoa'
    );
END;
/
BEGIN
    SA_COMPONENTS.CREATE_COMPARTMENT(
        policy_name => 'OLS_POLICY',
        comp_num => 4,
        short_name => 'HC',
        long_name => 'Hanh chinh'
    );
END;
/
-- Tạo Group
BEGIN
    SA_COMPONENTS.CREATE_GROUP(
        policy_name => 'OLS_POLICY',
        group_num => 1,
        short_name => 'CS1',
        long_name => 'Co so 1'
    );
END;
/
BEGIN
    SA_COMPONENTS.CREATE_GROUP(
        policy_name => 'OLS_POLICY',
        group_num => 2,
        short_name => 'CS2',
        long_name => 'Co so 2'
    );
END;
/

-- Tạo Label cho User
BEGIN
    -- u1: Trưởng đơn vị có quyền đọc được toàn bộ thông báo không phân biệt lĩnh vực và cơ sở.
    SA_LABEL_ADMIN.CREATE_LABEL(
        policy_name  => 'OLS_POLICY',
        label_tag    => 301,
        label_value  => 'TDV:T,L,H,HC:CS1,CS2'
    );

    -- u2. Trưởng đơn vị đọc thông báo khoa Hóa ở cơ sở 2
    SA_LABEL_ADMIN.CREATE_LABEL(
        policy_name => 'OLS_POLICY',
        label_tag   => 302,
        label_value => 'TDV:H:CS2'      
    );

    -- u3. Trưởng đơn vị đọc thông báo khoa Lý ở cơ sở 2
    SA_LABEL_ADMIN.CREATE_LABEL(
        policy_name => 'OLS_POLICY',
        label_tag   => 303,
        label_value => 'TDV:L:CS2'     
    );

    -- u4. Nhân viên đọc thông báo khoa Hóa ở cơ sở 2
    SA_LABEL_ADMIN.CREATE_LABEL(
        policy_name => 'OLS_POLICY',
        label_tag   => 304,
        label_value => 'NV:H:CS2'    
    );

    -- u5. Sinh viên đọc thông báo khoa Hóa ở cơ sở 2
    SA_LABEL_ADMIN.CREATE_LABEL(
        policy_name => 'OLS_POLICY',
        label_tag   => 305,
        label_value => 'SV:H:CS2'      
    );

    -- u6. Trưởng đơn vị đọc thông báo hành chính không phân biệt cơ sở
    SA_LABEL_ADMIN.CREATE_LABEL(
        policy_name => 'OLS_POLICY',
        label_tag   => 306,
        label_value => 'TDV:HC:CS1,CS2'       
    );

    -- u7. Nhân viên đọc tất cả thông báo không phân biệt lĩnh vực và cơ sở
    SA_LABEL_ADMIN.CREATE_LABEL(
        policy_name => 'OLS_POLICY',
        label_tag   => 307,
        label_value => 'NV:T,L,H,HC:CS1,CS2'
    );

    -- u8. Nhân viên đọc thông báo hành chính ở cơ sở 1
    SA_LABEL_ADMIN.CREATE_LABEL(
        policy_name => 'OLS_POLICY',
        label_tag   => 308,
        label_value => 'NV:HC:CS1'
    );
END;
/

BEGIN
    -- t3: Cần phát tán đến tất cả sinh viên
    SA_LABEL_ADMIN.CREATE_LABEL(
        policy_name => 'OLS_POLICY',
        label_tag   => 403,
        label_value  => 'SV:T,L,H:CS1,CS2'
    );

    -- t4: Cần phát tán đến sinh viên thuộc khoa Hóa ở cơ sở 1
    SA_LABEL_ADMIN.CREATE_LABEL(
        policy_name => 'OLS_POLICY',
        label_tag   => 404,
        label_value  => 'SV:H:CS1'
    );

    --t6: Cần phát tán đến sinh viên thuộc khoa Hóa ở cả hai cơ sở
    SA_LABEL_ADMIN.CREATE_LABEL(
        policy_name => 'OLS_POLICY',
        label_tag   => 406,
        label_value  => 'SV:H:CS1,CS2'
    );

    -- t8: Cần phát tán đến trưởng khoa Hóa ở cơ sở 1
    SA_LABEL_ADMIN.CREATE_LABEL(
        policy_name => 'OLS_POLICY',
        label_tag   => 408,
        label_value  => 'TDV:H:CS1'
    );

    -- t9: Cần phát tán đến trưởng khoa Hóa ở cơ sở 1 và cơ sở 2
    SA_LABEL_ADMIN.CREATE_LABEL(
        policy_name => 'OLS_POLICY',
        label_tag   => 409,
        label_value  => 'TDV:H:CS1,CS2'
    );

    SA_LABEL_ADMIN.CREATE_LABEL(
        policy_name => 'OLS_POLICY',
        label_tag   => 411,
        label_value  => 'NV:HC:CS1,CS2'
    );
END;
/

BEGIN 
    SA_POLICY_ADMIN.APPLY_TABLE_POLICY ( 
        policy_name => 'OLS_POLICY', 
        schema_name => 'ADMIN', 
        table_name => 'THONGBAO', 
        table_options => 'NO_CONTROL'
    ); 
END; 
/

UPDATE THONGBAO 
SET OLS_LABEL = CHAR_TO_LABEL('OLS_POLICY','NV:T,L,H,HC:CS1,CS2') 
WHERE MATHONGBAO = 1;

UPDATE THONGBAO 
SET OLS_LABEL = CHAR_TO_LABEL('OLS_POLICY','SV:T,L,H:CS1,CS2') 
WHERE MATHONGBAO = 2; 

UPDATE THONGBAO 
SET OLS_LABEL = CHAR_TO_LABEL('OLS_POLICY','SV:T,L,H:CS1,CS2') 
WHERE MATHONGBAO = 3; 

UPDATE THONGBAO 
SET OLS_LABEL = CHAR_TO_LABEL('OLS_POLICY','SV:H:CS1') 
WHERE MATHONGBAO = 4; 

UPDATE THONGBAO 
SET OLS_LABEL = CHAR_TO_LABEL('OLS_POLICY','TDV:L:CS2') 
WHERE MATHONGBAO = 5; 

UPDATE THONGBAO 
SET OLS_LABEL = CHAR_TO_LABEL('OLS_POLICY','NV:HC:CS1,CS2') 
WHERE MATHONGBAO = 6;

UPDATE THONGBAO 
SET OLS_LABEL = CHAR_TO_LABEL('OLS_POLICY','NV:T,L,H,HC:CS1,CS2') 
WHERE MATHONGBAO = 7;

UPDATE THONGBAO 
SET OLS_LABEL = CHAR_TO_LABEL('OLS_POLICY','SV:H:CS2') 
WHERE MATHONGBAO = 8;

UPDATE THONGBAO 
SET OLS_LABEL = CHAR_TO_LABEL('OLS_POLICY','SV:T,L,H:CS1,CS2') 
WHERE MATHONGBAO = 9;

UPDATE THONGBAO 
SET OLS_LABEL = CHAR_TO_LABEL('OLS_POLICY','NV:HC:CS1,CS2') 
WHERE MATHONGBAO = 10;

UPDATE THONGBAO 
SET OLS_LABEL = CHAR_TO_LABEL('OLS_POLICY','SV:H:CS2') 
WHERE MATHONGBAO = 11;

UPDATE THONGBAO 
SET OLS_LABEL = CHAR_TO_LABEL('OLS_POLICY','SV:T,L,H:CS1,CS2') 
WHERE MATHONGBAO = 12;

UPDATE THONGBAO 
SET OLS_LABEL = CHAR_TO_LABEL('OLS_POLICY','SV:H:CS1') 
WHERE MATHONGBAO = 13;

UPDATE THONGBAO 
SET OLS_LABEL = CHAR_TO_LABEL('OLS_POLICY','SV:T,L,H:CS1,CS2') 
WHERE MATHONGBAO = 14;

UPDATE THONGBAO 
SET OLS_LABEL = CHAR_TO_LABEL('OLS_POLICY','SV:H:CS2') 
WHERE MATHONGBAO = 15;

UPDATE THONGBAO 
SET OLS_LABEL = CHAR_TO_LABEL('OLS_POLICY','NV:H:CS2') 
WHERE MATHONGBAO = 16;

UPDATE THONGBAO 
SET OLS_LABEL = CHAR_TO_LABEL('OLS_POLICY','SV:H:CS1,CS2') 
WHERE MATHONGBAO = 17;

UPDATE THONGBAO 
SET OLS_LABEL = CHAR_TO_LABEL('OLS_POLICY','TDV:H:CS1') 
WHERE MATHONGBAO = 18;

UPDATE THONGBAO 
SET OLS_LABEL = CHAR_TO_LABEL('OLS_POLICY','TDV:H:CS1,CS2') 
WHERE MATHONGBAO = 19;

UPDATE THONGBAO 
SET OLS_LABEL = CHAR_TO_LABEL('OLS_POLICY','TDV:H:CS1') 
WHERE MATHONGBAO = 20;

UPDATE THONGBAO 
SET OLS_LABEL = CHAR_TO_LABEL('OLS_POLICY','NV:HC:CS1') 
WHERE MATHONGBAO = 21;

BEGIN
    SA_POLICY_ADMIN.REMOVE_TABLE_POLICY('OLS_POLICY','ADMIN','THONGBAO');
    SA_POLICY_ADMIN.APPLY_TABLE_POLICY (
        policy_name => 'OLS_POLICY',
        schema_name => 'ADMIN',
        table_name => 'THONGBAO',
        table_options => 'READ_CONTROL',
        predicate => NULL
    );
END;
/
-- Gán nhãn cho các user 
EXECUTE SA_USER_ADMIN.SET_USER_LABELS('OLS_POLICY','NV001','TDV:T,L,H,HC:CS1,CS2');

EXECUTE SA_USER_ADMIN.SET_USER_LABELS('OLS_POLICY','NV002','TDV:H:CS2');

EXECUTE SA_USER_ADMIN.SET_USER_LABELS('OLS_POLICY','NV006','TDV:L:CS2');

EXECUTE SA_USER_ADMIN.SET_USER_LABELS('OLS_POLICY','NV064','NV:H:CS2');

EXECUTE SA_USER_ADMIN.SET_USER_LABELS('OLS_POLICY','SV0001','SV:H:CS2');

EXECUTE SA_USER_ADMIN.SET_USER_LABELS('OLS_POLICY','NV011','TDV:HC:CS1,CS2');

EXECUTE SA_USER_ADMIN.SET_USER_LABELS('OLS_POLICY','NV224','NV:T,L,H,HC:CS1,CS2');

EXECUTE SA_USER_ADMIN.SET_USER_LABELS('OLS_POLICY','NV246','NV:HC:CS1');

EXECUTE SA_USER_ADMIN.SET_USER_LABELS('OLS_POLICY','NV272','NV:T,L,H,HC:CS1,CS2');

EXECUTE SA_USER_ADMIN.SET_USER_LABELS('OLS_POLICY','NV236','NV:T,L,H,HC:CS1,CS2');

EXECUTE SA_USER_ADMIN.SET_USER_LABELS('OLS_POLICY','NV261','NV:T,L,H,HC:CS1,CS2');
