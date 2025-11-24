# Luồng Hoạt Động Chức Năng Ký Số PDF

## Tổng Quan

Hệ thống hỗ trợ ký số PDF cho các loại hóa đơn:
- **Hóa đơn nhập kho** (Import Invoice)
- **Hóa đơn xuất kho** (Export Invoice)  
- **Hóa đơn bán hàng** (Invoice)

Chức năng ký số sử dụng **GroupDocs.Signature** với certificate số (PFX) để tạo chữ ký số trực tiếp trên PDF với dấu ký đồ họa.

---

## 1. Luồng Hoạt Động Tổng Thể

### 1.1 Sơ Đồ Luồng

```
┌─────────────┐
│  Web App    │
│  (Client)   │
└──────┬──────┘
       │
       │ 1. User nhập thông tin + upload PFX Certificate từ CA
       │
       ▼
┌─────────────────────────────────────────────────────────┐
│  POST /api/admin/import/post-secure                     │
│  POST /api/admin/export/create-secure                   │
│  (Payload được mã hóa RSA/AES)                          │
└──────┬──────────────────────────────────────────────────┘
       │
       │ 2. WebAPI nhận request, giải mã payload
       │
       ▼
┌─────────────────────────────────────────────────────────┐
│  WebAPI Controller                                       │
│  - ImportController/ExportController                    │
│  - Xử lý nghiệp vụ (tạo STOCK_IN/STOCK_OUT, INVOICE)   │
└──────┬──────────────────────────────────────────────────┘
       │
       │ 3. Tạo PDF chưa ký
       │
       ▼
┌─────────────────────────────────────────────────────────┐
│  InvoicePdfService                                       │
│  - GenerateImportInvoicePdf()                           │
│  - GenerateExportInvoicePdf()                           │
│  - GenerateInvoicePdf()                                 │
└──────┬──────────────────────────────────────────────────┘
       │
       │ 4. Sử dụng PFX Certificate từ CA
       │
       ▼
┌─────────────────────────────────────────────────────────┐
│  PdfSignatureService                                     │
│  - SignPdfWithDigitalCertificate()                       │
│  (PFX certificate được cung cấp từ client)              │
└──────┬──────────────────────────────────────────────────┘
       │
       │ 5. Ký PDF với GroupDocs.Signature
       │
       ▼
┌─────────────────────────────────────────────────────────┐
│  GroupDocs.Signature                                    │
│  - Nhúng chữ ký số vào PDF                              │
│  - Tạo visual signature box                            │
└──────┬──────────────────────────────────────────────────┘
       │
       │ 6. Lưu PDF đã ký vào Database (BLOB)
       │
       ▼
┌─────────────────────────────────────────────────────────┐
│  Oracle Database                                         │
│  - UPDATE_STOCKIN_PDF                                   │
│  - UPDATE_STOCKOUT_PDF                                   │
│  - UPDATE_INVOICE_PDF                                   │
└──────┬──────────────────────────────────────────────────┘
       │
       │ 7. Trả về PDF đã ký cho Client
       │
       ▼
┌─────────────┐
│  Web App    │
│  (Download) │
└─────────────┘
```

---

## 2. Chi Tiết Từng Bước

### 2.1 Web App (Client Side)

#### A. Import Stock với Ký Số PDF

**File:** `WebApp/Areas/Admin/Controllers/ImportController.cs`

**Luồng:**
1. User nhập thông tin import (items, note) và upload **PFX Certificate** (file .pfx/.p12) cùng mật khẩu
2. JavaScript convert PFX file sang Base64:
   ```javascript
   // Trong View: Import/Create.cshtml
   document.getElementById("pfx-file").addEventListener("change", function (e) {
       const file = e.target.files[0];
       const reader = new FileReader();
       reader.onload = function () {
           const base64 = reader.result.split(',')[1] || reader.result;
           document.getElementById("CertificatePfxBase64").value = base64;
       };
       reader.readAsDataURL(file);  // Đọc file binary
   });
   ```
