-- ALTER SESSION SET CONTAINER = PDB;
-- ALTER PLUGGABLE DATABASE PDB OPEN;

-- CREATE USER ADMIN IDENTIFIED BY 12345;
-- GRANT CONNECT, RESOURCE TO ADMIN;
-- GRANT UNLIMITED TABLESPACE TO ADMIN;
-- GRANT  SELECT ANY DICTIONARY TO ADMIN;

-- -- Cấp quyền EXECUTE cho ADMIN
-- GRANT EXECUTE ON LBACSYS.SA_COMPONENTS TO ADMIN WITH GRANT OPTION; 
-- GRANT EXECUTE ON LBACSYS.sa_user_admin TO ADMIN WITH GRANT OPTION; 
-- GRANT EXECUTE ON LBACSYS.sa_label_admin TO ADMIN WITH GRANT OPTION; 
-- GRANT EXECUTE ON sa_policy_admin TO ADMIN WITH GRANT OPTION; 
-- GRANT EXECUTE ON char_to_label TO ADMIN WITH GRANT OPTION;  
-- -- ADD ADMIN VÀO LBAC_DBA
-- GRANT LBAC_DBA TO ADMIN; 
-- GRANT EXECUTE ON sa_sysdba TO ADMIN;  
-- GRANT EXECUTE ON TO_LBAC_DATA_LABEL TO ADMIN; -- CẤP QUYỀN THỰC THI 

-- GRANT DBA TO ADMIN;
--
--
--CONNECT  ADMIN/12345@localhost:1521/PDB;

CREATE TABLE NHANVIEN (
    MANLD VARCHAR2(10) PRIMARY KEY,
    HOTEN NVARCHAR2(100),
    PHAI VARCHAR2(3),
    NGSINH DATE,
    LUONG NUMBER(10,2),
    PHUCAP NUMBER(10,2), 
    DT VARCHAR2(15),
    VAITRO VARCHAR2(10),
    MADV VARCHAR2(10)
);

CREATE TABLE SINHVIEN (
    MASV VARCHAR2(10) PRIMARY KEY,
    HOTEN NVARCHAR2(50),
    PHAI VARCHAR2(3),
    NGSINH DATE, 
    DCHI VARCHAR2(255), 
    DT VARCHAR2(10),
    KHOA VARCHAR2(10),
    TINHTRANG VARCHAR2(20)
);

CREATE TABLE DONVI (
    MADV VARCHAR2(10) PRIMARY KEY,
    TEN_DV NVARCHAR2(50),
    LOAI_DV VARCHAR2(10),
    TRG_DV VARCHAR2(10)
);

CREATE TABLE HOCPHAN (
    MAHP VARCHAR2(10) PRIMARY KEY,
    TENHP NVARCHAR2(100),
    SOTC NUMBER(5),
    STLT NUMBER(3),
    STTH NUMBER(3),
    MADV VARCHAR2(10)
);

CREATE TABLE MOMON (
    MAMM VARCHAR2(10) PRIMARY KEY,
    MAHP VARCHAR2(10),
    MAGV VARCHAR2(10),
    HK NUMBER(1),
    NAM NUMBER(4)
);

CREATE TABLE DANGKY (
    MASV VARCHAR2(10),
    MAMM VARCHAR2(10),
    DIEMTH NUMBER(5,2),
    DIEMQT NUMBER(5,2),
    DIEMCK NUMBER(5,2),
    DIEMTK NUMBER(5,2),
    IS_14DAYS NUMBER(1) DEFAULT 0,
    
    PRIMARY KEY (MASV, MAMM)
);

CREATE TABLE THONGBAO (
    MATHONGBAO INTEGER PRIMARY KEY,
    NOIDUNG NVARCHAR2(1000),
    NGAYTHONGBAO DATE DEFAULT SYSDATE
);

ALTER TABLE NHANVIEN
ADD CONSTRAINT FK_NHANVIEN_MADV FOREIGN KEY (MADV)
REFERENCES DONVI(MADV);

ALTER TABLE SINHVIEN
ADD CONSTRAINT FK_SINHVIEN_KHOA FOREIGN KEY (KHOA)
REFERENCES DONVI(MADV);

ALTER TABLE HOCPHAN
ADD CONSTRAINT FK_HOCPHAN_MADV FOREIGN KEY (MADV)
REFERENCES DONVI(MADV);

ALTER TABLE MOMON
ADD CONSTRAINT FK_MOMON_MAHP FOREIGN KEY (MAHP)
REFERENCES HOCPHAN(MAHP);

