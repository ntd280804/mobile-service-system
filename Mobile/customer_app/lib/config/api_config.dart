class ApiConfig {
  // Backend API base URL
  // ✅ FIXED: Khớp với WebApp và Backend server
  static const String baseUrl = 'https://10.147.20.199:5131';

  // Public endpoints (Customer)
  static const String login = '/api/Public/Customer/login';
  static const String register = '/api/Public/Customer/register';
  static const String ChangePass = '/api/Public/Customer/change-password';
  static const String logout = '/api/Public/Customer/logout';
  static const String getOrders = '/api/Common/Order';
  static const String getAppointments = '/api/Common/Appointment';
  static const String createAppointment = '/api/Public/Appointment';
  static const String qrLoginConfirm = '/api/Public/QrLogin/confirm';
  static const String webToMobileQrConfirm = '/api/Public/WebToMobileQr/confirm';

  // Admin endpoints (Employee)
  static const String loginEmployee = '/api/Admin/Employee/login';
  static const String logoutEmployee = '/api/Admin/Employee/logout';
  static const String changePasswordEmployee = '/api/Admin/Employee/change-password';
  static const String getAllAppointments = '/api/Common/Appointment';
  static const String getAllOrders = '/api/Common/Order';
  static const String getOrdersByType = '/api/Admin/Order/by-order-type';
  
  // Import/Export/Invoice endpoints
  static const String getAllImports = '/api/admin/Import';
  static const String getImportDetails = '/api/admin/Import';
  static const String getImportInvoice = '/api/admin/Import';
  static const String verifyImportSign = '/api/admin/Import';
  static const String getAllExports = '/api/admin/Export';
  static const String getExportDetails = '/api/admin/Export';
  static const String getExportInvoice = '/api/admin/Export';
  static const String verifyExportSign = '/api/admin/Export';
  static const String getAllInvoices = '/api/admin/Invoice';
  static const String getInvoiceDetails = '/api/admin/Invoice';
  static const String getInvoicePdf = '/api/admin/Invoice';
  static const String verifyInvoice = '/api/admin/Invoice';
  
  // Part endpoints
  static const String getPartsByOrderId = '/api/admin/Part';
  static const String getPartsByPartRequest = '/api/admin/Part';
  static const String getPartsByRequestId = '/api/admin/Partrequest';
  static const String getOrderDetails = '/api/admin/Order';
}
