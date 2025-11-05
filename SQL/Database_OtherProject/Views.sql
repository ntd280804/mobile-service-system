-- Tạo view cho nhân viên cơ bản (NVCB)
-- Nhân viên cơ bản có quyền xem và cập nhật số điện thoại (DT) của mình
CREATE OR REPLACE VIEW V_NVCB AS
SELECT
    *
FROM
    ADMIN.NHANVIEN
WHERE
    MANLD = SYS_CONTEXT('USERENV', 'SESSION_USER');

-- Tạo view cho trưởng đơn vị (TRGDV)
-- Trưởng đơn vị có quyền xem thông tin của các nhân viên thuộc đơn vị mình làm trưởng,
-- trừ các thuộc tính LUONG và PHUCAP
CREATE OR REPLACE VIEW V_NHANVIEN_TRGDV AS
SELECT
    MANLD,
    HOTEN,
    PHAI,
    NGSINH,
    DT,
    VAITRO,
    MADV
FROM
    ADMIN.NHANVIEN
WHERE MADV = (
    SELECT MADV 
    FROM DONVI 
    WHERE TRG_DV = SYS_CONTEXT('USERENV', 'SESSION_USER')
);

-- Tạo view để GIAOVIEN chỉ xem các dòng phân công liên quan đến họ
-- trong bảng MOMON
CREATE OR REPLACE VIEW V_MOMON_GV AS
SELECT *
FROM ADMIN.MOMON
WHERE MAGV = SYS_CONTEXT('USERENV', 'SESSION_USER')
/


-- Tạo view để NVPDT có thể xem, thêm, xóa, sửa
-- trong bảng MOMON liên quan tới kỳ hiện tại.
CREATE OR REPLACE VIEW V_MOMON_NVPDT AS
SELECT *
FROM ADMIN.MOMON 
WHERE NAM = EXTRACT(YEAR FROM SYSDATE)
AND HK = CASE
WHEN EXTRACT(MONTH FROM SYSDATE) BETWEEN 9 AND 12 THEN 1
WHEN EXTRACT(MONTH FROM SYSDATE) BETWEEN 1 AND 4 THEN 2
ELSE 3 END
/


-- Tạo view để trưởng đơn vị có thể xem phân công của 
-- giảng dạy của giảng viên thuộc đơn vị mình phụ trách.
CREATE OR REPLACE VIEW V_MOMON_TRGDV AS
SELECT *
FROM ADMIN.MOMON 
WHERE MAGV IN (
                SELECT MANLD
                FROM ADMIN.NHANVIEN
                WHERE MADV = (
                                SELECT MADV
                                FROM ADMIN.DONVI
                                WHERE TRG_DV = SYS_CONTEXT('USERENV', 'SESSION_USER')
                             )
              )
/     

-- Tạo view để sinh viên có thể xem dòng dữ liệu MOMON
-- mà thuộc phụ trách bởi Khoa mà sinh viên đang học.
CREATE OR REPLACE VIEW V_MOMON_SV AS
SELECT *
FROM ADMIN.MOMON
WHERE MAHP IN (
                SELECT MAHP
                FROM ADMIN.HOCPHAN
                WHERE MADV IN 
                (
                    SELECT KHOA
                    FROM ADMIN.SINHVIEN
                    WHERE MASV = SYS_CONTEXT('USERENV', 'SESSION_USER')
                )
               )
/
