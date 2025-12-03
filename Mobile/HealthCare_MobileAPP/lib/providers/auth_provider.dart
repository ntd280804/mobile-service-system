import 'package:flutter/material.dart';
import '../services/storage_service.dart';
import '../services/api_service.dart';

/// Authentication provider managing login state and JWT tokens
class AuthProvider extends ChangeNotifier {
  final StorageService _storage = StorageService();
  final ApiService _api = ApiService();

  String? _token;
  String? _username;
  String? _userType; // 'customer' or 'employee'
  bool _isLoading = false;

  String? get token => _token;
  String? get username => _username;
  String? get userType => _userType;
  bool get isAuthenticated => _token != null && _token!.isNotEmpty;
  bool get isLoading => _isLoading;
  bool get isCustomer => _userType == 'customer';
  bool get isEmployee => _userType == 'employee';

  /// Initialize auth state from storage
  Future<void> initialize() async {
    _isLoading = true;
    try {
      _token = await _storage.getToken();
      _username = await _storage.getUsername();
      _userType = await _storage.getUserType();
    } finally {
      _isLoading = false;
    }
    notifyListeners();
  }

  /// Customer login
  Future<Map<String, dynamic>> loginCustomer({
    required String phone,
    required String password,
  }) async {
    _isLoading = true;
    notifyListeners();

    try {
      final response = await _api.login(phone, password);

      // API login() returns {token, roles, sessionId}
      _token = response['token'];
      _username = phone;
      _userType = 'customer';

      // Token already saved by API service
      await _storage.saveUserType(_userType!);

      _isLoading = false;
      notifyListeners();

      return {'success': true};
    } catch (e) {
      _isLoading = false;
      notifyListeners();
      return {
        'success': false,
        'message': _parseErrorMessage(e.toString()),
      };
    }
  }

  /// Employee login
  Future<Map<String, dynamic>> loginEmployee({
    required String username,
    required String password,
  }) async {
    _isLoading = true;
    notifyListeners();

    try {
      final response = await _api.loginEmployee(username, password);

      // API loginEmployee() returns {token, roles, sessionId}
      _token = response['token'];
      _username = username;
      _userType = 'employee';

      // Token already saved by API service
      await _storage.saveUserType(_userType!);

      _isLoading = false;
      notifyListeners();

      return {'success': true};
    } catch (e) {
      _isLoading = false;
      notifyListeners();
      return {
        'success': false,
        'message': _parseErrorMessage(e.toString()),
      };
    }
  }

  /// Customer registration
  Future<Map<String, dynamic>> registerCustomer({
    required String fullName,
    required String phone,
    required String email,
    required String password,
    String? address,
  }) async {
    _isLoading = true;
    notifyListeners();

    try {
      final response = await _api.register(
        fullName: fullName,
        phone: phone,
        email: email,
        password: password,
        address: address,
      );

      _isLoading = false;
      notifyListeners();

      return {'success': true, 'data': response};
    } catch (e) {
      _isLoading = false;
      notifyListeners();
      return {
        'success': false,
        'message': _parseErrorMessage(e.toString()),
      };
    }
  }

  /// Logout
  Future<void> logout() async {
    await _api.logout();
    _token = null;
    _username = null;
    _userType = null;
    notifyListeners();
  }

  /// Change password for current user type
  Future<Map<String, dynamic>> changePassword({
    required String oldPassword,
    required String newPassword,
  }) async {
    _isLoading = true;
    notifyListeners();

    try {
      if (isCustomer) {
        await _api.changePassword(oldPassword, newPassword);
      } else if (isEmployee) {
        await _api.changePasswordEmployee(oldPassword, newPassword);
      } else {
        throw 'Không xác định được loại tài khoản';
      }

      _isLoading = false;
      notifyListeners();
      return {'success': true};
    } catch (e) {
      _isLoading = false;
      notifyListeners();
      return {
        'success': false,
        'message': _parseErrorMessage(e.toString()),
      };
    }
  }

  String _parseErrorMessage(String error) {
    final msg = error.trim();
    if (msg.isEmpty) return 'Đã xảy ra lỗi';
    if (msg.toLowerCase().contains('timeout')) return 'Yêu cầu hết thời gian chờ';
    if (msg.contains('401') || msg.toLowerCase().contains('unauthorized')) return 'Phiên đăng nhập hết hạn';
    if (msg.length > 200) return msg.substring(0, 200);
    return msg;
  }
}
