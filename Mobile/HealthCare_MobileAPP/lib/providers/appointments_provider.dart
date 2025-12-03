import 'package:flutter/material.dart';
import '../services/api_service.dart';

/// Appointments provider for fetching and managing customer appointments
class AppointmentsProvider extends ChangeNotifier {
  final ApiService _api = ApiService();

  List<Map<String, dynamic>> _appointments = [];
  bool _isLoading = false;
  String? _errorMessage;

  List<Map<String, dynamic>> get appointments => _appointments;
  bool get isLoading => _isLoading;
  String? get errorMessage => _errorMessage;
  bool get hasAppointments => _appointments.isNotEmpty;

  /// Fetch all appointments for current customer
  Future<void> fetchAppointments() async {
    _isLoading = true;
    _errorMessage = null;
    notifyListeners();

    try {
      _appointments = await _api.getAppointments();
      _isLoading = false;
      notifyListeners();
    } catch (e) {
      _isLoading = false;
      _errorMessage = _parseErrorMessage(e.toString());
      notifyListeners();
    }
  }

  /// Refresh appointments (with loading indicator)
  Future<void> refreshAppointments() async {
    await fetchAppointments();
  }

  /// Create new appointment
  Future<Map<String, dynamic>> createAppointment({
    required DateTime date,
    String? description,
  }) async {
    _isLoading = true;
    notifyListeners();

    try {
      await _api.createAppointment(date, description: description);
      
      // Refresh list after creating
      await fetchAppointments();
      
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

  /// Get upcoming appointments (future dates)
  List<Map<String, dynamic>> get upcomingAppointments {
    final now = DateTime.now();
    return _appointments.where((apt) {
      try {
        final dateStr = apt['appointmentDate'] ?? apt['APPOINTMENT_DATE'];
        if (dateStr == null) return false;
        final aptDate = DateTime.parse(dateStr.toString());
        return aptDate.isAfter(now);
      } catch (_) {
        return false;
      }
    }).toList();
  }

  /// Get past appointments
  List<Map<String, dynamic>> get pastAppointments {
    final now = DateTime.now();
    return _appointments.where((apt) {
      try {
        final dateStr = apt['appointmentDate'] ?? apt['APPOINTMENT_DATE'];
        if (dateStr == null) return false;
        final aptDate = DateTime.parse(dateStr.toString());
        return aptDate.isBefore(now);
      } catch (_) {
        return false;
      }
    }).toList();
  }

  /// Clear appointments (on logout)
  void clearAppointments() {
    _appointments = [];
    _errorMessage = null;
    notifyListeners();
  }

  String _parseErrorMessage(String error) {
    if (error.contains('Network') || error.contains('Connection')) {
      return 'Không thể kết nối server';
    }
    if (error.contains('Timeout')) {
      return 'Yêu cầu hết thời gian chờ';
    }
    if (error.contains('401') || error.contains('Unauthorized')) {
      return 'Phiên đăng nhập hết hạn';
    }
    return 'Lỗi: ${error.length > 100 ? error.substring(0, 100) : error}';
  }
}
