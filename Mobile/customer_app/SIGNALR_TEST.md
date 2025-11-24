# Test SignalR Logout Synchronization

## Mục đích
Kiểm tra tính năng đồng bộ logout giữa các phiên đăng nhập. Khi đăng xuất từ WebApp, mobile app tự động logout.

## Yêu cầu
- Backend WebAPI đang chạy tại `https://10.147.20.199:5131`
- ADB reverse đã setup: `adb reverse tcp:5131 tcp:5131`
- SignalR Hub endpoint: `/Hubs/notification?sessionId={sessionId}`
- Event name: `ForceLogout` (khớp với WebApp)

## Các bước test

### 1. Khởi động emulator và app
```powershell
# Khởi động emulator nếu chưa chạy
flutter emulators --launch Pixel_8_Pro_API_35

# Chạy app
flutter run
```

### 2. Đăng nhập trên Mobile App
- Mở app trên emulator
- Đăng nhập với tài khoản test (VD: `0123456789`)
- Kiểm tra debug log: "SignalR connected successfully"
- App chuyển đến Home screen

### 3. Đăng nhập trên WebApp (cùng tài khoản)
- Mở trình duyệt: `https://10.147.20.199:5131`
- Đăng nhập với CÙNG số điện thoại vừa dùng trên mobile
- WebApp sẽ gửi event `ForceLogout` qua SignalR tới mobile

### 4. Kiểm tra kết quả mong đợi

#### Trên Mobile App:
- **SnackBar hiện lên** với nội dung: `"Bạn đã đăng xuất"`
- App **tự động chuyển về Login screen**
- Token và sessionId bị xóa

#### Trong Debug Log (VS Code Debug Console):
```
SignalR received logout event
SignalR disconnected
```

### 5. Test ngược lại (Optional)
- Đăng nhập lại trên Mobile
- Đăng nhập WebApp với tài khoản khác
- Logout từ Mobile
- Kiểm tra mobile disconnect SignalR thành công

## Cấu trúc code SignalR

### SignalRService (`lib/services/signalr_service.dart`)
```dart
// Kết nối sau khi login
await signalRService.connect(sessionId);

// Lắng nghe sự kiện logout
signalRService.onLogoutReceived = () {
  // Xử lý logout trong UI
};

// Ngắt kết nối trước khi logout
await signalRService.disconnect();
```

### Login Screen
- File: `lib/screens/login_screen.dart`
- Dòng: Import `signalr_service.dart`
- Logic: Sau khi login thành công → `_signalR.connect(sessionId)`

### Home Screen
- File: `lib/screens/home_screen.dart`
- `initState`: Setup callback `onLogoutReceived`
- `_handleRemoteLogout()`: Xử lý khi nhận event logout
- `_logout()`: Disconnect SignalR trước khi logout
- `dispose()`: Cleanup callback

## Troubleshooting

### Không thấy "SignalR connected"
- Kiểm tra backend có đang chạy không
- Kiểm tra `adb reverse` đã setup chưa
- Kiểm tra `api_config.dart` đúng URL chưa

### Mobile không logout khi WebApp logout
- Kiểm tra cùng số điện thoại chưa
- Xem backend log: Hub có nhận sessionId không
- Kiểm tra event name: phải là `'ForceLogout'` (khớp với WebApp _Layout.cshtml)

### SignalR disconnect lỗi
- Bình thường, không ảnh hưởng chức năng
- Có thể bỏ qua log "SignalR disconnect error"

## Lưu ý
- SignalR chỉ hoạt động trong chế độ Debug (có `kDebugMode`)
- Nếu backend dùng HTTPS với cert không hợp lệ, cần setup cert hoặc dùng localhost
- Event name phải khớp giữa client và server (`'logout'`)
- SessionId được trả về từ API login, phải truyền vào `?sessionId=` query parameter

## Next Steps
Sau khi test SignalR thành công, tiếp tục:
1. ✅ Setup ZeroTier VPN
2. Update `api_config.dart` với ZeroTier IP
3. Test lại với ZeroTier network