ALTER TABLE MOMON
ADD CONSTRAINT FK_MOMON_MAGV FOREIGN KEY (MAGV)
REFERENCES NHANVIEN(MANLD);

ALTER TABLE DANGKY
ADD CONSTRAINT FK_DANGKY_MASV FOREIGN KEY (MASV)
REFERENCES SINHVIEN(MASV)
ON DELETE CASCADE;

ALTER TABLE DANGKY
ADD CONSTRAINT FK_DANGKY_MAMM FOREIGN KEY (MAMM)
REFERENCES MOMON(MAMM);

-- 1. Tạo dữ liệu DONVI (15 đơn vị)
BEGIN
    -- 3 khoa và 5 phòng ban ở mỗi đơn vị
    FOR dept IN (
        SELECT 1 idx, 'HOA_CS1' MADV, N'Khoa Hóa học - Cơ sở 1' TEN_DV, 'Khoa' LOAI_DV FROM DUAL UNION ALL
        SELECT 2, 'HOA_CS2', N'Khoa Hóa học - Cơ sở 2' , 'Khoa' FROM DUAL UNION ALL
        SELECT 3, 'TOAN_CS1', N'Khoa Toán học - Cơ sở 1', 'Khoa' FROM DUAL UNION ALL
        SELECT 4, 'TOAN_CS2', N'Khoa Toán học - Cơ sở 2', 'Khoa' FROM DUAL UNION ALL
        SELECT 5, 'LY_CS1', N'Khoa Vật lý - Cơ sở 1', 'Khoa' FROM DUAL UNION ALL
        SELECT 6, 'LY_CS2', N'Khoa Vật lý - Cơ sở 2', 'Khoa' FROM DUAL UNION ALL
        SELECT 7, 'PDT_CS1', N'Phòng Đào tạo - Cơ sở 1', 'Phong' FROM DUAL UNION ALL
        SELECT 8, 'PDT_CS2', N'Phòng Đào tạo - Cơ sở 2', 'Phong' FROM DUAL UNION ALL
        SELECT 9, 'PKT_CS1', N'Phòng Khảo thí - Cơ sở 1', 'Phong' FROM DUAL UNION ALL
        SELECT 10, 'PKT_CS2', N'Phòng Khảo thí - Cơ sở 2', 'Phong' FROM DUAL UNION ALL
        SELECT 11, 'TCHC_CS1', N'Phòng Tổ chức hành chính - Cơ sở 1', 'Phong' FROM DUAL UNION ALL
        SELECT 12, 'TCHC_CS2', N'Phòng Tổ chức hành chính - Cơ sở 2', 'Phong' FROM DUAL UNION ALL
        SELECT 13, 'CTSV_CS1', N'Phòng Công tác sinh viên - Cơ sở 1', 'Phong' FROM DUAL UNION ALL
        SELECT 14, 'CTSV_CS2', N'Phòng Công tác sinh viên - Cơ sở 2', 'Phong' FROM DUAL UNION ALL
        SELECT 15, 'QT_CS1', N'Phòng Quản trị - Cơ sở 1', 'Phong' FROM DUAL
    ) LOOP
        BEGIN
            INSERT INTO DONVI (MADV, TEN_DV, LOAI_DV, TRG_DV)
            VALUES (dept.MADV, dept.TEN_DV, dept.LOAI_DV, 'NV'||LPAD(dept.idx, 3, '0'));
        EXCEPTION
            WHEN DUP_VAL_ON_INDEX THEN
                NULL;
        END;
    END LOOP;
    COMMIT;
END;
/

