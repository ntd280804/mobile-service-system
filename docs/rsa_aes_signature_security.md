# Bảo mật API — Mã hóa RSA/AES & Ký số trong hệ thống

## 1. Mã hóa API (2 chiều) sử dụng RSA/AES

### 1.1 Quy trình mã hóa hai chiều (Hybrid Encryption)
- **Client-Server trao đổi public key RSA:**
  - Client lấy public key server: `GET /api/Public/Security/server-public-key`
  - Client đăng ký public key của mình lên server qua: `POST /api/Public/Security/register-client-key`

#### **A. Khi Client gửi dữ liệu lên Server**
- Client:
  - Sinh ngẫu nhiên AES key 256 bit + IV 128 bit
  - Serialize data (JSON, ...), mã hóa bằng AES
  - Ghép key+IV, mã hóa bằng RSA public key của server => `EncryptedKeyBlockBase64`
  - Gửi API:
    - Body = `{ EncryptedKeyBlockBase64, CipherDataBase64 }`
- Server:
  - Dùng private key server để giải mã AES key+IV
  - Dùng AES key+IV giải mã data, xử lý nghiệp vụ

  *Ví dụ API sử dụng:*
    - `POST /api/Public/Customer/LoginSecure` (login bảo mật, mã hóa payload)
    - `POST /api/Admin/Export/CreateExportFromOrderSecure` (mã hóa dữ liệu phiếu xuất kho)
    - `POST /api/Admin/Import/CreateImport...Secure` ...
    - Tất cả sử dụng EncryptedPayload (`EncryptedKeyBlockBase64`, `CipherDataBase64`)

#### **B. Khi Server trả về dữ liệu bảo mật cho Client**
- Server:
  - Sinh ngẫu nhiên AES key + IV
  - Mã hóa dữ liệu trả về bằng AES
  - Ghép key+IV, mã hóa bằng RSA public key của client
  - Trả về hai trường: (1) `EncryptedKeyBlockBase64`, (2) `CipherDataBase64`
- Client:
  - Dùng private key của mình giải mã, lấy AES key+IV
  - Giải mã dữ liệu API response qua AES

  *Ví dụ API sử dụng:*
    - `POST /api/Public/Security/encrypt-for-client`


### 1.2 Minh họa tổng thể luồng 2 chiều

```
(Client)                  (Server)
  |  lấy public-key --->     |
  |<--- gửi public-key      |
==== (request lên server)  ====
  | tạo key+iv, AES data    |
  | RSA encrypt key+iv      |
  | gửi EncryptedPayload    |
  |----------------------->| API .LoginSecure/.CreateExport...Secure
  |  giải mã (RSA->AES)     |
  |  xử lý-nghiệp vụ        |
==== (response về client)  ====
  |                         | tạo key+iv, AES data out
  |                         | RSA encrypt key+iv (public client)
  |<-----------------------| trả EncryptedPayload
  | giải mã (RSA->AES), đọc data clear
```

---

## 2. Ký số tài liệu (Ký số PDF, xác thực số liệu)

### 2.1 Quy trình ký số trong hệ thống
- Dịch vụ `PdfSignatureService` sử dụng:
  - Ký từng block nhỏ (20KB) bằng hàm SIGN_PDF (oracle proc + privateKey người ký)
  - (Hoặc) dùng certificate số (pfx) với GroupDocs.Signature ký PDF trực tiếp với dấu ký đồ họa
  - Private key/cert luôn do người ký quản lý

### 2.2 Luồng ký số PDF (Pseudocode):
```
Client [upload pdf, nhập khóa] ---> Server
Server:
- Chia file .pdf thành nhiều block <= 20KB
- Với mỗi block:
  - Dùng stored proc SIGN_PDF(block, privateKey) => signature block
- Nối signature các block lại
- Gọi UpdateFinalSignature để lưu dấu vết ký vào DB/nghiệp vụ
- Kết quả trả lại client: pdf đã ký số hoặc chữ ký số dạng byte[]
```

### 2.3 Quản lý private key/cert
- Mỗi người dùng/đối tượng đều có file chứa privateKey riêng
- Không lưu private key trên server
- Chứng thư số (pfx) có thể được import lúc ký PDF (GroupDocs)

---

## 3. Ghi chú bảo mật bổ sung
- Mã hóa lai (Hybrid RSA/AES) hai chiều, mọi payload quan trọng đều có thể dùng mã hóa tùy yêu cầu
- Private key (client, server) tuyệt đối không được lưu cùng nơi, không truyền qua mạng
- Digital Signature (ký số) giúp xác minh không thể giả mạo nguồn phát sinh và phục vụ kiểm toán pháp lý
- API cho phép mở rộng mã hóa chi tiết từng giao dịch nghiệp vụ bằng kiến trúc này
