class ApiConfig {
  // Backend API base URL
  // ✅ FIXED: Khớp với WebApp và Backend server
  static const String baseUrl = 'https://10.147.20.199:5131';

  // Public endpoints (Customer)
  static const String login = '/api/Public/Customer/login';
  static const String register = '/api/Public/Customer/register';
  static const String ChangePass = '/api/Public/Customer/change-password';
  static const String logout = '/api/Public/Customer/logout';
  static const String getOrders = '/api/Public/Order/all';
  static const String getAppointments = '/api/Public/Appointment/all';
  static const String createAppointment = '/api/Public/Appointment/Create';
  static const String qrLoginConfirm = '/api/Public/QrLogin/confirm';

  // Admin endpoints (Employee)
  static const String loginEmployee = '/api/Admin/Employee/login';
  static const String logoutEmployee = '/api/Admin/Employee/logout';
  static const String changePasswordEmployee = '/api/Admin/Employee/change-password';
  static const String getAllAppointments = '/api/Admin/Appointment/all';
  static const String getAllOrders = '/api/Admin/Order';
  static const String getOrdersByType = '/api/Admin/Order/by-order-type';
  
  // Import/Export/Invoice endpoints
  static const String getAllImports = '/api/admin/import/getallimport';
  static const String getImportDetails = '/api/admin/Import/details';
  static const String getImportInvoice = '/api/admin/Import/invoice';
  static const String verifyImportSign = '/api/admin/Import/verifysign';
  static const String getAllExports = '/api/admin/export/getallexport';
  static const String getExportDetails = '/api/admin/Export/details';
  static const String getExportInvoice = '/api/admin/Export/invoice';
  static const String verifyExportSign = '/api/admin/Export/verifysign';
  static const String getAllInvoices = '/api/admin/invoice';
  static const String getInvoiceDetails = '/api/admin/invoice';
  static const String getInvoicePdf = '/api/admin/invoice';
  static const String verifyInvoice = '/api/admin/invoice';
  
  // Part endpoints
  static const String getPartsByOrderId = '/api/admin/part/by-order-id';
  static const String getPartsByPartRequest = '/api/admin/part/by-part-request';
  static const String getPartsByRequestId = '/api/admin/partrequest/by-request-id';
  static const String getOrderDetails = '/api/admin/order/details';
}