3. WebApp mã hóa payload bằng **RSA/AES Hybrid Encryption**:
   ```csharp
   // Lấy server public key
   var keyResp = await _httpClient.GetFromJsonAsync<ApiResponse<string>>(
       "api/public/security/server-public-key");
   
   // Mã hóa payload (bao gồm CertificatePfxBase64 và CertificatePassword)
   var encrypted = EncryptHelper.HybridEncrypt(json, keyResp.Data);
   ```

4. Gửi request đến WebAPI:
   ```csharp
   POST /api/admin/import/post-secure
   Body: {
       encryptedKeyBlockBase64: "...",
       cipherDataBase64: "..."
   }
   ```

5. Nhận response:
   - **Thành công:** Nhận file PDF đã ký (`application/pdf`)
   - **Lỗi:** Nhận JSON error message

#### B. Export Stock (Tạo Invoice) với Ký Số PDF

**File:** `WebApp/Areas/Admin/Controllers/ExportController.cs`

**View:** `WebApp/Areas/Admin/Views/Order/Index.cshtml` (Modal form)

**Luồng:**
1. User click "Hoàn thành" trên Order → Mở modal
2. User upload **PFX Certificate** (file .pfx/.p12) và nhập mật khẩu
3. JavaScript convert PFX file sang Base64 và submit form:
   ```javascript
   function processKeyAndSubmit(orderId) {
       const fileInput = document.getElementById(`pfxFile-${orderId}`);
       const passwordInput = document.getElementById(`pfxPassword-${orderId}`);
       const contentInput = document.getElementById(`pfxContent-${orderId}`);
       
       const reader = new FileReader();
       reader.onload = function(e) {
           const base64 = e.target.result.split(',')[1] || e.target.result;
           contentInput.value = base64;
           form.submit();
       };
       reader.readAsDataURL(file);
   }
   ```
4. WebApp mã hóa payload và gửi đến: `POST /api/admin/export/create-secure`
5. Tạo cả Export Invoice và Invoice PDF đã ký

---

### 2.2 WebAPI (Server Side)

#### A. Import Controller

**File:** `WebAPI/Areas/Admin/Controllers/ImportController.cs`

**Endpoint:** `POST /api/admin/import/post-secure`

**Luồng xử lý:**

1. **Giải mã payload:**
   ```csharp
   // Decrypt RSA/AES
   byte[] keyBlock = rsaKeyService.DecryptKeyBlock(...);
   // Giải mã AES để lấy ImportStockDto
   ```

2. **Validate PFX Certificate:**
   ```csharp
   // Hệ thống yêu cầu PFX từ CA, không cho phép tạo self-signed
   if (!hasProvidedPfx) {
       throw new InvalidOperationException(
           "Không thể tạo PFX hợp lệ khi chỉ có private key. " +
           "Vui lòng cung cấp PFX do CA cấp.");
   }
   ```

3. **Xử lý nghiệp vụ:**
   - Tạo `STOCK_IN` record trong database
   - Tạo các `STOCK_IN_ITEM`
   - Tạo các `PART` records
   - **Lưu ý:** Không cần ký số nghiệp vụ riêng vì đã dùng PFX để ký PDF

4. **Sử dụng PFX Certificate:**
   ```csharp
   // Sử dụng PFX từ client (bắt buộc)
   byte[] pfxBytes = Convert.FromBase64String(dto.CertificatePfxBase64);
   string pfxPassword = dto.CertificatePassword;
   ```

4. **Tạo và ký PDF:**
   ```csharp
   var signedPdf = _invoicePdfService.GenerateImportInvoicePdfAndSignWithCertificate(
       pdfDto, pfxBytes, pfxPassword, ...);
   ```

5. **Response:**
   - Trả về file PDF đã ký: `File(signedPdf, "application/pdf", fileName)`

#### B. Export Controller

**File:** `WebAPI/Areas/Admin/Controllers/ExportController.cs`

**Endpoint:** `POST /api/admin/export/create-secure`