-- 2. Tạo dữ liệu NHANVIEN với mã NVxxx
DECLARE
    v_counter NUMBER := 0;
    
    -- Hàm tạo mã nhân viên NVxxx
    FUNCTION get_next_nv_id RETURN VARCHAR2 IS
    BEGIN
        v_counter := v_counter + 1;
        RETURN 'NV' || LPAD(v_counter, 3, '0');
    END;

    -- Thủ tục chèn nhân viên
    PROCEDURE insert_nhanvien(
        p_hoten IN NVARCHAR2,
        p_phai IN VARCHAR2,
        p_ngsinh IN DATE,
        p_luong IN NUMBER,
        p_phucap IN NUMBER,
        p_dt IN VARCHAR2,
        p_vaitro IN VARCHAR2,
        p_madv IN VARCHAR2
    ) IS
        v_id VARCHAR2(10);
    BEGIN
        v_id := get_next_nv_id();
        
        INSERT INTO NHANVIEN (
            MANLD, HOTEN, PHAI, NGSINH, LUONG, PHUCAP, DT, VAITRO, MADV
        ) VALUES (
            v_id, p_hoten, p_phai, p_ngsinh, p_luong, p_phucap, p_dt, p_vaitro, p_madv
        );
        
        -- Nếu là trưởng đơn vị, cập nhật lại bảng DONVI
        IF p_vaitro = 'TRGDV' AND p_madv IN ('HOA','TOAN','LY') THEN
            UPDATE DONVI SET TRG_DV = v_id WHERE MADV = p_madv;
        END IF;
    EXCEPTION
        WHEN OTHERS THEN
            DBMS_OUTPUT.PUT_LINE('Lỗi chèn nhân viên: ' || SQLERRM);
    END;

    -- 15 trưởng đơn vị
    PROCEDURE insert_trgdv IS
        TYPE dept_array IS TABLE OF VARCHAR2(10);
        v_departments dept_array := dept_array(
            'HOA_CS1', 'HOA_CS2', 'TOAN_CS1', 'TOAN_CS2', 
            'LY_CS1', 'LY_CS2', 'PDT_CS1', 'PDT_CS2',
            'PKT_CS1', 'PKT_CS2', 'TCHC_CS1', 'TCHC_CS2','CTSV_CS1',
            'CTSV_CS2', 'QT_CS1'
        );
    BEGIN
        FOR i IN 1..15 LOOP
            insert_nhanvien(
                CASE 
                    WHEN i <= 6 THEN N'Trưởng khoa ' || 
                        CASE 
                            WHEN v_departments(i) LIKE 'HOA%' THEN N'Hóa'
                            WHEN v_departments(i) LIKE 'TOAN%' THEN N'Toán'
                            WHEN v_departments(i) LIKE 'LY%' THEN N'Vật lý'
                            ELSE N'Khác'
                        END
                    ELSE N'Trưởng phòng ' || 
                        CASE 
                            WHEN v_departments(i) LIKE 'PDT%' THEN N'Đào tạo'
                            WHEN v_departments(i) LIKE 'PKT%' THEN N'Khảo thí'
                            WHEN v_departments(i) LIKE 'TCHC%' THEN N'TC-HC'
                            WHEN v_departments(i) LIKE 'CTSV%' THEN N'CTSV'
                            WHEN v_departments(i) LIKE 'QT' THEN N'Quản trị'
                            ELSE N'Khác'
                        END
                END,
                CASE WHEN MOD(i,2) = 0 THEN 'Nu' ELSE 'Nam' END,
                TO_DATE(1975 + MOD(i,10) || '-' || LPAD(MOD(i,12)+1, 2, '0') || '-' || LPAD(MOD(i,28)+1, 2, '0'), 'YYYY-MM-DD'),
                15000000 + i*200000,
                3000000 + i*50000,
                '09' || LPAD(MOD(100+i, 90)+10, 2, '0') || '12345',
                'TRGDV',
                v_departments(i)
            );
        END LOOP;
        COMMIT;
    END insert_trgdv;

    -- 200 giảng viên
    PROCEDURE insert_gv IS
    BEGIN
        FOR i IN 1..200 LOOP
            insert_nhanvien(
                CASE 
                    WHEN MOD(i,2) = 0 THEN N'Nguyễn Thị ' || CHR(65 + MOD(i,26))
                    ELSE N'Trần Văn ' || CHR(65 + MOD(i,26))
                END,
                CASE WHEN MOD(i,2) = 0 THEN 'Nu' ELSE 'Nam' END,
                TO_DATE('198' || MOD(i,10) || '-' || LPAD(MOD(i,12)+1, 2, '0') || '-' || LPAD(MOD(i,28)+1, 2, '0'), 'YYYY-MM-DD'),
                10000000 + i*20000,
                2000000 + i*5000,
                '09' || LPAD(MOD(200+i, 90)+10, 2, '0') || '54321',
                'GV',
                CASE MOD(i,6)
                    WHEN 0 THEN 'HOA_CS1'
                    WHEN 1 THEN 'HOA_CS2'
                    WHEN 2 THEN 'TOAN_CS1'
                    WHEN 3 THEN 'TOAN_CS2'
                    WHEN 4 THEN 'LY_CS1'
                    WHEN 5 THEN 'LY_CS2'
                END
            );
        END LOOP;
        COMMIT;
    END insert_gv;

    -- 20 nhân viên phòng đào tạo
    PROCEDURE insert_nvpdt IS
    BEGIN
        FOR i IN 1..20 LOOP
            insert_nhanvien(
                N'Nguyễn Thị ' || 
                CASE MOD(i,10)
                    WHEN 0 THEN N'Anh'
                    WHEN 1 THEN N'Bình'
                    WHEN 2 THEN N'Châu'
                    WHEN 3 THEN N'Dung'
                    WHEN 4 THEN N'Giang'
                    WHEN 5 THEN N'Hạnh'
                    WHEN 6 THEN N'Lan'
                    WHEN 7 THEN N'Liên'
                    WHEN 8 THEN N'Mai'
                    ELSE N'Nga'
                END,
                'Nu',
                TO_DATE('199' || MOD(i,10) || '-' || LPAD(MOD(i,12)+1, 2, '0') || '-' || LPAD(MOD(i,28)+1, 2, '0'), 'YYYY-MM-DD'),
                8000000 + i*10000,
                1500000 + i*2000,
                '09' || LPAD(MOD(300+i, 90)+10, 2, '0') || '67890',
                'NVPDT',
                CASE MOD(i,2)
                    WHEN 0 THEN 'PDT_CS1'
                    WHEN 1 THEN 'PDT_CS2'
                END
            );
        END LOOP;
        COMMIT;
    END insert_nvpdt;

    -- 10 nhân viên phòng khảo thí
    PROCEDURE insert_nvpkt IS
    BEGIN
        FOR i IN 1..10 LOOP
            insert_nhanvien(
                N'Nguyễn Văn ' || 
                CASE MOD(i,10)
                    WHEN 0 THEN N'Anh'
                    WHEN 1 THEN N'Bình'
                    WHEN 2 THEN N'Cường'
                    WHEN 3 THEN N'Dũng'
                    WHEN 4 THEN N'Đạt'
                    WHEN 5 THEN N'Hiếu'
                    WHEN 6 THEN N'Hoàng'
                    WHEN 7 THEN N'Hùng'
                    WHEN 8 THEN N'Mạnh'
                    ELSE N'Phong'
                END,
                'Nam',
                TO_DATE('199' || MOD(i,10) || '-' || LPAD(MOD(i,12)+1, 2, '0') || '-' || LPAD(MOD(i,28)+1, 2, '0'), 'YYYY-MM-DD'),
                9000000 + i*10000,
                2000000 + i*2000,
                '09' || LPAD(MOD(400+i, 90)+10, 2, '0') || '11223',
                'NVPKT',
                CASE MOD(i,2)
                    WHEN 0 THEN 'PKT_CS1'
                    WHEN 1 THEN 'PKT_CS2'
                END
            );
        END LOOP;
        COMMIT;
    END insert_nvpkt;

    -- 15 nhân viên tổ chức hành chính
    PROCEDURE insert_nvtchc IS
    BEGIN
        FOR i IN 1..15 LOOP
            insert_nhanvien(
                N'Trần Thị ' || CHR(65 + MOD(i, 26)),
                'Nu',
                TO_DATE('199' || MOD(i,10) || '-01-01', 'YYYY-MM-DD'),
                8500000 + i*10000,
                1800000,
                '09' || LPAD(MOD(500+i, 90)+10, 2, '0') || '45678',
                'NVTCHC',
                CASE MOD(i,2)
                    WHEN 0 THEN 'TCHC_CS1'
                    WHEN 1 THEN 'TCHC_CS2'
                END
            );
        END LOOP;
        COMMIT;
    END insert_nvtchc;

    -- 10 nhân viên công tác sinh viên
    PROCEDURE insert_nvctsv IS
    BEGIN
        FOR i IN 1..10 LOOP
            insert_nhanvien(
                N'Lê Văn ' || CHR(65 + MOD(i, 26)),
                'Nam',
                TO_DATE('199' || MOD(i,10) || '-01-01', 'YYYY-MM-DD'),
                8000000 + i*10000,
                1500000,
                '09' || LPAD(MOD(600+i, 90)+10, 2, '0') || '33445',
                'NVCTSV',
                CASE MOD(i,2)
                    WHEN 0 THEN 'CTSV_CS1'
                    WHEN 1 THEN 'CTSV_CS2'
                END
            );
        END LOOP;
        COMMIT;
    END insert_nvctsv;

    -- 500 nhân viên cơ bản
    PROCEDURE insert_nvcb IS
    BEGIN
        FOR i IN 1..500 LOOP
            insert_nhanvien(
                CASE MOD(i,2)
                    WHEN 0 THEN N'Nguyễn Thị ' || CHR(65 + MOD(i,26))
                    ELSE N'Trần Văn ' || CHR(65 + MOD(i,26))
                END,
                CASE MOD(i,2) WHEN 0 THEN 'Nu' ELSE 'Nam' END,
                TO_DATE('198' || MOD(i,10) || '-' || LPAD(MOD(i,12)+1, 2, '0') || '-' || LPAD(MOD(i,28)+1, 2, '0'), 'YYYY-MM-DD'),
                7000000 + i*5000,
                1000000 + i*1000,
                '09' || LPAD(MOD(700+i, 90)+10, 2, '0') || '98765',
                'NVCB',
                CASE MOD(i,14)
                    WHEN 0 THEN 'HOA_CS1'
                    WHEN 1 THEN 'TOAN_CS1'
                    WHEN 2 THEN 'LY_CS1'
                    WHEN 3 THEN 'PDT_CS1'
                    WHEN 4 THEN 'PKT_CS1'
                    WHEN 5 THEN 'TCHC_CS1'
                    WHEN 6 THEN 'CTSV_CS1'
                    WHEN 7 THEN 'HOA_CS2'
                    WHEN 8 THEN 'TOAN_CS2'
                    WHEN 9 THEN 'LY_CS2'
                    WHEN 10 THEN 'PDT_CS2'
                    WHEN 11 THEN 'PKT_CS2'
                    WHEN 12 THEN 'TCHC_CS2'
                    WHEN 13 THEN 'CTSV_CS1'
                END
            );
            
            IF MOD(i,100) = 0 THEN
                COMMIT;
            END IF;
        END LOOP;
        COMMIT;
    END insert_nvcb;

