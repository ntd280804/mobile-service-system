import 'dart:async';
import 'dart:convert';
import 'dart:io'; // Thêm thư viện này
import 'package:flutter/foundation.dart';
import 'package:http/http.dart' as http;
import 'package:http/io_client.dart'; // Thêm thư viện này để tạo IOClient
import 'package:shared_preferences/shared_preferences.dart';

class ApiService {
  static const List<String> _baseCandidates = [
    'http://10.147.20.199:5131',
    'https://10.147.20.199:5131',
  ];

  // khóa cho SharedPreferences
  static const String _keyToken = 'api_token';
  static const String _keyUsername = 'oracle_username';
  static const String _keyPlatform = 'oracle_platform';
  static const String _keySessionId = 'oracle_sessionId';

  /// Tạo một http.Client tùy chỉnh.
  /// Nếu base URL là HTTPS, nó sẽ tạo một IOClient bỏ qua kiểm tra chứng chỉ (chỉ trong môi trường non-web).
  static http.Client _createHttpClient(String base) {
    if (base.startsWith('https://') && !kIsWeb) {
      // Bỏ qua kiểm tra chứng chỉ SSL/TLS cho môi trường dev/test
      final httpClient = HttpClient()
        ..badCertificateCallback = (X509Certificate cert, String host, int port) => true;
      return IOClient(httpClient);
    }
    // Sử dụng http.Client mặc định cho HTTP hoặc khi chạy trên web
    return http.Client();
  }

  // đăng nhập vào backend sau đó trả về thông báo mô tả kết quả
  // mã thông báo và tiêu đề phiên Oracle được lưu trữ trong SharedPreferences.
  static Future<String> login(String username, String password,
      {String platform = 'MOBILE'}) async {
    if (username.isEmpty || password.isEmpty) return 'Username and password required.';

    const endpoint = '/api/Admin/Employee/Login';

    final body = jsonEncode({
      'Username': username,
      'Password': password,
      'Platform': platform,
    });

    Exception? lastEx;

    for (final base in _baseCandidates) {
      final uri = Uri.parse('$base$endpoint');
      // 1. Tạo client tùy chỉnh (có thể bỏ qua SSL)
      final client = _createHttpClient(base);

      try {
        // 2. Sử dụng client đã tạo
        final resp = await client.post(uri,
            headers: {'Content-Type': 'application/json'}, body: body).timeout(const Duration(seconds: 10));

        if (resp.statusCode == 200) {
          final data = jsonDecode(resp.body) as Map<String, dynamic>;
          final token = data['token'] as String?;
          final sessionId = data['sessionId'] as String?;
          final returnedUsername = data['username'] as String?;

          final prefs = await SharedPreferences.getInstance();
          if (token != null) await prefs.setString(_keyToken, token);
          if (returnedUsername != null) await prefs.setString(_keyUsername, returnedUsername);
          if (platform.isNotEmpty) await prefs.setString(_keyPlatform, platform);
          if (sessionId != null) await prefs.setString(_keySessionId, sessionId);

          client.close(); // Đóng client sau khi thành công
          return 'OK';
        } else if (resp.statusCode == 401) {
          try {
            final data = jsonDecode(resp.body);
            final message = data['message'] ?? 'Unauthorized';
            client.close();
            return message.toString();
          } catch (_) {
            client.close();
            return 'Unauthorized';
          }
        } else {
          client.close();
          return 'Server error: ${resp.statusCode} ($base)';
        }
      } catch (e) {
        client.close(); // Đóng client khi gặp lỗi mạng
        if (e is Exception) lastEx = e;
        if (kIsWeb) {
          final msg = e.toString().toLowerCase();
          if (msg.contains('certificate') || msg.contains('handshake') ||
              msg.contains('cors')) {
            return 'Kết nối thất bại: có thể do HTTPS / chứng chỉ hoặc CORS khi chạy trên web. Hãy thử truy cập $base trong trình duyệt hoặc dùng Postman. Chi tiết: $e';
          }
        }
        // Tiếp tục vòng lặp để thử URL tiếp theo
        continue;
      }
    }

    return lastEx != null ? 'Network error: ${lastEx.toString()}' : 'Network error: cannot reach backend';
  }

  // lấy tiêu đề để sử dụng cho các yêu cầu đã xác thực
  // mã thông báo Bearer cộng với tiêu đề X-Oracle-* được yêu cầu bởi backend cho các lệnh gọi được bảo vệ.
  static Future<Map<String, String>> getAuthHeaders() async {
    final prefs = await SharedPreferences.getInstance();
    final token = prefs.getString(_keyToken);
    final username = prefs.getString(_keyUsername);
    final platform = prefs.getString(_keyPlatform);
    final sessionId = prefs.getString(_keySessionId);

    final headers = <String, String>{'Content-Type': 'application/json'};
    if (token != null && token.isNotEmpty) {
      headers['Authorization'] = 'Bearer $token';
    }
    if (username != null) headers['X-Oracle-Username'] = username;
    if (platform != null) headers['X-Oracle-Platform'] = platform;
    if (sessionId != null) headers['X-Oracle-SessionId'] = sessionId;

    return headers;
  }

  static Future<void> logout() async {
    final prefs = await SharedPreferences.getInstance();
    await prefs.remove(_keyToken);
    await prefs.remove(_keyUsername);
    await prefs.remove(_keyPlatform);
    await prefs.remove(_keySessionId);
  }
}