**Luồng tương tự Import**, nhưng:
- Tạo `STOCK_OUT` → `INVOICE`
- Ký cả Export Invoice và Invoice PDF
- Trả về Invoice PDF (ưu tiên)

---

### 2.3 InvoicePdfService

**File:** `WebAPI/Services/InvoicePdfService.cs`

#### A. Tạo PDF Chưa Ký

**Methods:**
- `GenerateImportInvoicePdf()` - Tạo PDF hóa đơn nhập kho
- `GenerateExportInvoicePdf()` - Tạo PDF hóa đơn xuất kho
- `GenerateInvoicePdf()` - Tạo PDF hóa đơn bán hàng

**Công nghệ:** QuestPDF

**Nội dung PDF:**
- Header: Logo công ty, thông tin công ty (MST, địa chỉ, SĐT, Email)
- Thông tin phiếu/hóa đơn: Mã, ngày, nhân viên, khách hàng
- Bảng chi tiết: Items (linh kiện) và Services (dịch vụ)
- Tổng cộng
- Khu vực "Chữ ký số:" (chờ ký)

#### B. Ký PDF với Certificate

**Methods:**
- `GenerateImportInvoicePdfAndSignWithCertificate()`
- `GenerateExportInvoicePdfAndSignWithCertificate()`
- `GenerateInvoicePdfAndSignWithCertificate()`

**Luồng:**
1. Tạo PDF chưa ký: `GenerateImportInvoicePdf(...)`
2. Tính toán vị trí chữ ký: `CalculateSignaturePosition()`
3. Gọi `PdfSignatureService.SignPdfWithDigitalCertificate()`
4. Lưu PDF đã ký vào DB: `PdfSignatureService.UpdateFinalPDF()`
5. Trả về PDF bytes đã ký

---

### 2.4 PdfSignatureService

**File:** `WebAPI/Services/PdfSignatureService.cs`

#### A. Ký PDF với Digital Certificate

**Method:** `SignPdfWithDigitalCertificate()`

```csharp
public byte[] SignPdfWithDigitalCertificate(
    byte[] pdfBytes, 
    byte[] certificatePfxBytes, 
    string certificatePassword, 
    Action<DigitalSignOptions> configureAppearance = null)
```

**Quy trình:**
1. **Load PDF và Certificate:**
   ```csharp
   using (var pdfStream = new MemoryStream(pdfBytes))
   using (var certStream = new MemoryStream(certificatePfxBytes))
   using (var signature = new Signature(pdfStream))
   ```

2. **Cấu hình Digital Sign Options:**
   ```csharp
   var options = new DigitalSignOptions(certStream) {
       Password = certificatePassword,
       Reason = "Approved",
       Location = "Việt Nam",
       PagesSetup = new PagesSetup { LastPage = true },
       Width = 170,
       Height = 90,
       HorizontalAlignment = HorizontalAlignment.Right,
       VerticalAlignment = VerticalAlignment.Bottom,
       Margin = new Padding(20),
       Appearance = new PdfDigitalSignatureAppearance() {
           ReasonLabel = "Lý do",
           LocationLabel = "Địa điểm",
           DigitalSignedLabel = "Ký bởi",
           DateSignedAtLabel = "Ngày"
       }
   };
   ```

3. **Customize appearance (nếu có):**
   ```csharp
   configureAppearance?.Invoke(options);
   // Ví dụ: Set absolute position
   options.Left = left;
   options.Top = top;
   ```

4. **Ký PDF:**
   ```csharp
   using (var output = new MemoryStream()) {
       signature.Sign(output, options);
       return output.ToArray();
   }
   ```

**Kết quả:** PDF bytes đã được ký số với visual signature box

#### B. Lưu PDF Đã Ký vào Database

**Method:** `UpdateFinalPDF()`

```csharp
public void UpdateFinalPDF(
    string procedureName, 
    byte[] PDF, 
    Action<OracleCommand> configureParameters)
```

