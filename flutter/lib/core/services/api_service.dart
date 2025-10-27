import 'dart:async';
import 'dart:convert';
import 'dart:io'; // Thêm thư viện này
import 'package:flutter/foundation.dart';
import 'package:http/http.dart' as http;
import 'package:http/io_client.dart'; // Thêm thư viện này để tạo IOClient
import 'package:shared_preferences/shared_preferences.dart';
import 'dart:convert';
import '../models/order.dart';
import '../models/part.dart';
import '../models/part_request.dart';
import '../models/import_stock.dart';

class ApiService {
  static const List<String> _baseCandidates = [
    'http://10.147.20.199:5131',
    'https://10.147.20.199:5131',
  ];

  // khóa cho SharedPreferences
  static const String _keyToken = 'api_token';
  static const String _keyUsername = 'oracle_username';
  static const String _keyPlatform = 'oracle_platform';
  static const String _keyRoles = 'oracle_roles';
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
          final returnedRoles = data['roles'] as String?; // comma separated roles from backend

          final prefs = await SharedPreferences.getInstance();
          if (token != null) await prefs.setString(_keyToken, token);
          if (returnedUsername != null) await prefs.setString(_keyUsername, returnedUsername);
          if (returnedRoles != null) await prefs.setString(_keyRoles, returnedRoles);
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
    await prefs.remove(_keyRoles);
  }

 //order
  static Future<List<Order>> getOrders() async {
    final prefs = await SharedPreferences.getInstance();
    final bases = _baseCandidates;
    final headers = await getAuthHeaders();

    Exception? lastEx;
    for (final base in bases) {
      final uri = Uri.parse('$base/api/Admin/Order');
      final client = _createHttpClient(base);
      try {
        final resp = await client.get(uri, headers: headers).timeout(const Duration(seconds: 10));
        if (resp.statusCode == 200) {
          final List<dynamic> data = jsonDecode(resp.body);
          client.close();
          return data.map((e) => Order.fromJson(e as Map<String, dynamic>)).toList();
        } else if (resp.statusCode == 401) {
          client.close();
          throw Exception('Unauthorized');
        } else {
          client.close();
          throw Exception('Server error: ${resp.statusCode}');
        }
      } catch (e) {
        client.close();
        lastEx = e is Exception ? e : Exception(e.toString());
        continue;
      }
    }

    throw lastEx ?? Exception('Network error');
  }

  // Parts
  static Future<List<Part>> getParts() async {
    final headers = await getAuthHeaders();
    Exception? lastEx;
    for (final base in _baseCandidates) {
      final uri = Uri.parse('$base/api/Admin/Part');
      final client = _createHttpClient(base);
      try {
        final resp = await client.get(uri, headers: headers).timeout(const Duration(seconds: 10));
        if (resp.statusCode == 200) {
          final List<dynamic> data = jsonDecode(resp.body);
          client.close();
          return data.map((e) => Part.fromJson(e as Map<String, dynamic>)).toList();
        }
        client.close();
      } catch (e) {
        client.close();
        lastEx = e is Exception ? e : Exception(e.toString());
        continue;
      }
    }
    throw lastEx ?? Exception('Network error');
  }

  // Part Requests
  static Future<List<PartRequest>> getPartRequests() async {
    final headers = await getAuthHeaders();
    Exception? lastEx;
    for (final base in _baseCandidates) {
      final uri = Uri.parse('$base/api/Admin/Partrequest/getallpartrequest');
      final client = _createHttpClient(base);
      try {
        final resp = await client.get(uri, headers: headers).timeout(const Duration(seconds: 10));
        if (resp.statusCode == 200) {
          final List<dynamic> data = jsonDecode(resp.body);
          client.close();
          return data.map((e) => PartRequest.fromJson(e as Map<String, dynamic>)).toList();
        }
        client.close();
      } catch (e) {
        client.close();
        lastEx = e is Exception ? e : Exception(e.toString());
        continue;
      }
    }
    throw lastEx ?? Exception('Network error');
  }

  // Imports
  static Future<List<ImportStock>> getImports() async {
    final headers = await getAuthHeaders();
    Exception? lastEx;
    for (final base in _baseCandidates) {
      final uri = Uri.parse('$base/api/admin/import/getallimport');
      final client = _createHttpClient(base);
      try {
        final resp = await client.get(uri, headers: headers).timeout(const Duration(seconds: 10));
        if (resp.statusCode == 200) {
          final List<dynamic> data = jsonDecode(resp.body);
          client.close();
          // Map to ImportStock minimal representation; GET all import returns summary with StockInId, EmpUsername, InDate, Note
          return data.map((e) => ImportStock.fromJson(e as Map<String, dynamic>)).toList();
        }
        client.close();
      } catch (e) {
        client.close();
        lastEx = e is Exception ? e : Exception(e.toString());
        continue;
      }
    }
    throw lastEx ?? Exception('Network error');
  }

  static Future<ImportStock> getImportDetails(int stockInId) async {
    final headers = await getAuthHeaders();
    Exception? lastEx;
    for (final base in _baseCandidates) {
      final uri = Uri.parse('$base/api/admin/import/details/$stockInId');
      final client = _createHttpClient(base);
      try {
        final resp = await client.get(uri, headers: headers).timeout(const Duration(seconds: 10));
        if (resp.statusCode == 200) {
          final data = jsonDecode(resp.body) as Map<String, dynamic>;
          client.close();
          return ImportStock.fromJson(data);
        }
        client.close();
      } catch (e) {
        client.close();
        lastEx = e is Exception ? e : Exception(e.toString());
        continue;
      }
    }
    throw lastEx ?? Exception('Network error');
  }

  static Future<int> postImport(Map<String, dynamic> payload) async {
    final headers = await getAuthHeaders();
    Exception? lastEx;
    for (final base in _baseCandidates) {
      final uri = Uri.parse('$base/api/admin/import/post');
      final client = _createHttpClient(base);
      try {
        final resp = await client.post(uri, headers: headers, body: jsonEncode(payload)).timeout(const Duration(seconds: 20));
        if (resp.statusCode == 200) {
          final data = jsonDecode(resp.body) as Map<String, dynamic>;
          client.close();
          return data['StockInId'] is int ? data['StockInId'] : (data['StockInId'] as num).toInt();
        }
        client.close();
      } catch (e) {
        client.close();
        lastEx = e is Exception ? e : Exception(e.toString());
        continue;
      }
    }
    throw lastEx ?? Exception('Network error');
  }

}