BEGIN
    -- Reset counter
    v_counter := 0;
    
    -- Thực hiện chèn dữ liệu
    insert_trgdv;  -- 15 trưởng đơn vị
    insert_gv;     -- 200 giảng viên
    insert_nvpdt;  -- 20 nhân viên phòng đào tạo
    insert_nvpkt;  -- 10 nhân viên phòng khảo thí
    insert_nvtchc; -- 15 nhân viên tổ chức hành chính
    insert_nvctsv; -- 10 nhân viên công tác sinh viên
    insert_nvcb;   -- 500 nhân viên cơ bản
    
    DBMS_OUTPUT.PUT_LINE('Đã tạo thành công ' || v_counter || ' nhân viên');
END;
/

-- 3. Tạo dữ liệu SINHVIEN (4000 sinh viên)
DECLARE
  v_khoa_list VARCHAR2(100) := 'HOA_CS1,HOA_CS2,TOAN_CS1,TOAN_CS2,LY_CS1,LY_CS2';
  v_status_list VARCHAR2(200) := N'Đang học,Nghỉ học,Bảo lưu,Tạm nghỉ,Thôi học';
  v_lastnames VARCHAR2(1000) := N'Nguyễn,Trần,Lê,Phạm,Hoàng,Phan,Vũ,Võ,Đặng,Bùi,Đỗ,Ngô,Huỳnh,Dương,Lý';
  v_firstnames VARCHAR2(1000) := N'Văn,Thị,Hồng,Kim,Thanh,Minh,Đức,Anh,Tuấn,Linh,Ngọc,Quang,Thúy,Hải,Yến';