**Quy trình:**
1. Lấy Oracle connection của user hiện tại
2. Tạo stored procedure command:
   ```csharp
   using (var cmd = new OracleCommand(procedureName, connection)) {
       cmd.CommandType = CommandType.StoredProcedure;
       configureParameters(cmd); // Bind p_stockin_id, p_invoice_id, etc.
       
       // Bind PDF as BLOB
       cmd.Parameters.Add("p_signature", OracleDbType.Blob, ...).Value = PDF;
       cmd.ExecuteNonQuery();
   }
   ```

**Stored Procedures:**
- `APP.UPDATE_STOCKIN_PDF(p_stockin_id, p_signature BLOB)`
- `APP.UPDATE_STOCKOUT_PDF(p_stockout_id, p_signature BLOB)`
- `APP.UPDATE_INVOICE_PDF(p_invoice_id, p_signature BLOB)`

---

## 3. Cấu Trúc API Response

### 3.1 WebAPI Response

#### A. Thành Công (Ký PDF)

**Content-Type:** `application/pdf`

**Response Body:** PDF file bytes (binary)

**Headers:**
```
Content-Type: application/pdf
Content-Disposition: attachment; filename="ImportInvoice_123.pdf"
```

**Ví dụ trong Controller:**
```csharp
return File(signedPdf, "application/pdf", $"ImportInvoice_{stockInId}.pdf");
```

#### B. Lỗi

**Content-Type:** `application/json`

**Response Body:**
```json
{
    "Message": "Oracle Error - Transaction rolled back",
    "ErrorCode": 12345,
    "Error": "ORA-xxxxx: ...",
    "StackTrace": "..."
}
```

Hoặc:
```json
{
    "Message": "General Error",
    "Error": "Invalid certificate PFX base64."
}
```

Hoặc khi thiếu PFX:
```json
{
    "Message": "General Error",
    "Error": "Không thể tạo PFX hợp lệ khi chỉ có private key. Vui lòng cung cấp PFX do CA cấp."
}
```

### 3.2 WebApp Response Handling

**File:** `WebApp/Areas/Admin/Controllers/ImportController.cs`

```csharp
var response = await _httpClient.SendAsync(requestMessage);

if (response.IsSuccessStatusCode) {
    var mediaType = response.Content.Headers.ContentType?.MediaType ?? "";
    
    // Nếu là PDF → download file
    if (string.Equals(mediaType, "application/pdf", ...)) {
        var stream = await response.Content.ReadAsStreamAsync();
        return File(stream, "application/pdf", fileName);
    }
    // Nếu là JSON → hiển thị message
    else {
        var json = await response.Content.ReadFromJsonAsync<...>();
        TempData["Success"] = "Import thành công";
        return RedirectToAction(nameof(Index));
    }
}
```

---

## 4. Mô Hình Khóa và Định Dạng Tệp

### 4.0 Cấu Trúc File PKCS#12 (.p12/.pfx)

File PKCS#12 (`.p12` hoặc `.pfx`) là một container nhị phân chứa:

1. **Chứng thư X.509 (Public Certificate)**
   - Tương đương với file `.crt` hoặc `.cer`
   - Chứa thông tin công khai: Subject, Issuer, Serial Number, Validity Period
   - Có thể verify bằng public key

2. **Khóa riêng (Private Key)**
   - Tương đương với file `.key`
   - RSA hoặc ECC private key
   - Được mã hóa và bảo vệ bằng password trong PFX

3. **Chuỗi chứng thư (Certificate Chain)**
   - Intermediate CA certificates (nếu có)
   - Root CA certificate (nếu có)
   - Giúp xây dựng trust chain để verify certificate

#### Trong Triển Khai Hệ Thống

**Không lưu file rời:**
- Hệ thống **không** lưu riêng file `.crt` và `.key` trên đĩa
- Tất cả thông tin được đóng gói trong file `.pfx/.p12` duy nhất
- PFX được upload từ client, chỉ tồn tại trong bộ nhớ khi xử lý

