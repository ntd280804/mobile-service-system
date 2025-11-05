-- Tạo user trưởng đơn vị
CREATE USER NV001 IDENTIFIED BY "NV001";
CREATE USER NV002 IDENTIFIED BY "NV002";
CREATE USER NV006 IDENTIFIED BY "NV006";
CREATE USER NV011 IDENTIFIED BY "NV011";
GRANT role_trgdv TO NV001, NV002, NV006, NV011;
GRANT role_nvcb TO NV001, NV002, NV006, NV011;

-- Tạo user giảng viên
CREATE USER NV064 IDENTIFIED BY "NV064";
GRANT role_gv TO NV064;
GRANT role_nvcb TO NV064;

-- Tạo user nhân viên phòng đào tạo
CREATE USER NV224 IDENTIFIED BY "NV224";
GRANT role_nvpdt TO NV224;
GRANT role_nvcb TO NV224;

-- Tạo user sinh viên
CREATE USER SV0001 IDENTIFIED BY "SV0001";
GRANT role_sv TO SV0001;

-- Tạo user nhân viên tổ chức hành chính
CREATE USER NV246 IDENTIFIED BY "NV246";
GRANT role_tchc TO NV246;
GRANT role_nvcb TO NV246;


-- Tạo user nhân viên công tác sinh viên
CREATE USER NV261 IDENTIFIED BY "NV261";
GRANT role_nvcb TO NV261;
GRANT role_nvctsv TO NV261;

-- Tạo user nhân viên phòng khảo thí
CREATE USER NV236 IDENTIFIED BY "NV236";
GRANT role_nvcb TO NV236;
GRANT role_nvpkt TO NV236;

-- Tạo user nhân viên cơ bản
CREATE USER NV272 IDENTIFIED BY "NV272";
GRANT role_nvcb TO NV272;
