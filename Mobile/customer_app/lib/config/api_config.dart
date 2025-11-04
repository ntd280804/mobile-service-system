class ApiConfig {
  // Backend API base URL
  // ✅ FIXED: Khớp với WebApp và Backend server
  static const String baseUrl = 'https://10.147.20.199:5131';

  // Public endpoints
  static const String login = '/api/Public/Customer/login';
  static const String loginSecure = '/api/Public/Customer/login-secure';
  static const String publicKey = '/api/Public/Customer/public-key';
  static const String register = '/api/Public/Customer/register';
  static const String logout = '/api/Public/Customer/logout';
  static const String getOrders = '/api/Public/Customer/orders';
  static const String getAppointmentsByPhone = '/api/Public/Appointment/get-by-phone';
  static const String createAppointment = '/api/Public/Appointment/Create';
}