**Xử lý trong bộ nhớ:**
```csharp
// Load PFX từ Base64 (từ client upload)
byte[] pfxBytes = Convert.FromBase64String(dto.CertificatePfxBase64);
string pfxPassword = dto.CertificatePassword;

// Giải nén PFX để lấy certificate và private key
using (var cert = new X509Certificate2(pfxBytes, pfxPassword))
{
    // cert chứa cả public certificate và private key
    // Có thể truy cập:
    // - cert.PublicKey: Public key
    // - cert.HasPrivateKey: Kiểm tra có private key (phải = true)
    // - cert.Subject: Thông tin chủ thể (CN=...)
    // - cert.Issuer: Thông tin CA
    // - cert.NotBefore / NotAfter: Thời hạn hiệu lực
}
```

**Khái niệm logic:**
- `invoice.crt` và `invoice.key` chỉ là khái niệm logic
- Trong thực tế, cả hai được đóng gói trong file `.pfx`
- Khi cần, hệ thống extract từ PFX trong bộ nhớ (không ghi ra đĩa)
- GroupDocs.Signature nhận trực tiếp PFX bytes và password

**Lợi ích:**
- ✅ **Bảo mật:** Private key không bao giờ tồn tại dạng file rời trên server
- ✅ **Tiện lợi:** Chỉ cần quản lý một file `.pfx` thay vì nhiều file
- ✅ **An toàn:** PFX được mã hóa bằng password, chỉ giải nén khi cần dùng
- ✅ **Không lưu trữ:** PFX chỉ tồn tại trong bộ nhớ khi xử lý, không lưu trên đĩa

---

## 5. Tạo PFX Certificate

### 5.1 Tạo PFX Certificate bằng OpenSSL

**Lưu ý:** Hệ thống **yêu cầu PFX certificate từ CA** (Certificate Authority), không hỗ trợ tạo self-signed certificate từ private key.

#### Bước 1: Tạo Private Key và Certificate Request

```bash
openssl req -x509 -newkey rsa:2048 -keyout private.key -out certificate.crt -days 365 -nodes
```

**Giải thích:**
- `req -x509`: Tạo self-signed certificate (hoặc có thể dùng để tạo CSR)
- `-newkey rsa:2048`: Tạo RSA key 2048 bit
- `-keyout private.key`: File chứa private key
- `-out certificate.crt`: File certificate
- `-days 365`: Valid 365 ngày
- `-nodes`: Không mã hóa private key (hoặc bỏ `-nodes` để có password)

**Hoặc tạo CSR để gửi CA:**
```bash
openssl req -new -newkey rsa:2048 -keyout private.key -out request.csr
```

#### Bước 2: Tạo PFX từ Private Key và Certificate

```bash
openssl pkcs12 -export -inkey private.key -in certificate.crt -out personal.pfx
```

**Giải thích:**
- `pkcs12 -export`: Export sang định dạng PKCS#12 (PFX)
- `-inkey private.key`: File private key
- `-in certificate.crt`: File certificate (từ CA hoặc self-signed)
- `-out personal.pfx`: File PFX output

**Khi chạy lệnh này, OpenSSL sẽ hỏi:**
- `Enter Export Password:` → Nhập mật khẩu bảo vệ PFX (cần nhớ để dùng khi ký PDF)

#### Bước 3: Sử dụng PFX trong hệ thống

1. Upload file `personal.pfx` lên Web App
2. Nhập mật khẩu PFX đã tạo ở bước 2
3. Hệ thống sẽ dùng PFX này để ký số PDF

### 5.2 Từ PFX Certificate Có Sẵn (từ CA)

**Input từ Client:**
- `CertificatePfxBase64`: PFX file đã encode Base64
- `CertificatePassword`: Mật khẩu PFX

**Xử lý:**
```csharp
byte[] pfxBytes = Convert.FromBase64String(dto.CertificatePfxBase64);
string pfxPassword = dto.CertificatePassword;
```

**Lưu ý:**
- PFX nên là certificate từ CA (Certificate Authority) để đảm bảo tính pháp lý
- GroupDocs.Signature sẽ validate certificate khi ký
- Hệ thống chấp nhận cả self-signed PFX (tạo bằng OpenSSL) nhưng khuyến nghị dùng từ CA

