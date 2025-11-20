# POL_EMPLOYEE OLS Policy

## Tóm tắt nhanh
- Mỗi dòng của bảng `APP.EMPLOYEE` được gắn trường `OLS_LABEL` để kiểm soát truy cập theo chính sách `POL_EMPLOYEE`.
- Ba mức nhãn (`SEN`, `CONF`, `PUB`) kết hợp với bốn compartment tương ứng với các vai trò ứng dụng (`ADMIN`, `THUKHO`, `KITHUATVIEN`, `TIEPTAN`).
- Các nhóm ứng dụng được đồng bộ với compartment để dễ mở rộng quyền dựa trên vai trò.
- Bộ nhãn (label tag 1001–1005) xác định phạm vi đọc/ghi của từng vai trò; quản trị viên có thể đọc mọi compartment, trong khi các vai trò khác bị giới hạn.
- Người dùng và nhân viên được gán nhãn qua các khối PL/SQL dùng collection giúp script gọn và dễ bảo trì.
- Chính sách áp dụng `READ_CONTROL` cùng `LABEL_DEFAULT`, đảm bảo mọi truy vấn trên `APP.EMPLOYEE` tuân thủ nhãn mặc định nếu không chỉ định rõ.

## Luồng thực thi chính
1. Cấp quyền `POL_EMPLOYEE_DBA`, thêm cột `OLS_LABEL` và tạo policy với tùy chọn `NO_CONTROL`.
2. Dùng collection để tạo bulk levels, compartments, groups và labels.
3. Thiết lập nhãn cho người dùng ứng dụng và cập nhật `OLS_LABEL` cho các bản ghi nhân viên mẫu.
4. Định nghĩa giới hạn mức đọc/ghi (`SET_LEVELS`) cho từng vai trò.
5. Áp dụng lại policy lên bảng `APP.EMPLOYEE` với tùy chọn `READ_CONTROL` để kích hoạt kiểm soát nhãn.