BEGIN
  FOR i IN 1..4000 LOOP
    BEGIN
      INSERT INTO SINHVIEN (
        MASV, HOTEN, PHAI, NGSINH, DCHI, DT, KHOA, TINHTRANG
      ) VALUES (
        'SV' || LPAD(i, 4, '0'),
        REGEXP_SUBSTR(v_lastnames, '[^,]+', 1, MOD(i,15)+1) || ' ' || 
        REGEXP_SUBSTR(v_firstnames, '[^,]+', 1, MOD(i,15)+1) || ' ' ||
        CASE MOD(i,3)
          WHEN 0 THEN N'Anh'
          WHEN 1 THEN N'Bảo'
          ELSE N'Công'
        END,
        CASE MOD(i,2) WHEN 0 THEN 'Nu' ELSE 'Nam' END,
        TO_DATE('200' || MOD(i,3) || '-' || LPAD(MOD(i,12)+1, 2, '0') || '-' || LPAD(MOD(i,28)+1, 2, '0'), 'YYYY-MM-DD'),
        N'Địa chỉ ' || i || ', ' || 
        CASE MOD(i,10)
          WHEN 0 THEN N'Quận 1, TP.HCM'
          WHEN 1 THEN N'Quận 3, TP.HCM'
          WHEN 2 THEN N'Quận 5, TP.HCM'
          WHEN 3 THEN N'Quận 7, TP.HCM'
          WHEN 4 THEN N'Quận 10, TP.HCM'
          WHEN 5 THEN N'Quận Bình Thạnh, TP.HCM'
          WHEN 6 THEN N'Quận Gò Vấp, TP.HCM'
          WHEN 7 THEN N'Quận Phú Nhuận, TP.HCM'
          WHEN 8 THEN N'Quận Tân Bình, TP.HCM'
          ELSE N'Quận Thủ Đức, TP.HCM'
        END,
        '09' || LPAD(MOD(i,90)+10, 2, '0') || '123' || MOD(i,1000),
        REGEXP_SUBSTR(v_khoa_list, '[^,]+', 1, MOD(i,6)+1),
        REGEXP_SUBSTR(v_status_list, '[^,]+', 1, MOD(i,5)+1)
      );
    EXCEPTION
      WHEN DUP_VAL_ON_INDEX THEN
        NULL;
    END;
    
    IF MOD(i,500) = 0 THEN
      COMMIT;
    END IF;
  END LOOP;
  COMMIT;
