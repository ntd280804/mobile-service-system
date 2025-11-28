import 'dart:async';
import 'dart:io'; // ✅ Import để bypass SSL

import 'package:dio/dio.dart';
import 'package:dio/io.dart'; // ✅ Import adapter
import 'dart:convert';

import '../config/api_config.dart';
import 'storage_service.dart';
import '../models/order.dart';
import '../models/part.dart';
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
        options.headers['X-Oracle-Platform'] = 'MOBILE';
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

      final data = resp.data['data'] as Map<String, dynamic>;
      final token = data['token'] ?? '';
      final roles = data['roles'] ?? '';
      final sessionId = data['sessionId'] ?? '';
      final username = data['username'] ?? phone;

      if (token is String && token.isNotEmpty) {
        await _storage.saveToken(token);
      }
      if (sessionId is String && sessionId.isNotEmpty) {
        await _storage.saveSessionId(sessionId);
      }
      await _storage.saveUsername(username);
      await _storage.saveUserRole(roles);

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

  Future<Map<String, dynamic>> loginEmployee(String phone, String password) async {
    await _ensureInterceptors();
    try {
      final resp = await _dio.post(
        ApiConfig.loginEmployee,
        data: {
          'username': phone,
          'password': password,
          'platform': 'MOBILE',
        },
      );

      final data = resp.data['data'] as Map<String, dynamic>;
      final token = data['token'] ?? '';
      final roles = data['roles'] ?? '';
      final sessionId = data['sessionId'] ?? '';
      final username = data['username'] ?? phone;

      if (token is String && token.isNotEmpty) {
        await _storage.saveToken(token);
      }
      if (sessionId is String && sessionId.isNotEmpty) {
        await _storage.saveSessionId(sessionId);
      }
      await _storage.saveUsername(username);
      await _storage.saveUserRole(roles);

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

  Future<void> confirmQrLogin(String code) async {
    await _ensureInterceptors();
    try {
      final resp = await _dio.post(
        ApiConfig.qrLoginConfirm,
        data: {
          'code': code,
        },
      );

      Map<String, dynamic> body = {};
      if (resp.data is Map<String, dynamic>) {
        body = resp.data as Map<String, dynamic>;
      }

      if (body['success'] != true) {
        final errorMsg = body['error'] ?? body['message'] ?? 'Xác nhận đăng nhập thất bại';
        throw errorMsg;
      }
    } on DioException catch (e) {
      final msg = e.response?.data is Map
          ? (e.response?.data['message'] ?? e.response?.data['error'] ?? 'Xác nhận đăng nhập thất bại')
          : 'Xác nhận đăng nhập thất bại';
      throw msg;
    } catch (_) {
      throw 'Không thể kết nối máy chủ';
    }
  }

  Future<Map<String, dynamic>> loginViaWebQr(String code, {String platform = 'MOBILE'}) async {
    await _ensureInterceptors();
    try {
      final resp = await _dio.post(
        ApiConfig.webToMobileQrConfirm,
        data: {
          'code': code,
          'platform': platform,
        },
      );

      Map<String, dynamic> body = {};
      if (resp.data is Map<String, dynamic>) {
        body = resp.data as Map<String, dynamic>;
      }

      if (body['success'] != true || body['data'] == null) {
        final errorMsg = body['error'] ?? 'Đăng nhập bằng QR thất bại';
        throw errorMsg;
      }

      final data = body['data'] as Map<String, dynamic>;
      final token = data['token'] ?? '';
      final roles = data['roles'] ?? '';
      final sessionId = data['sessionId'] ?? '';
      final username = data['username'] ?? '';

      if (token is String && token.isNotEmpty) {
        await _storage.saveToken(token);
      }
      if (sessionId is String && sessionId.isNotEmpty) {
        await _storage.saveSessionId(sessionId);
      }
      if (username is String && username.isNotEmpty) {
        await _storage.saveUsername(username);
      }
      await _storage.saveUserRole(roles);

      return {
        'token': token,
        'roles': roles,
        'sessionId': sessionId,
        'username': username,
      };
    } on DioException catch (e) {
      final msg = e.response?.data is Map
          ? (e.response?.data['message'] ?? e.response?.data['error'] ?? 'Đăng nhập bằng QR thất bại')
          : 'Đăng nhập bằng QR thất bại';
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


  Future<void> logout() async {
    await _ensureInterceptors();
    try {
      // Try customer logout first, if fails try employee logout
    try {
      await _dio.post(ApiConfig.logout);
      } catch (_) {
        await _dio.post(ApiConfig.logoutEmployee);
      }
    } catch (_) {
      // ignore network errors on logout
    } finally {
      await _storage.clearAll();
    }
  }

  // Employee endpoints
  Future<List<Map<String, dynamic>>> getAllAppointments() async {
    await _ensureInterceptors();
    try {
      final resp = await _dio.get(ApiConfig.getAllAppointments);
      if (resp.data is List) {
        return (resp.data as List).cast<Map<String, dynamic>>();
      }
      return [];
    } catch (_) {
      return [];
    }
  }

  Future<List<Map<String, dynamic>>> getAllOrders() async {
    await _ensureInterceptors();
    try {
      final resp = await _dio.get(ApiConfig.getAllOrders);
      if (resp.data is List) {
        return (resp.data as List).cast<Map<String, dynamic>>();
      }
      return [];
    } catch (_) {
      return [];
    }
  }

  Future<void> changePasswordEmployee(String oldPassword, String newPassword) async {
    await _ensureInterceptors();
    try {
      await _dio.post(
        ApiConfig.changePasswordEmployee,
        data: {
          'oldPassword': oldPassword,
          'newPassword': newPassword,
        },
      );
    } on DioException catch (e) {
      final msg = e.response?.data is Map
          ? e.response?.data['message'] ?? e.response?.data['error'] ?? 'Đổi mật khẩu thất bại'
          : 'Đổi mật khẩu thất bại';
      throw msg;
    } catch (_) {
      throw 'Không thể kết nối máy chủ';
    }
  }

  // Import/Export/Invoice endpoints
  Future<List<Map<String, dynamic>>> getAllImports() async {
    await _ensureInterceptors();
    try {
      final resp = await _dio.get(ApiConfig.getAllImports);
      if (resp.data is List) {
        return (resp.data as List).cast<Map<String, dynamic>>();
      }
      return [];
    } catch (_) {
      return [];
    }
  }

  Future<List<Map<String, dynamic>>> getAllExports() async {
    await _ensureInterceptors();
    try {
      final resp = await _dio.get(ApiConfig.getAllExports);
      if (resp.data is List) {
        return (resp.data as List).cast<Map<String, dynamic>>();
      }
      return [];
    } catch (_) {
      return [];
    }
  }

  Future<List<Map<String, dynamic>>> getAllInvoices() async {
    await _ensureInterceptors();
    try {
      final resp = await _dio.get(ApiConfig.getAllInvoices);
      if (resp.data is List) {
        return (resp.data as List).cast<Map<String, dynamic>>();
      }
      return [];
    } catch (_) {
      return [];
    }
  }

  // Detail endpoints
  Future<Map<String, dynamic>> getImportDetails(int stockInId) async {
    await _ensureInterceptors();
    try {
      final resp = await _dio.get('${ApiConfig.getImportDetails}/$stockInId/details');
      return resp.data as Map<String, dynamic>;
    } catch (e) {
      throw 'Không thể tải chi tiết: $e';
    }
  }

  Future<Map<String, dynamic>> getExportDetails(int stockOutId) async {
    await _ensureInterceptors();
    try {
      final resp = await _dio.get('${ApiConfig.getExportDetails}/$stockOutId/details');
      return resp.data as Map<String, dynamic>;
    } catch (e) {
      throw 'Không thể tải chi tiết: $e';
    }
  }

  Future<Map<String, dynamic>> getInvoiceDetails(int invoiceId) async {
    await _ensureInterceptors();
    try {
      final resp = await _dio.get('${ApiConfig.getInvoiceDetails}/$invoiceId/details');
      return resp.data as Map<String, dynamic>;
    } catch (e) {
      throw 'Không thể tải chi tiết: $e';
    }
  }

  // PDF download endpoints
  Future<List<int>> downloadImportPdf(int stockInId) async {
    await _ensureInterceptors();
    try {
      final resp = await _dio.get(
        '${ApiConfig.getImportInvoice}/$stockInId/invoice',
        options: Options(responseType: ResponseType.bytes),
      );
      return resp.data as List<int>;
    } catch (e) {
      throw 'Không thể tải PDF: $e';
    }
  }

  Future<List<int>> downloadExportPdf(int stockOutId) async {
    await _ensureInterceptors();
    try {
      final resp = await _dio.get(
        '${ApiConfig.getExportInvoice}/$stockOutId/invoice',
        options: Options(responseType: ResponseType.bytes),
      );
      return resp.data as List<int>;
    } catch (e) {
      throw 'Không thể tải PDF: $e';
    }
  }

  Future<List<int>> downloadInvoicePdf(int invoiceId) async {
    await _ensureInterceptors();
    try {
      final resp = await _dio.get(
        '${ApiConfig.getInvoicePdf}/$invoiceId/pdf',
        options: Options(responseType: ResponseType.bytes),
      );
      return resp.data as List<int>;
    } catch (e) {
      throw 'Không thể tải PDF: $e';
    }
  }

  // Verify endpoints
  Future<Map<String, dynamic>> verifyImportSign(int stockInId) async {
    await _ensureInterceptors();
    try {
      final resp = await _dio.get('${ApiConfig.verifyImportSign}/$stockInId/verify');
      final data = resp.data as Map<String, dynamic>;
      // Normalize isValid to boolean - handle both isValid and IsValid, both boolean and int
      dynamic isValidValue = data['isValid'] ?? data['IsValid'] ?? false;
      if (isValidValue is bool) {
        data['isValid'] = isValidValue;
      } else if (isValidValue is int) {
        data['isValid'] = isValidValue == 1;
      } else {
        data['isValid'] = false;
      }
      return data;
    } on DioException catch (e) {
      final msg = e.response?.data is Map
          ? (e.response?.data['message'] ?? e.response?.data['Message'] ?? 'Không thể xác thực')
          : 'Không thể xác thực';
      throw msg;
    } catch (e) {
      throw 'Không thể xác thực: $e';
    }
  }

  Future<Map<String, dynamic>> verifyExportSign(int stockOutId) async {
    await _ensureInterceptors();
    try {
      final resp = await _dio.get('${ApiConfig.verifyExportSign}/$stockOutId/verify');
      final data = resp.data as Map<String, dynamic>;
      // Normalize isValid to boolean - handle both isValid and IsValid, both boolean and int
      dynamic isValidValue = data['isValid'] ?? data['IsValid'] ?? false;
      if (isValidValue is bool) {
        data['isValid'] = isValidValue;
      } else if (isValidValue is int) {
        data['isValid'] = isValidValue == 1;
      } else {
        data['isValid'] = false;
      }
      return data;
    } on DioException catch (e) {
      final msg = e.response?.data is Map
          ? (e.response?.data['message'] ?? e.response?.data['Message'] ?? 'Không thể xác thực')
          : 'Không thể xác thực';
      throw msg;
    } catch (e) {
      throw 'Không thể xác thực: $e';
    }
  }

  Future<Map<String, dynamic>> verifyInvoice(int invoiceId) async {
    await _ensureInterceptors();
    try {
      final resp = await _dio.get('${ApiConfig.verifyInvoice}/$invoiceId/verify');
      final data = resp.data as Map<String, dynamic>;
      // Normalize isValid to boolean - handle both isValid and IsValid, both boolean and int
      dynamic isValidValue = data['isValid'] ?? data['IsValid'] ?? false;
      if (isValidValue is bool) {
        data['isValid'] = isValidValue;
      } else if (isValidValue is int) {
        data['isValid'] = isValidValue == 1;
      } else {
        data['isValid'] = false;
      }
      return data;
    } on DioException catch (e) {
      final msg = e.response?.data is Map
          ? (e.response?.data['message'] ?? e.response?.data['Message'] ?? 'Không thể xác thực')
          : 'Không thể xác thực';
      throw msg;
    } catch (e) {
      throw 'Không thể xác thực: $e';
    }
  }
  Future<void> changePassword(String oldPassword, String newPassword) async {
    await _ensureInterceptors();
    try {
      await _dio.post(
        ApiConfig.ChangePass,
        data: {
          'oldPassword': oldPassword,
          'newPassword': newPassword,
        },
      );

    } on DioException catch (e) {
      final msg = e.response?.data is Map
          ? e.response?.data['message'] ?? 'Đổi mật khẩu thất bại'
          : 'Đổi mật khẩu thất bại';
      throw msg;
    } catch (_) {
      throw 'Không thể kết nối máy chủ';
    }
  }


  Future<List<Order>> getOrders() async {
    await _ensureInterceptors();
    try {
      final resp = await _dio.get(ApiConfig.getOrders);
      if (resp.data is List) {
        // Parse mỗi item sang model Order
        return (resp.data as List)
            .map((e) => Order.fromJson(Map<String, dynamic>.from(e)))
            .toList();
      }
      return [];
    } catch (_) {
      return [];
    }
  }

  Future<List<Map<String, dynamic>>> getAppointments() async {
    await _ensureInterceptors();
    try {
      final username = await _storage.getUsername();
      if (username == null || username.isEmpty) {
        throw 'Không tìm thấy username trong bộ nhớ';
      }

      final resp = await _dio.get(
        '${ApiConfig.getAppointments}',
      );
      if (resp.data is List) {
        return (resp.data as List)
            .map((e) => Map<String, dynamic>.from(e))
            .toList();
      } else {
        return [];
      }
    } catch (e) {
      print('Lỗi getAppointments: $e');
      return [];
    }
  }

  Future<void> createAppointment(DateTime date, {String? description}) async {
    await _ensureInterceptors();
    try {
      final username = await _storage.getUsername();
      if (username == null || username.isEmpty) {
        throw 'Không tìm thấy username trong bộ nhớ';
      }

      // Chuẩn hóa về đầu ngày (date-only)
      final dateOnly = DateTime(date.year, date.month, date.day);

      final payload = {
        'customerPhone': username,
        'appointmentDate': dateOnly.toIso8601String(),
        if (description != null && description.trim().isNotEmpty)
          'description': description.trim(),
      };

      await _dio.post(
        ApiConfig.createAppointment,
        data: payload,
      );
    } on DioException catch (e) {
      final msg = e.response?.data is Map
          ? (e.response?.data['message'] ?? e.response?.data['detail'] ?? 'Đặt lịch thất bại')
          : 'Đặt lịch thất bại';
      throw msg;
    } catch (_) {
      throw 'Không thể kết nối máy chủ';
    }
  }

  // Part endpoints
  Future<List<Part>> getPartsByOrderId(int orderId) async {
    await _ensureInterceptors();
    try {
      final resp = await _dio.get('${ApiConfig.getPartsByOrderId}/$orderId/by-order-id');
      if (resp.data is List) {
        return (resp.data as List)
            .map((e) => Part.fromJson(Map<String, dynamic>.from(e)))
            .toList();
      }
      return [];
    } catch (e) {
      print('Lỗi getPartsByOrderId: $e');
      return [];
    }
  }

  Future<List<Part>> getPartsByPartRequest(int orderId) async {
    await _ensureInterceptors();
    try {
      final resp = await _dio.get('${ApiConfig.getPartsByPartRequest}/$orderId/by-part-request');
      if (resp.data is List) {
        return (resp.data as List)
            .map((e) => Part.fromJson(Map<String, dynamic>.from(e)))
            .toList();
      }
      return [];
    } catch (e) {
      print('Lỗi getPartsByPartRequest: $e');
      return [];
    }
  }

  Future<List<Part>> getPartsByRequestId(int requestId) async {
    await _ensureInterceptors();
    try {
      final resp = await _dio.get('${ApiConfig.getPartsByRequestId}/$requestId/by-request-id');
      if (resp.data is List) {
        return (resp.data as List)
            .map((e) => Part.fromJson(Map<String, dynamic>.from(e)))
            .toList();
      }
      return [];
    } catch (e) {
      print('Lỗi getPartsByRequestId: $e');
      return [];
    }
  }

  Future<Map<String, dynamic>> getOrderDetails(int orderId) async {
    await _ensureInterceptors();
    try {
      final resp = await _dio.get('${ApiConfig.getOrderDetails}/$orderId/details');
      return resp.data as Map<String, dynamic>;
    } catch (e) {
      throw 'Không thể tải chi tiết đơn hàng: $e';
    }
  }
}