### 5.3 Lưu Ý Quan Trọng

⚠️ **Hệ thống KHÔNG cho phép tạo self-signed certificate từ private key.**

- Nếu chỉ có private key mà không có PFX, hệ thống sẽ báo lỗi:
  ```
  "Không thể tạo PFX hợp lệ khi chỉ có private key. Vui lòng cung cấp PFX do CA cấp."
  ```

- **Yêu cầu:** Người dùng phải cung cấp PFX certificate hợp lệ từ CA (Certificate Authority)

### 5.4 Extract Public Certificate từ PFX

**Nếu cần lấy public certificate để verify hoặc lưu trữ:**

```csharp
using (var cert = new X509Certificate2(pfxBytes, pfxPassword))
{
    // Lấy public certificate (chỉ certificate, không có private key)
    byte[] publicCertBytes = cert.Export(X509ContentType.Cert);
    string publicCertBase64 = Convert.ToBase64String(publicCertBytes);
    
    // Hoặc export sang PEM format
    string publicCertPem = "-----BEGIN CERTIFICATE-----\n" +
                          Convert.ToBase64String(publicCertBytes) +
                          "\n-----END CERTIFICATE-----";
    
    // Lấy public key (RSA hoặc ECC)
    var publicKey = cert.GetRSAPublicKey(); // Hoặc GetECDsaPublicKey() cho ECC
    byte[] publicKeyBytes = publicKey.ExportSubjectPublicKeyInfo();
    string publicKeyBase64 = Convert.ToBase64String(publicKeyBytes);
}
```

**Lưu ý:**
- `Export(X509ContentType.Cert)` chỉ export public certificate, **không** bao gồm private key
- Private key chỉ có thể truy cập từ PFX gốc (với password)
- Public certificate có thể chia sẻ an toàn để verify chữ ký
- Trong hệ thống, không cần extract public certificate vì GroupDocs.Signature nhận trực tiếp PFX

---

## 6. Cấu Trúc DTOs

### 6.1 ImportStockDto (WebAPI)

**File:** `WebAPI/Areas/Admin/Controllers/ImportController.cs`

```csharp
public class ImportStockDto {
    public int StockInId { get; set; }
    public string EmpUsername { get; set; }
    public string Note { get; set; }
    public DateTime InDate { get; set; }
    public List<ImportItemDto> Items { get; set; }
    public string CertificatePfxBase64 { get; set; }      // Required: PFX từ CA
    public string CertificatePassword { get; set; }      // Required: Mật khẩu PFX
}
```

### 6.2 ExportStockDto (WebAPI)

**File:** `WebAPI/Areas/Admin/Controllers/ExportController.cs`

```csharp
public class ExportStockDto {
    public int StockOutId { get; set; }
    public string EmpUsername { get; set; }
    public string Note { get; set; }
    public DateTime OutDate { get; set; }
    public List<ExportItemDto> Items { get; set; }
}

public class CreateExportFromOrderDto {
    public string EmpUsername { get; set; }
    public int OrderId { get; set; }
    public string CertificatePfxBase64 { get; set; }      // Required: PFX từ CA
    public string CertificatePassword { get; set; }      // Required: Mật khẩu PFX
}
```

### 6.3 ApiResponse (WebAPI)

**File:** `WebAPI/Models/ApiResponse.cs`

```csharp
public class ApiResponse<T> {
    public bool Success { get; set; }
    public T? Data { get; set; }
    public string? Error { get; set; }
    
    public static ApiResponse<T> Ok(T data) => 
        new ApiResponse<T> { Success = true, Data = data };
    
    public static ApiResponse<T> Fail(string error) => 
        new ApiResponse<T> { Success = false, Error = error };
}
```

### 6.4 ApiResponse (WebApp)

**File:** `WebApp/Services/SecurityClient.cs`

```csharp
public class ApiResponse<T> {
    public bool Success { get; set; }
    public T? Data { get; set; }
    public string? Error { get; set; }
}
```

