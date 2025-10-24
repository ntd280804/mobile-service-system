# ✅ Checklist Yêu Cầu WebSocket/SignalR

## Tổng quan
Đã implement đầy đủ các yêu cầu WebSocket để đồng bộ logout giữa Mobile App và WebApp.

---

## ✅ Các yêu cầu đã hoàn thành

### 1. ✅ Đăng nhập trước khi kết nối WebSocket
- **Login Screen** (`lib/screens/login_screen.dart`):
  - Nhập số điện thoại + mật khẩu
  - Gọi API `/api/Customer/login`
  - Nhận `sessionId` từ response
  - Kiểm tra role phải là `customer`

### 2. ✅ Tạo WebSocket connection đến API
- **SignalR Service** (`lib/services/signalr_service.dart`):
  - Singleton pattern
  - `HubConnectionBuilder()` với SignalR
  - Sử dụng package `signalr_netcore: ^1.3.3`

### 3. ✅ Endpoint đúng format
```dart
final hubUrl = '${ApiConfig.baseUrl}/Hubs/notification?sessionId=$sessionId';
// https://10.147.20.199:5131/Hubs/notification?sessionId=abc123
```
- ✅ Host: `10.147.20.199:5131` (theo yêu cầu)
- ✅ Path: `/Hubs/notification`
- ✅ Query param: `?sessionId={sessionId từ API login}`

### 4. ✅ Auto-reconnect khi mất kết nối
```dart
_connection = HubConnectionBuilder()
    .withUrl(hubUrl)
    .withAutomaticReconnect() // ✅
    .build();
```

### 5. ✅ Listen event từ server
```dart
_connection!.on('ForceLogout', _handleLogout);
```
- Event name: **`ForceLogout`** (khớp với WebApp _Layout.cshtml)

### 6. ✅ Xử lý khi nhận signal logout
- **Home Screen** (`lib/screens/home_screen.dart`):
  - Setup callback trong `initState()`
  - `_handleRemoteLogout()`:
    - Hiện SnackBar: "Bạn đã đăng xuất từ thiết bị khác"
    - Gọi `_logout()` để clear session
    - Navigate về Login screen

### 7. ✅ Disconnect WebSocket khi logout
```dart
Future<void> _logout() async {
  await _signalR.disconnect(); // ✅ Disconnect trước
  await _api.logout();         // Gọi API logout
  // Navigate to login...
}
```

### 8. ✅ Code khớp với WebApp
So sánh với _Layout.cshtml (screenshot):
- ✅ Cùng endpoint: `/Hubs/notification?sessionId=`
- ✅ Cùng event name: `ForceLogout`
- ✅ Cùng auto-reconnect pattern
- ✅ Cùng logic: nhận signal → logout → redirect

---

## 📋 File structure

```
lib/
├── config/
│   └── api_config.dart           # baseUrl = https://10.147.20.199:5131
├── services/
│   ├── signalr_service.dart      # WebSocket connection manager
│   └── api_service.dart          # HTTP API calls
├── screens/
│   ├── login_screen.dart         # Login → connect SignalR
│   └── home_screen.dart          # Listen logout → auto logout
└── main.dart
```

---

## 🔧 Dependencies

```yaml
dependencies:
  dio: ^5.7.0                      # HTTP client
  flutter_secure_storage: ^9.2.2  # Token storage
  signalr_netcore: ^1.3.3          # SignalR WebSocket ✅
```

---

## 🧪 Test Steps

### Chuẩn bị:
```powershell
# 1. Start emulator
flutter emulators --launch Pixel_8_Pro_API_35

# 2. Setup ADB reverse (khi emulator đã chạy)
adb reverse tcp:5131 tcp:5131

# 3. Run app
flutter run
```

### Test scenario:
1. **Login trên Mobile** với SĐT: `0123456789`
2. **Login trên WebApp** (`https://10.147.20.199:5131`) với CÙNG SĐT
3. **Expected:** Mobile hiện SnackBar "Bạn đã đăng xuất từ thiết bị khác" và tự động logout

### Debug logs (kDebugMode):
```
✅ SignalR connecting: https://10.147.20.199:5131/Hubs/notification?sessionId=...
✅ SignalR connected successfully
📡 SignalR received logout event
✅ SignalR disconnected
```

---

## 🎯 So sánh với WebApp

| Feature | WebApp (_Layout.cshtml) | Mobile App | Match |
|---------|------------------------|------------|-------|
| Library | `@microsoft/signalr` | `signalr_netcore` | ✅ |
| Endpoint | `/Hubs/notification?sessionId=` | Same | ✅ |
| Host | `10.147.20.199:5131` | Same | ✅ |
| Event | `ForceLogout` | `ForceLogout` | ✅ |
| Auto-reconnect | Yes | Yes | ✅ |
| On logout | Redirect to login | Navigate to login | ✅ |
| Disconnect | On page unload | On logout | ✅ |

---

## ✅ Kết luận

**TẤT CẢ các yêu cầu đã được implement đầy đủ:**

1. ✅ Login trước khi tạo WebSocket
2. ✅ Tạo WebSocket connection đến API
3. ✅ Endpoint đúng format: `https://10.147.20.199:5131/Hubs/notification?sessionId=`
4. ✅ Code pattern khớp với _Layout.cshtml trong WebApp
5. ✅ Auto-reconnect
6. ✅ Listen event `ForceLogout`
7. ✅ Xử lý logout khi nhận signal
8. ✅ Disconnect WebSocket khi logout
9. ✅ Flutter analyze: No issues found

**Sẵn sàng test và cài ZeroTier!** 🚀

---

## 📝 Notes

- IP đã update từ `10.147.20.56` → `10.147.20.199` (theo yêu cầu)
- Event name đã fix từ `logout` → `ForceLogout` (khớp WebApp)
- SignalR chỉ chạy trong Debug mode (`kDebugMode`)
- ADB reverse cần setup khi emulator đã chạy