END;
/

-- 4. Tạo dữ liệu HOCPHAN (100 học phần)
DECLARE
  v_departments VARCHAR2(100) := 'HOA_CS1,HOA_CS2,TOAN_CS1,TOAN_CS2,LY_CS1,LY_CS2';
  v_prefixes VARCHAR2(200) := N'Đại cương,Nhập môn,Chuyên đề,Nâng cao,Ứng dụng,Phân tích,Lý thuyết,Thực hành';
  v_subjects VARCHAR2(1000) := N'Toán cao cấp,Đại số tuyến tính,Toán tổ hợp,Xác suất thống kê,Vật lý đại cương 1,Vật lý đại cương 2,Vật lý hiện đại 1,Vật lý hiện đại 2,Vật lý thống kê,Hóa hữu cơ 1, Hóa hữu cơ 2,Hóa vô cơ 1,Hóa vô cơ 2';
BEGIN
  FOR i IN 1..100 LOOP
    BEGIN
      INSERT INTO HOCPHAN (
        MAHP, TENHP, SOTC, STLT, STTH, MADV
      ) VALUES (
        'HP' || LPAD(i, 3, '0'),
        REGEXP_SUBSTR(v_prefixes, '[^,]+', 1, MOD(i,8)+1) || ' ' || 
        REGEXP_SUBSTR(v_subjects, '[^,]+', 1, MOD(i,13)+1),
        MOD(i,5)+2,
        (MOD(i,5)+2)*15,
        (MOD(i,5)+2)*30,
        REGEXP_SUBSTR(v_departments, '[^,]+', 1, MOD(i,6)+1)
      );
    EXCEPTION
      WHEN DUP_VAL_ON_INDEX THEN
        NULL;
    END;
  END LOOP;
  COMMIT;
END;
/

-- 6. Tạo dữ liệu MOMON (300 môn học mở)
DECLARE
  v_current_year NUMBER := EXTRACT(YEAR FROM SYSDATE);
  v_hocky NUMBER;
  v_namhoc NUMBER;
  v_gv_start_id NUMBER := 16;
BEGIN
  FOR i IN 1..300 LOOP
    BEGIN
      -- Phân bổ đều cho 3 học kỳ (1,2,3)
      v_hocky := MOD(i,3)+1;
      v_namhoc := v_current_year - MOD(i,3); -- Các năm 2022-2024
      
      -- Đảm bảo năm học không nhỏ hơn 2022
      IF v_namhoc < 2022 THEN
        v_namhoc := 2022;
      END IF;
      
      INSERT INTO MOMON (
        MAMM, MAHP, MAGV, HK, NAM
      ) VALUES (
        'MM' || LPAD(i, 3, '0'),
        'HP' || LPAD(MOD(i,100)+1, 3, '0'),
        'NV' || LPAD(v_gv_start_id + MOD(i,200), 3, '0'),
        v_hocky,
        v_namhoc
      );
    EXCEPTION
      WHEN DUP_VAL_ON_INDEX THEN
        NULL;
    END;
  END LOOP;
  COMMIT;
END;
/


-- 7. Tạo dữ liệu DANGKY (10000 đăng ký)
DECLARE
  v_max_hk NUMBER;
  v_max_nam NUMBER;
  v_is_current NUMBER(1); -- 1=TRUE (học kỳ hiện tại), 0=FALSE
  v_count_current NUMBER := 0;
