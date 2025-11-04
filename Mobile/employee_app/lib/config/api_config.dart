class ApiConfig {
  // Backend API base URL
  static const String baseUrl = 'https://10.147.20.199:5131';

  // Admin endpoints
  static const String login = '/api/Admin/Employee/login';
  static const String loginSecure = '/api/Admin/Employee/login-secure';
  static const String publicKey = '/api/Admin/Employee/public-key';
  static const String logout = '/api/Admin/Employee/logout';
  static const String getAllAppointments = '/api/Admin/Appointment/all';
  static const String getAllOrders = '/api/Admin/Order';
  static const String getOrdersByType = '/api/Admin/Order/by-order-type';
}