---

## 7. Bảo Mật

### 7.1 Mã Hóa Payload

- **Request:** Sử dụng **RSA/AES Hybrid Encryption**
  - Client lấy server public key: `GET /api/public/security/server-public-key`
  - Mã hóa payload (bao gồm CertificatePfxBase64 và CertificatePassword) bằng RSA/AES
  - Gửi `EncryptedPayload` lên server

- **Response:** PDF file (binary) hoặc JSON error

### 7.2 Certificate Management

- **PFX từ client:** 
  - Chỉ được truyền trong request đã mã hóa (RSA/AES)
  - Chỉ dùng để ký PDF, không lưu trên server
  - Phải là certificate hợp lệ từ CA (Certificate Authority)
  
- **PDF đã ký:** Lưu trong database dạng BLOB

### 7.3 Yêu Cầu Bảo Mật

- **Không cho phép tạo self-signed certificate từ private key**
- **Yêu cầu PFX từ CA:** Đảm bảo tính xác thực và pháp lý của chữ ký số
- **Mã hóa payload:** PFX và password được mã hóa RSA/AES trước khi gửi lên server

---

## 8. API Endpoints

### 8.1 Import Stock

| Method | Endpoint | Mô Tả |
|--------|----------|-------|
| POST | `/api/admin/import/post-secure` | Tạo import stock và ký PDF (mã hóa) |
| GET | `/api/admin/import/invoice/{stockinId}` | Lấy PDF đã ký |
| GET | `/api/admin/import/details/{stockinId}` | Lấy chi tiết import |
| GET | `/api/admin/import/verifysign/{stockinId}` | Xác thực chữ ký |

### 8.2 Export Stock

| Method | Endpoint | Mô Tả |
|--------|----------|-------|
| POST | `/api/admin/export/create-secure` | Tạo export từ order và ký PDF (mã hóa) |
| GET | `/api/admin/export/invoice/{stockoutId}` | Lấy PDF đã ký |
| GET | `/api/admin/export/details/{stockoutId}` | Lấy chi tiết export |
| GET | `/api/admin/export/verifysign/{stockoutId}` | Xác thực chữ ký |

---

## 9. Ví Dụ Sử Dụng

### 9.1 Import Stock với PFX Certificate

**WebApp (View):**
```html
<!-- Upload PFX file -->
<input type="file" id="pfx-file" accept=".pfx,.p12" required />
<input type="password" name="CertificatePassword" required />

<!-- JavaScript: Convert PFX to base64 -->
<script>
    reader.onload = function(e) {
        const base64 = e.target.result.split(',')[1] || e.target.result;
        document.getElementById("CertificatePfxBase64").value = base64;
    };
    reader.readAsDataURL(file);
</script>
```

**WebApp (Controller):**
```csharp
var model = new ImportStockDto {
    EmpUsername = "admin",
    Note = "Nhập kho linh kiện",
    CertificatePfxBase64 = model.CertificatePfxBase64,  // Từ form
    CertificatePassword = model.CertificatePassword,    // Từ form
    Items = new List<ImportItemDto> { ... }
};

// Mã hóa và gửi
var encrypted = EncryptHelper.HybridEncrypt(json, serverPublicKey);
await _httpClient.PostAsJsonAsync("api/admin/import/post-secure", encrypted);
```

**WebAPI xử lý:**
1. Giải mã → lấy `ImportStockDto`
2. Validate: Kiểm tra có PFX và password
3. Nếu thiếu PFX → throw exception: "Không thể tạo PFX hợp lệ khi chỉ có private key..."
4. Tạo PDF → ký PDF với PFX → lưu DB
5. Trả về PDF đã ký

### 9.2 Export Stock với PFX Certificate

**WebApp (View - Modal trong Order Index):**
```html
<form method="post" asp-area="Admin" asp-controller="Export" asp-action="Create">
    <input type="hidden" name="orderid" value="@order.OrderId" />
    <input type="hidden" name="EmpUsername" value="@username" />
    
    <input type="file" id="pfxFile-@order.OrderId" accept=".pfx,.p12" required />
    <input type="password" name="CertificatePassword" required />
    <input type="hidden" id="pfxContent-@order.OrderId" name="CertificatePfxBase64" />
</form>
```

