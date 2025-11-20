# Định danh và Xác thực

## Mục lục
1. [Tổng quan định danh](#tổng-quan-định-danh)  
2. [OracleConnectionManager hoạt động thế nào?](#oracleconnectionmanager-hoạt-động-thế-nào)  
   2.1 [Cấu trúc dữ liệu và khóa định danh](#cấu-trúc-dữ-liệu-và-khóa-định-danh)  
   2.2 [Quy trình đăng nhập (CreateConnection)](#quy-trình-đăng-nhập-createconnection)  
   2.3 [Quy trình sử dụng và làm mới session](#quy-trình-sử-dụng-và-làm-mới-session)  
   2.4 [Quy trình đăng xuất (RemoveConnection)](#quy-trình-đăng-xuất-removeconnection)  
3. [Quản lý session theo nền tảng (platform)](#quản-lý-session-theo-nền-tảng-platform)  
4. [Luồng định danh – xác thực cho từng tác nhân](#luồng-định-danh--xác-thực-cho-từng-tác-nhân)  
   4.1 [Nhân viên qua WebApp](#nhân-viên-qua-webapp)  
   4.2 [Khách hàng qua Mobile App](#khách-hàng-qua-mobile-app)  
5. [Ghi chú bảo mật cốt lõi](#ghi-chú-bảo-mật-cốt-lõi)
6. [Luồng đăng nhập Web bằng QR](#luồng-đăng-nhập-web-bằng-qr)

---

## Tổng quan định danh
- Hệ thống sử dụng **Oracle Database** như kho định danh duy nhất, mỗi nhân viên/khách hàng đều có user Oracle riêng.  
- Để truy cập nghiệp vụ, người dùng phải sở hữu **JWT của ứng dụng** và đồng thời **một phiên Oracle mở** được quản lý tại WebAPI.  
- Thành phần trung tâm chịu trách nhiệm trông coi phiên là dịch vụ `OracleConnectionManager`.

## OracleConnectionManager hoạt động thế nào?

### Cấu trúc dữ liệu và khóa định danh
- Lưu trữ trong **`ConcurrentDictionary<(username, platform, sessionId), OracleConnInfo>`**.  
- `username`: user Oracle tương ứng (nhân viên dùng username nội bộ, khách hàng dùng số điện thoại).  
- `platform`: kênh truy cập (`WEB_APP`, `MOBILE_APP`, `POSTMAN`, …).  
- `sessionId`: GUID do ứng dụng tạo, cũng được set vào Oracle bằng `DBMS_SESSION.SET_IDENTIFIER`.  
- `OracleConnInfo`: giữ `OracleConnection` + `OracleSid` để tiện ghi log/kill session.  
- Nhờ khóa 3 thành phần, hệ thống cho phép cùng một người đăng nhập nhiều nền tảng nhưng **chỉ 1 phiên/ platform**.

### Quy trình đăng nhập (CreateConnection)
1. **Tiếp nhận thông tin** sau khi thủ tục `LOGIN_EMPLOYEE`/`LOGIN_CUSTOMER` xác thực mật khẩu thành công.  
2. **RemoveAllConnections(username, platform)** để đảm bảo không còn phiên cũ trên cùng nền tảng; đồng thời gửi SignalR “ForceLogout” tới các client đang dùng session cũ.  
3. **Dựng connection string** từ template và credential nhận được, mở `OracleConnection`.  
4. **Ghi dấu platform & session** vào Oracle:
   ```sql
   DBMS_APPLICATION_INFO.SET_MODULE(module_name => 'WebAPI-{platform}', client_info => sessionId);
   DBMS_SESSION.SET_IDENTIFIER(sessionId);
   ```
5. **Lấy SID** thật bằng `SELECT SYS_CONTEXT('USERENV','SID') FROM DUAL`.  
6. **Ghi vào dictionary** với khóa `(username, platform, sessionId)` → lúc này coi như login hoàn tất.  
7. **Phát JWT** (WebAPI) hoặc token REST (Mobile) để client kèm trong các request tiếp theo.

### Quy trình sử dụng và làm mới session
- Mỗi request `[Authorize]` phải gửi:
  - Header JWT (`Authorization: Bearer ...`),  
  - `X-Oracle-Username`, `X-Oracle-Platform`, `X-Oracle-SessionId`.  
- `OracleSessionHelper` đọc các header trên và gọi `GetConnectionIfExists`.  
- `GetConnectionIfExists` sẽ:
  1. Tra dictionary theo khóa 3 thành phần.  
  2. Nếu tìm thấy, chạy `SELECT 1 FROM DUAL` để chắc chắn session Oracle còn mở.  
  3. Nếu query thành công → trả lại `OracleConnection` để stored procedure nghiệp vụ sử dụng.  
  4. Nếu thất bại (timeout, killed) → tự động `RemoveConnection` và trả `null`, khiến API phản hồi 401 “Oracle session expired”.
- **Làm mới session:** khi người dùng đăng nhập lại, `CreateConnection` sẽ tạo session mới, override khóa cũ, người dùng cũ lập tức bị buộc logout trên platform tương ứng.

### Quy trình đăng xuất (RemoveConnection)
1. Khi người dùng bấm “Đăng xuất” hoặc JWT hết hạn, WebApp/Mobile gọi endpoint logout.  
2. API lấy thông tin header và gọi `RemoveConnection(username, platform, sessionId)`.  
3. Bên trong hàm:
   - Xóa entry khỏi dictionary.  
   - Đóng connection, `ClearPool`, giải phóng tài nguyên.  
   - Thông báo SignalR `ForceLogout` cho group `sessionId` để client điều hướng về trang đăng nhập.  
4. Từ đó mọi request dùng sessionId này sẽ bị từ chối cho đến khi đăng nhập lại.

## Quản lý session theo nền tảng (platform)
- **Mục tiêu:** ngăn việc chia sẻ tài khoản trong cùng môi trường (ví dụ nhiều người dùng chung tài khoản Admin tại quầy).  
- Với cơ chế “1 platform = 1 session”, khi cùng `username` đăng nhập lại trên cùng `platform`, phiên cũ bị buộc logout ngay.  
- Tuy nhiên, người dùng vẫn có thể đăng nhập song song giữa các nền tảng khác nhau:
  - ADMIN01 đăng nhập WebApp (`platform=WEB_APP`) và Mobile (`platform=MOBILE_APP`) cùng lúc → hợp lệ.  
  - Khi ADMIN01 mở thêm tab WebApp → tab cũ nhận SignalR yêu cầu đăng nhập lại.  
- Platform cũng giúp truy vết: `DBMS_APPLICATION_INFO.SET_MODULE` ghi rõ nguồn truy cập, hỗ trợ audit.

## Luồng định danh – xác thực cho từng tác nhân

### Nhân viên qua WebApp
1. **Định danh**: mỗi nhân viên được tạo bằng thủ tục `REGISTER_EMPLOYEE`, sở hữu username Oracle riêng + role (`ROLE_ADMIN`, `ROLE_THUKHO`, …).  
2. **Đăng nhập**:
   - Gửi username/password → `APP.LOGIN_EMPLOYEE`.  
   - Nếu thành công, WebAPI gọi `CreateConnection`, trả về JWT + `sessionId`.  
   - WebApp lưu JWT trong cookie bảo mật, sessionId trong session server.  
3. **Sử dụng**: mọi request AJAX tới WebAPI đều đính kèm JWT + các header Oracle.  
4. **Đăng xuất**: người dùng chọn Logout → WebApp gọi endpoint logout, API `RemoveConnection`, cookie JWT bị xóa, client chuyển về trang login.

### Khách hàng qua Mobile App
1. **Định danh**: số điện thoại là username duy nhất; đăng ký bằng `REGISTER_CUSTOMER` (tạo luôn user Oracle).  
2. **Đăng nhập**:
   - App gửi phone/password → `APP.LOGIN_CUSTOMER`.  
   - WebAPI tạo session Oracle với `platform=MOBILE_APP`, trả JWT + sessionId.  
   - App lưu vào secure storage.  
3. **Đăng xuất/timeout**: khi người dùng bấm logout hoặc JWT hết hạn, app gọi API logout → `RemoveConnection`; nếu app tắt đột ngột, `CleanupDeadConnections` sẽ dọn session khi phát hiện không còn sống.

## Ghi chú bảo mật cốt lõi
- **Hai lớp xác thực**: JWT xác thực ứng dụng, Oracle session xác thực dữ liệu; thiếu một trong hai → bị chặn.  
- **Hash mật khẩu**: dùng `HASH_PASSWORD`, `HASH_PASSWORD_20CHARS`; mật khẩu không bao giờ lưu dạng rõ.  
- **SignalR Force Logout**: đảm bảo thông báo real-time khi phiên bị chiếm quyền hoặc đăng nhập nơi khác.  
- **Audit và VPD**: các chính sách trong `SQL/Audit` & `SQL/VPD` ghi lại thao tác, lọc dữ liệu theo user để tránh lộ thông tin định danh.

---

## Luồng đăng nhập Web bằng QR

### Mục tiêu

- Cho phép **khách hàng đang đăng nhập trên Mobile App** sử dụng app để **đăng nhập WebApp (Customer)** mà không cần nhập mật khẩu trên trình duyệt.
- Đảm bảo sau QR login, WebApp vẫn sử dụng đầy đủ:
  - **JWT** cho xác thực ứng dụng.
  - **Oracle session** (do `OracleConnectionManager` quản lý) cho nghiệp vụ.

### Thành phần tham gia

- **WebApp (MVC)**:
  - View: trang `Public/Customer/Login.cshtml`.
  - Controller: `Public/CustomerController` (action `CompleteQrLogin`).
  - Helper: `OracleClientHelper` (set header khi gọi WebAPI).
- **WebAPI**:
  - `Public/QrLoginController` – tạo/confirm/poll phiên QR.
  - `Public/CustomerController` – endpoint `qr-login` (proxy customer).
  - `CustomerQrLoginService` – đăng nhập proxy cho QR.
  - `QrLoginStore` – lưu phiên QR in-memory (TTL 2 phút).
  - `OracleConnectionManager` – quản lý connection Oracle (có hỗ trợ proxy).
- **Mobile App**:
  - `HomeScreen` – icon QR mở bottom-sheet nhập code.
  - `QrWebLoginSheet` – màn hình nhập mã QR.
  - `ApiService.confirmQrLogin(code)` – gọi API xác nhận.

---

### 1. WebApp – tạo QR và hiển thị mã

**Endpoint WebAPI**  
- `POST /api/Public/QrLogin/create`  
- Controller: `QrLoginController.Create()`  
- Auth: `AllowAnonymous`

**Luồng**  
1. Người dùng mở trang `Customer/Login` trên WebApp.
2. JS trong `Login.cshtml` gọi AJAX tới `/api/Public/QrLogin/create`.
3. `QrLoginStore.CreateSession()` tạo một phiên QR mới với:
   - `Id` (qrLoginId): GUID.
   - `Code`: chuỗi random (A–Z, 2–9) dài 8 ký tự.
   - `ExpiresAtUtc = Now + 2 phút`.
   - `Status = Pending`.
4. WebApp nhận về `qrLoginId`, `code`, `expiresAtUtc` và:
   - Vẽ QR image từ `code` (dùng dịch vụ QR public).
   - Hiển thị `code` dạng text rõ ràng.
   - Copy `code` sang ô “Nhập mã trên app” (readonly).
5. JS bắt đầu **poll** trạng thái bằng `/api/Public/QrLogin/status/{qrLoginId}` mỗi 2–3 giây.

---

### 2. Mobile – nhập code và confirm

**Phương thức trên Mobile**  
- `ApiService.confirmQrLogin(String code)`  
- Gửi request:
  ```http
  POST /api/Public/QrLogin/confirm
  Authorization: Bearer <JWT-mobile>
  Content-Type: application/json

  { "code": "<CODE-TRÊN-WEB>" }
  ```

**Luồng trên app**  
1. Khách hàng đã đăng nhập vào Mobile App (đã có JWT + sessionId mobile trong secure storage).
2. Tại `HomeScreen`, user bấm icon QR → mở `QrWebLoginSheet`.
3. User nhập `code` đang hiển thị trên Web, bấm “Xác nhận”.
4. `confirmQrLogin` gửi request kèm:
   - JWT trong `Authorization`.
   - `X-Oracle-Username`, `X-Oracle-SessionId`, `X-Oracle-Platform="MOBILE"` từ interceptor.
5. Nếu WebAPI trả `success=true` → app chỉ hiển thị thông báo “Đã xác nhận đăng nhập Web …”. App không cần token mới.

---

### 3. WebAPI – xác nhận QR và tạo session Web

**Endpoint**  
- `POST /api/Public/QrLogin/confirm`  
- Controller: `QrLoginController.Confirm(QrLoginConfirmRequest request)`  
- Auth: `[Authorize(AuthenticationSchemes = "Bearer")]` (bắt buộc JWT mobile)

**Luồng xử lý**  
1. Kiểm tra input: `request.Code` không rỗng.
2. Tra `QrLoginStore.GetByCode(code)`:
   - Nếu không tồn tại, hết hạn, hoặc `Status != Pending` → trả 400 “Code không hợp lệ hoặc đã hết hạn.”
3. Đọc username từ JWT mobile:
   - `username = User.Identity.Name` (được map từ khách hàng; thường là số điện thoại).
4. Gọi dịch vụ đăng nhập proxy:
   ```csharp
   var loginResult = _customerQrLoginService.LoginViaProxy(username, "WEB");
   ```
5. `CustomerQrLoginService.LoginViaProxy`:
   - Đọc cấu hình:
     - `QrLogin.ProxyUser` – user proxy Oracle (ví dụ `App`).
     - `QrLogin.ProxyPassword` – mật khẩu của proxy user.
     - `QrLogin.DefaultPlatform` – platform cho QR (ví dụ `WEB`).
   - Tạo `sessionId = Guid.NewGuid().ToString()` cho phiên Web.
   - Mở connection Oracle qua `OracleConnectionManager.CreateConnection`:
     ```csharp
     var conn = _connManager.CreateConnection(
         username,          // logical username = số điện thoại
         proxyPassword,
         resolvedPlatform,  // WEB_QR
         sessionId,
         proxy: true        // dùng proxy user
     );
     ```
   - `CreateConnection` sẽ:
     - Gọi `RemoveAllConnections(username, platform)` để logout mọi phiên cũ cùng platform.
     - Build Oracle username thực: `ProxyUser[username]` nếu `proxy=true`.
     - Mở `OracleConnection` với user/password trên.
     - Set:
       ```sql
       DBMS_APPLICATION_INFO.SET_MODULE('WebAPI-{platform}', sessionId);
       DBMS_SESSION.SET_IDENTIFIER(sessionId);
       ```
     - Lưu connection vào dictionary với key `(username, platform, sessionId)` (username logic).
   - Sau khi có connection:
     - Gọi `APP.APP_CTX_PKG.set_role('ROLE_KHACHHANG')`.
     - Gọi `APP.APP_CTX_PKG.set_customer(username)`.
     - Sinh JWT Web dành cho khách hàng:
       ```csharp
       var token = _jwtHelper.GenerateToken(username, "ROLE_KHACHHANG", sessionId);
       ```
   - Trả về `CustomerLoginResult`:
     - `Username = username`
     - `Roles = "ROLE_KHACHHANG"`
     - `Result = "SUCCESS"`
     - `SessionId = sessionId`
     - `Token = token`
6. `QrLoginController.Confirm` cập nhật phiên QR trong `QrLoginStore`:
   - `Status = Confirmed`
   - `Username = loginResult.Username`
   - `Roles = loginResult.Roles`
   - `WebToken = loginResult.Token`
   - `WebSessionId = loginResult.SessionId`
7. Trả về JSON:
   ```json
   { "success": true, "data": "CONFIRMED" }
   ```

Kết quả: phía WebAPI đã chuẩn bị xong **JWT + Oracle session** dành riêng cho Web, gắn với code QR vừa được xác nhận.

---

### 4. WebApp – poll trạng thái và hoàn tất đăng nhập

**Endpoint WebAPI**  
- `GET /api/Public/QrLogin/status/{qrLoginId}`  
- Controller: `QrLoginController.Status(string qrLoginId)`  
- Auth: `AllowAnonymous`

**Luồng trên WebApp**  
1. JS trên `Login.cshtml` lưu `qrLoginId` từ bước tạo QR.
2. Đặt `setInterval` mỗi 2–3 giây:
   - Gọi `GET status/{qrLoginId}`.
   - Đọc `payload.data`:
     - `status` – Pending / Confirmed / Expired (hoặc 0 / 1 / 2).
     - `username`, `roles`, `webToken`, `webSessionId` – khi Confirmed.
3. Xử lý trạng thái:
   - **Pending** → cập nhật countdown, không làm gì thêm.
   - **Expired** → dừng poll, hiển thị “QR đã hết hạn, nhấn Tạo QR mới”.
   - **Confirmed**:
     - Dừng poll và countdown.
     - Gọi AJAX tới WebApp:
       ```http
       POST /Public/Customer/CompleteQrLogin
       RequestVerificationToken: <anti-forgery-token>
       Content-Type: application/json

       {
         "username": "<username>",
         "roles": "<roles>",
         "token": "<webToken>",
         "sessionId": "<webSessionId>"
       }
       ```

**Action WebApp**  
- `CustomerController.CompleteQrLogin(QrLoginCompleteDto dto)`:
  1. Validate dữ liệu (`Username`, `Token` không rỗng).
  2. Ghi vào session:
     ```csharp
     HttpContext.Session.SetString("CJwtToken", dto.Token);
     HttpContext.Session.SetString("CUsername", dto.Username);
     HttpContext.Session.SetString("CRole", dto.Roles ?? "");
     HttpContext.Session.SetString("CPlatform", "WEB");
     HttpContext.Session.SetString("CSessionId", dto.SessionId ?? Guid.NewGuid().ToString());
     ```
  3. Trả về JSON `{ redirect = Url.Action("Index", "Home", new { area = "Public" }) }`.
  4. JS nhận redirect, báo “Đăng nhập bằng QR thành công” rồi chuyển sang trang Home.

---

### 5. WebApp – gọi API nghiệp vụ sau QR login

Sau khi `CompleteQrLogin` thiết lập session, các controller Public sử dụng **cùng flow** như login bằng mật khẩu.

**Helper**  
- `OracleClientHelper.TrySetHeaders(HttpClient httpClient, out IActionResult redirectToLogin, bool isAdmin = false)`:
  1. Đọc:
     - `CJwtToken`, `CUsername`, `CPlatform`, `CSessionId` từ session.
  2. Nếu thiếu → trả redirect về trang login customer.
  3. Nếu đủ, set header cho `HttpClient`:
     - `Authorization: Bearer {CJwtToken}`.
     - `X-Oracle-Username: CUsername`.
     - `X-Oracle-Platform: CPlatform` (lúc này là `"WEB"`).
     - `X-Oracle-SessionId: CSessionId`.

**Phía WebAPI**  
- Các API nghiệp vụ Public gọi `OracleSessionHelper.GetConnectionOrUnauthorized`:
  - Đọc các header `X-Oracle-*`.
  - Gọi `OracleConnectionManager.GetConnectionIfExists(username, platform, sessionId)`:
    - Nếu tìm thấy connection → trả `OracleConnection` để gọi stored procedure nghiệp vụ.
    - Nếu không → trả `401 Unauthorized` với message “Oracle session expired. Please login again.”.

Như vậy, sau khi login bằng QR, WebApp có **JWT Web** và **Oracle session** tương đương login bằng username/password, chỉ khác là quá trình xác thực được ủy quyền qua Mobile App và cơ chế proxy trên Oracle.

---

> *Tài liệu này bổ sung mô tả chi tiết luồng đăng nhập Web bằng QR cho khách hàng, xây dựng trên cơ chế định danh và quản lý session sẵn có của hệ thống.*

