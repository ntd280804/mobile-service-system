import 'package:flutter/material.dart';
import '../services/api_service.dart';
import '../models/order.dart';

/// Orders provider for fetching and managing customer orders
class OrdersProvider extends ChangeNotifier {
  final ApiService _api = ApiService();

  List<Order> _orders = [];
  bool _isLoading = false;
  String? _errorMessage;

  List<Order> get orders => _orders;
  bool get isLoading => _isLoading;
  String? get errorMessage => _errorMessage;
  bool get hasOrders => _orders.isNotEmpty;

  /// Fetch all orders for current customer
  Future<void> fetchOrders() async {
    _isLoading = true;
    _errorMessage = null;
    notifyListeners();

    try {
      _orders = await _api.getOrders();
      _isLoading = false;
      notifyListeners();
    } catch (e) {
      _isLoading = false;
      _errorMessage = _parseErrorMessage(e.toString());
      notifyListeners();
    }
  }

  /// Refresh orders (with loading indicator)
  Future<void> refreshOrders() async {
    await fetchOrders();
  }

  /// Get order by ID
  Order? getOrderById(int orderId) {
    try {
      return _orders.firstWhere((order) => int.tryParse(order.orderId) == orderId);
    } catch (_) {
      return null;
    }
  }

  /// Filter orders by status
  List<Order> getOrdersByStatus(String status) {
    return _orders.where((order) => 
      order.status.toLowerCase() == status.toLowerCase()
    ).toList();
  }

  /// Get pending orders
  List<Order> get pendingOrders => getOrdersByStatus('Pending');

  /// Get completed orders
  List<Order> get completedOrders => getOrdersByStatus('Completed');

  /// Clear orders (on logout)
  void clearOrders() {
    _orders = [];
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
    return 'Lỗi tải đơn hàng: ${error.length > 100 ? error.substring(0, 100) : error}';
  }
}