BEGIN
  -- 1. Xác định học kỳ hiện tại (HK và NAM lớn nhất)
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
      v_max_hk := 2;  -- Giá trị mặc định nếu bảng trống
      v_max_nam := 2024;
  END;
  
  DBMS_OUTPUT.PUT_LINE('Học kỳ hiện tại: HK ' || v_max_hk || ' NĂM ' || v_max_nam);
  
  -- 2. Insert 10000 bản ghi vào DANGKY
  FOR i IN 1..10000 LOOP
    -- Xác định xem môn học này có thuộc học kỳ hiện tại không
    BEGIN
      SELECT 
        CASE WHEN HK = v_max_hk AND NAM = v_max_nam THEN 1 ELSE 0 END
      INTO v_is_current
      FROM MOMON
      WHERE MAMM = 'MM' || LPAD(MOD(i,300)+1, 3, '0');
    EXCEPTION
      WHEN NO_DATA_FOUND THEN
        v_is_current := 0;
    END;
    
    -- Đếm số môn học hiện tại để kiểm tra
    IF v_is_current = 1 THEN
      v_count_current := v_count_current + 1;
    END IF;
    
    -- Chèn dữ liệu vào DANGKY
    BEGIN
      INSERT INTO DANGKY (
        MASV, 
        MAMM, 
        IS_14DAYS,
        DIEMTH, 
        DIEMQT, 
        DIEMCK, 
        DIEMTK
      ) VALUES (
        'SV' || LPAD(MOD(i,4000)+1, 4, '0'), -- MASV ngẫu nhiên từ SV0001-SV4000
        'MM' || LPAD(MOD(i,300)+1, 3, '0'),  -- MAMM ngẫu nhiên từ MM001-MM300
        v_is_current, -- IS_14DAYS = 1 nếu là học kỳ hiện tại
        
        -- Điểm số: NULL nếu là học kỳ hiện tại, random nếu học kỳ cũ
        CASE WHEN v_is_current = 1 THEN NULL ELSE DBMS_RANDOM.VALUE(0,10) END,
        CASE WHEN v_is_current = 1 THEN NULL ELSE DBMS_RANDOM.VALUE(0,10) END,
        CASE WHEN v_is_current = 1 THEN NULL ELSE DBMS_RANDOM.VALUE(0,10) END,
        CASE WHEN v_is_current = 1 THEN NULL ELSE 
          (DBMS_RANDOM.VALUE(0,10)*0.3 + DBMS_RANDOM.VALUE(0,10)*0.3 + DBMS_RANDOM.VALUE(0,10)*0.4)
        END
      );
    EXCEPTION
      WHEN OTHERS THEN
        DBMS_OUTPUT.PUT_LINE('Lỗi khi chèn bản ghi ' || i || ': ' || SQLERRM);
    END;
    
    -- Commit mỗi 1000 bản ghi
    IF MOD(i,1000) = 0 THEN
      COMMIT;
    END IF;
  END LOOP;
  COMMIT;
  
  -- Thống kê kết quả
  DBMS_OUTPUT.PUT_LINE('Đã tạo:');
  DBMS_OUTPUT.PUT_LINE('- ' || v_count_current || ' bản ghi cho học kỳ hiện tại (IS_14DAYS=1, điểm=NULL)');
  DBMS_OUTPUT.PUT_LINE('- ' || (10000 - v_count_current) || ' bản ghi cho học kỳ cũ (IS_14DAYS=0, có điểm)');
END;
/

