import 'package:flutter_secure_storage/flutter_secure_storage.dart';

class StorageKeys {
  static const token = 'jwt_token';
  static const sessionId = 'session_id';
  static const username = 'username';
  static const userRole = 'user_role';
}

class StorageService {
  final FlutterSecureStorage _storage = const FlutterSecureStorage();

  Future<void> saveToken(String token) async {
    await _storage.write(key: StorageKeys.token, value: token);
  }

  Future<String?> getToken() async {
    return _storage.read(key: StorageKeys.token);
  }

  Future<void> deleteToken() async {
    await _storage.delete(key: StorageKeys.token);
  }

  Future<void> saveSessionId(String id) async {
    await _storage.write(key: StorageKeys.sessionId, value: id);
  }

  Future<String?> getSessionId() async {
    return _storage.read(key: StorageKeys.sessionId);
  }

  Future<void> saveUsername(String name) async {
    await _storage.write(key: StorageKeys.username, value: name);
  }

  Future<String?> getUsername() async {
    return _storage.read(key: StorageKeys.username);
  }

  Future<void> saveUserRole(String role) async {
    await _storage.write(key: StorageKeys.userRole, value: role);
  }

  Future<String?> getUserRole() async {
    return _storage.read(key: StorageKeys.userRole);
  }

  Future<void> clearAll() async {
    await _storage.delete(key: StorageKeys.token);
    await _storage.delete(key: StorageKeys.sessionId);
    await _storage.delete(key: StorageKeys.username);
    await _storage.delete(key: StorageKeys.userRole);
  }
}