**WebApp (Controller):**
```csharp
var plainDto = new {
    EmpUsername = username,
    OrderId = (int)model.orderid,
    CertificatePfxBase64 = model.CertificatePfxBase64,
    CertificatePassword = model.CertificatePassword
};

// Mã hóa và gửi
var encrypted = EncryptHelper.HybridEncrypt(json, serverPublicKey);
await _httpClient.PostAsJsonAsync("api/admin/export/create-secure", encrypted);
```

**WebAPI xử lý:**
1. Giải mã → lấy `CreateExportFromOrderDto`
2. Validate: Kiểm tra có PFX và password
3. Tạo STOCK_OUT → INVOICE
4. Tạo Export PDF → ký với PFX → lưu DB
5. Tạo Invoice PDF → ký với PFX → lưu DB
6. Trả về Invoice PDF đã ký (ưu tiên)

---

## 10. Lưu Ý Kỹ Thuật

### 10.1 GroupDocs.Signature

- **License:** Cần license hợp lệ để sử dụng
- **Signature Position:** Tính toán dựa trên layout PDF (QuestPDF)
- **Visual Appearance:** Có thể customize (position, size, labels)

### 10.2 Oracle Database

- **BLOB Storage:** PDF được lưu dạng BLOB
- **Stored Procedures:** 
  - `UPDATE_STOCKIN_PDF(p_stockin_id, p_signature BLOB)`
  - `UPDATE_STOCKOUT_PDF(p_stockout_id, p_signature BLOB)`
  - `UPDATE_INVOICE_PDF(p_invoice_id, p_signature BLOB)`

### 10.3 Performance

- **PDF Generation:** QuestPDF (fast, in-memory)
- **PDF Signing:** GroupDocs.Signature (có thể chậm với file lớn)
- **Database:** Lưu BLOB có thể tốn thời gian với file lớn

---

## 11. Troubleshooting

### 11.1 Lỗi "Invalid certificate PFX base64"

- Kiểm tra PFX file có đúng format Base64 không
- Kiểm tra PFX có bị corrupt không

### 11.2 Lỗi "Không thể tạo PFX hợp lệ khi chỉ có private key"

- Hệ thống yêu cầu PFX certificate từ CA
- Không thể tạo self-signed certificate từ private key
- Giải pháp: Tạo PFX bằng OpenSSL (xem mục 4.1) hoặc sử dụng PFX từ CA

### 11.3 PDF không có chữ ký

- Kiểm tra GroupDocs license
- Kiểm tra certificate có hợp lệ không
- Kiểm tra vị trí chữ ký có nằm ngoài page không

### 11.4 Lỗi Oracle "ORA-xxxxx"

- Kiểm tra connection string
- Kiểm tra stored procedure có tồn tại không
- Kiểm tra quyền user có đủ không

---

## Kết Luận

Chức năng ký số PDF trong hệ thống hoạt động theo luồng:
1. **Client** upload PFX certificate từ CA và nhập mật khẩu
2. **WebApp** mã hóa payload (PFX + password) bằng RSA/AES và gửi lên WebAPI
3. **WebAPI** giải mã, validate PFX, xử lý nghiệp vụ, tạo PDF
4. **PdfSignatureService** sử dụng PFX từ client để ký PDF
5. **GroupDocs.Signature** nhúng chữ ký số vào PDF với visual signature box
6. **Database** lưu PDF đã ký dạng BLOB
7. **Client** nhận PDF đã ký để download

**Đặc điểm quan trọng:**
- ✅ Yêu cầu PFX certificate từ CA (không cho phép self-signed từ private key)
- ✅ Mã hóa RSA/AES cho toàn bộ payload
- ✅ Không lưu PFX hoặc private key trên server
- ✅ PDF đã ký được lưu trong database để audit và xác thực