-- Thêm 21 thông báo mẫu
INSERT ALL
  INTO THONGBAO (MATHONGBAO, NOIDUNG, NGAYTHONGBAO) VALUES (1, N'Thông báo họp giao ban tuần này tại phòng họp lớn.', TO_DATE('07/04/2025', 'DD/MM/YYYY'))
  INTO THONGBAO (MATHONGBAO, NOIDUNG, NGAYTHONGBAO) VALUES (2, N'Nhắc nhở sinh viên hoàn thành học phí học kỳ này.', TO_DATE('06/04/2025', 'DD/MM/YYYY'))
  INTO THONGBAO (MATHONGBAO, NOIDUNG, NGAYTHONGBAO) VALUES (3, N'Lịch bảo trì hệ thống sẽ diễn ra vào cuối tuần.', TO_DATE('06/04/2025', 'DD/MM/YYYY'))
  INTO THONGBAO (MATHONGBAO, NOIDUNG, NGAYTHONGBAO) VALUES (4, N'Thông báo tuyển sinh viên thực tập tại phòng Hóa.', TO_DATE('13/04/2025', 'DD/MM/YYYY'))
  INTO THONGBAO (MATHONGBAO, NOIDUNG, NGAYTHONGBAO) VALUES (5, N'Kế hoạch tổ chức hội thảo khoa học Vật lý.', TO_DATE('06/04/2025', 'DD/MM/YYYY'))
  INTO THONGBAO (MATHONGBAO, NOIDUNG, NGAYTHONGBAO) VALUES (6, N'Thông báo nghỉ lễ Giỗ tổ Hùng Vương.', TO_DATE('16/04/2025', 'DD/MM/YYYY'))
  INTO THONGBAO (MATHONGBAO, NOIDUNG, NGAYTHONGBAO) VALUES (7, N'Thông báo về việc cập nhật thông tin cá nhân.', TO_DATE('16/04/2025', 'DD/MM/YYYY'))
  INTO THONGBAO (MATHONGBAO, NOIDUNG, NGAYTHONGBAO) VALUES (8, N'Lịch thi cuối kỳ môn Hóa học đã được công bố.', TO_DATE('16/04/2025', 'DD/MM/YYYY'))
  INTO THONGBAO (MATHONGBAO, NOIDUNG, NGAYTHONGBAO) VALUES (9, N'Thông báo về việc nhận học bổng học kỳ này.', TO_DATE('15/04/2025', 'DD/MM/YYYY'))
  INTO THONGBAO (MATHONGBAO, NOIDUNG, NGAYTHONGBAO) VALUES (10, N'Thông báo tuyển dụng nhân viên phòng TCHC.', TO_DATE('03/04/2025', 'DD/MM/YYYY'))
  INTO THONGBAO (MATHONGBAO, NOIDUNG, NGAYTHONGBAO) VALUES (11, N'Thông báo về việc đổi phòng học môn Hóa.', TO_DATE('08/04/2025', 'DD/MM/YYYY'))
  INTO THONGBAO (MATHONGBAO, NOIDUNG, NGAYTHONGBAO) VALUES (12, N'Nhắc nhở nộp báo cáo thực tập đúng hạn.', TO_DATE('08/04/2025', 'DD/MM/YYYY'))
  INTO THONGBAO (MATHONGBAO, NOIDUNG, NGAYTHONGBAO) VALUES (13, N'Thông báo về việc đăng ký đề tài nghiên cứu khoa học.', TO_DATE('07/04/2025', 'DD/MM/YYYY'))
  INTO THONGBAO (MATHONGBAO, NOIDUNG, NGAYTHONGBAO) VALUES (14, N'Thông báo về việc tổ chức ngày hội việc làm.', TO_DATE('10/04/2025', 'DD/MM/YYYY'))
  INTO THONGBAO (MATHONGBAO, NOIDUNG, NGAYTHONGBAO) VALUES (15, N'Thông báo về việc kiểm tra y tế định kỳ.', TO_DATE('09/04/2025', 'DD/MM/YYYY'))
  INTO THONGBAO (MATHONGBAO, NOIDUNG, NGAYTHONGBAO) VALUES (16, N'Thông báo về việc bảo trì hệ thống điện.', TO_DATE('10/04/2025', 'DD/MM/YYYY'))
  INTO THONGBAO (MATHONGBAO, NOIDUNG, NGAYTHONGBAO) VALUES (17, N'Thông báo về việc phát bằng tốt nghiệp.', TO_DATE('10/04/2025', 'DD/MM/YYYY'))
  INTO THONGBAO (MATHONGBAO, NOIDUNG, NGAYTHONGBAO) VALUES (18, N'Thông báo về Lịch kiểm tra phòng thí nghiệm Hóa tại Cơ sở 1', TO_DATE('06/04/2025', 'DD/MM/YYYY'))
  INTO THONGBAO (MATHONGBAO, NOIDUNG, NGAYTHONGBAO) VALUES (19, N'Thông báo về Hội nghị trưởng khoa Hóa toàn trường', TO_DATE('01/04/2025', 'DD/MM/YYYY'))
  INTO THONGBAO (MATHONGBAO, NOIDUNG, NGAYTHONGBAO) VALUES (20, N'Thông báo về tổ chức Hội nghị Hóa học tại cơ sở 2', TO_DATE('14/04/2025', 'DD/MM/YYYY'))
  INTO THONGBAO (MATHONGBAO, NOIDUNG, NGAYTHONGBAO) VALUES (21, N'Thông báo về việc tổ chức hội nghị cán bộ.', TO_DATE('03/04/2025', 'DD/MM/YYYY'))
SELECT * FROM DUAL;
COMMIT;