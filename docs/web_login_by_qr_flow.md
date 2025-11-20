# 5.3 Mã QR — Đăng nhập 2 chiều giữa Web và Mobile (QR Login)

## 5.3.1 Tổng quan

Đăng nhập hai chiều bằng QR cho phép người dùng xác thực trên web bằng cách quét mã QR từ ứng dụng mobile (hoặc ngược lại), tạo trải nghiệm nhanh, không cần nhập mật khẩu trên thiết bị hiện tại. Thường dùng cho:

- Đăng nhập web bằng mobile (web hiển thị QR và → mobile quét → xác thực)
- Đăng nhập mobile bằng web (web hiển thị QR và code → mobile quét → xác thực)

---

## 5.3.2 Luồng hoạt động

### 5.3.2.1 Đăng nhập web bằng mobile

#### A: Phía WebApp (Browser)
- **Bước 1:** Gọi API `POST /api/Public/QrLogin/create` (cho phép anonymous), API trả về `qrLoginId`, `code`, `expiresAtUtc`.
- **Bước 2:** Browser hiển thị code/QR cho người dùng và đồng thời bắt đầu polling trạng thái QR mỗi 2–3 giây.
- **Bước 3:** Kiểm tra trạng thái QR:
    - Nếu trạng thái là Pending thì tiếp tục polling và hiển thị đồng hồ đếm ngược.
    - Nếu trạng thái là Confirmed thì gọi API `POST /Public/Customer/CompleteQrLogin` và gửi `{ username, roles, token, sessionId }` (lấy từ API polling)
- **Bước 4:** Người dùng sẽ được đăng nhập vào tài khoản của mình thông qua phương thức Proxy Authentication.

#### B: Phía Mobile
- **Bước 1:** Đã đăng nhập sẵn và mở chức năng quét hoặc nhập mã code.
- **Bước 2:** Gửi xác nhận thông qua API `POST /api/Public/QrLogin/confirm` với JWT của mobile.
- **Bước 3:** Xác thực mã code/QR và cập nhật trạng thái của QR.

---

### 5.3.2.2 Đăng nhập mobile bằng web

#### A: Phía WebApp (Browser)
- **Bước 1:** Đã đăng nhập và gọi API `POST /api/Public/WebToMobileQr/create` để sinh mã QR.
- **Bước 2:** Hiển thị mã QR/code vừa nhận được.

#### B: Phía Mobile
- **Bước 1:** Chọn chức năng đăng nhập bằng mã QR từ web.
- **Bước 2:** Nhập hoặc quét QR/code và gửi API xác nhận `POST /api/Public/WebToMobileQr/confirm`.
- **Bước 3:** Kiểm tra hợp lệ và trạng thái QR:
    - Nếu trạng thái là Pending thì tiếp tục polling và hiển thị đồng hồ đếm ngược (nếu mobile muốn hỗ trợ UX tốt).
    - Nếu trạng thái là Confirmed thì gọi API nội bộ (nếu cần) để nhận token `{ username, roles, token, sessionId }` (lấy từ API polling nếu cần thiết, hoặc trực tiếp trong response xác nhận thành công).
- **Bước 4:** Người dùng sẽ được đăng nhập vào tài khoản của mình thông qua phương thức Proxy Authentication trên app mobile.

---

## Bảng API các vai trò & trình tự chính:

| Luồng                      | Bên tạo QR/code | API tạo mã                      | Ai nhập QR/code      | API xác nhận                                | Ai polling    | Ai được cấp session mới |
|----------------------------|-----------------|-------------------------------|----------------------|--------------------------------------------|--------------|-------------------------|
| Đăng nhập web từ mobile    | WebApp          | /api/Public/QrLogin/create    | Mobile app           | /api/Public/QrLogin/confirm                | WebApp       | WebApp                  |
| Đăng nhập mobile từ web    | WebApp          | /api/Public/WebToMobileQr/create | Mobile app        | /api/Public/WebToMobileQr/confirm          | Mobile app*  | Mobile app              |

(*Mobile app có thể không polling nếu API trả luôn session, nhưng nên làm loading và kiểm tra trạng thái xác nhận nếu server cho phép polling một chiều cho UX tốt.)

---

## Sơ đồ minh hoạ giản lược

```
// Đăng nhập Web từ Mobile
[WebApp] --(tạo QR/code)--> [WebAPI]
         <--(QR/code)--
[User Mobile App] --(scan/nhập code)--> [WebAPI] (QrLogin/confirm)
                   <--(poll status)-- [WebApp]
         --(CompleteQrLogin)--> [WebApp: user được login]

// Đăng nhập Mobile từ Web
[WebApp (đã login)] --(tạo QR/code)--> [WebAPI]
         <--(QR/code)--
[User Mobile App] --(scan/nhập code)--> [WebAPI] (WebToMobileQr/confirm)
                   <--(poll status hoặc nhận token luôn)-- [MobileApp]
         --(login thành công)--> [MobileApp]
```
