class ApiConfig {
  // Backend API base URL
  // ✅ FIXED: Khớp với WebApp và Backend server
  static const String baseUrl = 'https://10.147.20.199:5131';

  // Public endpoints
  static const String login = '/api/Public/Customer/login';
  static const String register = '/api/Public/Customer/register';
  static const String ChangePass = '/api/Public/Customer/change-password';
  static const String logout = '/api/Public/Customer/logout';
  static const String getOrders = '/api/Public/Order/all';
  static const String getAppointments = '/api/Public/Appointment/all';
  static const String createAppointment = '/api/Public/Appointment/Create';
}
