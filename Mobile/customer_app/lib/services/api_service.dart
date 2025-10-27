import 'dart:async';
import 'dart:io'; // ✅ Import để bypass SSL

import 'package:dio/dio.dart';
import 'package:dio/io.dart'; // ✅ Import adapter

import '../config/api_config.dart';
import 'storage_service.dart';

class ApiService {
  ApiService._internal() {
    // ⚠️ DEVELOPMENT ONLY: Bypass SSL certificate validation
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
        options.headers['X-Oracle-Platform'] = 'mobile';
        handler.next(options);
      },
    ));
    _interceptorsAdded = true;
  }

  Future<Map<String, dynamic>> login(String phone, String password) async {
    await _ensureInterceptors();
    try {
      final resp = await _dio.post(
        ApiConfig.login,
        data: {
          'username': phone,
          'password': password,
          'platform': 'MOBILE',
        },
      );
      
      final data = resp.data as Map<String, dynamic>;
      final token = data['token'] ?? data['Token'] ?? '';
      final roles = data['roles'] ?? data['Roles'] ?? '';
      final sessionId = data['sessionId'] ?? data['SessionId'] ?? '';
      final username = data['username'] ?? data['Username'] ?? phone;

      if (token is String && token.isNotEmpty) {
        await _storage.saveToken(token);
      }
      if (sessionId is String && sessionId.isNotEmpty) {
        await _storage.saveSessionId(sessionId);
      }
      await _storage.saveUsername(username);

      return {
        'token': token,
        'roles': roles,
        'sessionId': sessionId,
      };
    } on DioException catch (e) {
      final msg = e.response?.data is Map
          ? (e.response?.data['message'] ?? 'Đăng nhập thất bại')
          : 'Đăng nhập thất bại';
      throw msg;
    } catch (_) {
      throw 'Không thể kết nối máy chủ';
    }
  }

  Future<Map<String, dynamic>> register({
    required String fullName,
    required String phone,
    required String email,
    required String password,
    String? address,
  }) async {
    await _ensureInterceptors();
    try {
      final resp = await _dio.post(
        ApiConfig.register,
        data: {
          'fullName': fullName,
          'phone': phone,
          'email': email,
          'password': password,
          'address': address ?? '',
        },
      );
      
      return resp.data as Map<String, dynamic>;
    } on DioException catch (e) {
      final msg = e.response?.data is Map
          ? (e.response?.data['message'] ?? e.response?.data['detail'] ?? 'Đăng ký thất bại')
          : 'Đăng ký thất bại';
      throw msg;
    } catch (_) {
      throw 'Không thể kết nối máy chủ';
    }
  }

  Future<Map<String, dynamic>> forgotPassword(String phone) async {
    await _ensureInterceptors();
    try {
      // Note: Backend may not have this endpoint yet
      final resp = await _dio.post(
        '/api/Public/Customer/forgot-password',
        data: {'phone': phone},
      );
      return resp.data as Map<String, dynamic>;
    } on DioException catch (e) {
      if (e.response?.statusCode == 404) {
        throw 'Chức năng quên mật khẩu chưa được hỗ trợ';
      }
      final msg = e.response?.data is Map
          ? (e.response?.data['message'] ?? 'Gửi yêu cầu thất bại')
          : 'Gửi yêu cầu thất bại';
      throw msg;
    } catch (_) {
      throw 'Không thể kết nối máy chủ';
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

  Future<List<Map<String, dynamic>>> getOrders() async {
    await _ensureInterceptors();
    try {
      final resp = await _dio.get(ApiConfig.getOrders);
      final list = (resp.data as List).cast<Map<String, dynamic>>();
      return list;
    } catch (_) {
      return [];
    }
  }

  Future<List<Map<String, dynamic>>> getAppointments() async {
    await _ensureInterceptors();
    try {
      final resp = await _dio.get(ApiConfig.getAppointments);
      final list = (resp.data as List).cast<Map<String, dynamic>>();
      return list;
    } catch (_) {
      return [];
    }
  }
}
