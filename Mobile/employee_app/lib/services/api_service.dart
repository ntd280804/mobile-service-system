import 'dart:async';
import 'dart:io';

import 'package:dio/dio.dart';
import 'package:dio/io.dart';
import 'dart:convert';

import '../config/api_config.dart';
import 'storage_service.dart';
import 'encryption_service.dart';

class ApiService {
  ApiService._internal() {
    // DEVELOPMENT ONLY: Bypass SSL certificate validation
    (_dio.httpClientAdapter as IOHttpClientAdapter).createHttpClient = () {
      final client = HttpClient();
      client.badCertificateCallback = (cert, host, port) => true;
      return client;
    };
  }
  
  static final ApiService _instance = ApiService._internal();
  factory ApiService() => _instance;

  final Dio _dio = Dio(
    BaseOptions(
      baseUrl: ApiConfig.baseUrl,
      connectTimeout: const Duration(seconds: 15),
      receiveTimeout: const Duration(seconds: 20),
      headers: {
        'Content-Type': 'application/json',
      },
    ),
  );
  final StorageService _storage = StorageService();

  bool _interceptorsAdded = false;

  Future<void> _ensureInterceptors() async {
    if (_interceptorsAdded) return;
    _dio.interceptors.add(InterceptorsWrapper(
      onRequest: (options, handler) async {
        final token = await _storage.getToken();
        final username = await _storage.getUsername();
        final sessionId = await _storage.getSessionId();
        if (token != null && token.isNotEmpty) {
          options.headers['Authorization'] = 'Bearer $token';
        }
        if (username != null && username.isNotEmpty) {
          options.headers['X-Oracle-Username'] = username;
        }
        if (sessionId != null && sessionId.isNotEmpty) {
          options.headers['X-Oracle-SessionId'] = sessionId;
        }
        options.headers['X-Oracle-Platform'] = 'MOBILE';
        handler.next(options);
      },
    ));
    _interceptorsAdded = true;
  }

  Future<Map<String, dynamic>> loginSecure(String username, String password) async {
    await _ensureInterceptors();
    try {
      // 1. Get server public key
      final keyResp = await _dio.get(ApiConfig.publicKey);
      final publicKeyBase64 = keyResp.data as String;

      // 2. Prepare login data as JSON
      final loginData = json.encode({
        'username': username,
        'password': password,
        'platform': 'MOBILE',
      });

      // 3. Encrypt login data
      final encrypted = EncryptionService.hybridEncrypt(loginData, publicKeyBase64);

      // 4. Send encrypted payload
      final resp = await _dio.post(
        ApiConfig.loginSecure,
        data: {
          'encryptedKeyBlockBase64': encrypted['encryptedKeyBlock'],
          'cipherDataBase64': encrypted['cipherData'],
        },
      );

      // 5. Process response
      final responseData = resp.data;
      final data = responseData is Map && responseData.containsKey('data')
          ? responseData['data'] as Map<String, dynamic>
          : responseData as Map<String, dynamic>;

      final token = data['token'] ?? data['Token'] ?? '';
      final roles = data['roles'] ?? data['Roles'] ?? '';
      final sessionId = data['sessionId'] ?? data['SessionId'] ?? '';
      final user = data['username'] ?? data['Username'] ?? username;

      if (token is String && token.isNotEmpty) {
        await _storage.saveToken(token);
      }
      if (sessionId is String && sessionId.isNotEmpty) {
        await _storage.saveSessionId(sessionId);
      }
      await _storage.saveUsername(user);

      return {
        'token': token,
        'roles': roles,
        'sessionId': sessionId,
      };
    } on DioException catch (e) {
      final msg = e.response?.data is Map
          ? (e.response?.data['message'] ?? e.response?.data['detail'] ?? 'Đăng nhập thất bại')
          : 'Đăng nhập thất bại';
      throw msg;
    } catch (e) {
      throw 'Lỗi mã hóa hoặc kết nối: $e';
    }
  }

  Future<void> logout() async {
    await _ensureInterceptors();
    try {
      await _dio.post(ApiConfig.logout);
    } catch (_) {
      // ignore network errors on logout
    } finally {
      await _storage.clearAll();
    }
  }

  // Read-only endpoints
  Future<List<Map<String, dynamic>>> getAllAppointments() async {
    await _ensureInterceptors();
    try {
      final resp = await _dio.get(ApiConfig.getAllAppointments);
      return (resp.data as List).cast<Map<String, dynamic>>();
    } catch (_) {
      return [];
    }
  }

  Future<List<Map<String, dynamic>>> getAllOrders() async {
    await _ensureInterceptors();
    try {
      final resp = await _dio.get(ApiConfig.getAllOrders);
      return (resp.data as List).cast<Map<String, dynamic>>();
    } catch (_) {
      return [];
    }
  }

  Future<List<Map<String, dynamic>>> getOrdersByType(String orderType) async {
    await _ensureInterceptors();
    try {
      final resp = await _dio.get('${ApiConfig.getOrdersByType}?orderType=$orderType');
      return (resp.data as List).cast<Map<String, dynamic>>();
    } catch (_) {
      return [];
    }
  }
}
