import 'package:flutter/material.dart';
import '../models/employee_order.dart';
import '../services/api_service.dart';
import 'employee_order_detail_screen.dart';

class EmployeeOrderListScreen extends StatefulWidget {
  const EmployeeOrderListScreen({super.key});

  @override
  State<EmployeeOrderListScreen> createState() => _EmployeeOrderListScreenState();
}

class _EmployeeOrderListScreenState extends State<EmployeeOrderListScreen> {
  final ApiService _api = ApiService();
  List<EmployeeOrder> _orders = [];
  bool _isLoading = true;

  @override
  void initState() {
    super.initState();
    _loadOrders();
  }

  Future<void> _loadOrders() async {
    setState(() => _isLoading = true);
    try {
      final ordersData = await _api.getAllOrders();
      setState(() {
        _orders = ordersData
            .map((json) => EmployeeOrder.fromJson(json))
            .toList();
        _isLoading = false;
      });
    } catch (e) {
      setState(() => _isLoading = false);
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(
            content: Text('Không thể tải đơn hàng: ${e.toString()}'),
            backgroundColor: Colors.red,
          ),
        );
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: const Text('Đơn hàng'),
        actions: [
          IconButton(
            icon: const Icon(Icons.refresh),
            onPressed: _loadOrders,
          ),
        ],
      ),
      body: _isLoading
          ? const Center(child: CircularProgressIndicator())
          : RefreshIndicator(
              onRefresh: _loadOrders,
              child: _orders.isEmpty
                  ? const Center(child: Text('Không có đơn hàng nào.'))
                  : ListView.separated(
                      padding: const EdgeInsets.all(12),
                      itemCount: _orders.length,
                      separatorBuilder: (_, __) => const SizedBox(height: 8),
                      itemBuilder: (context, index) {
                        final order = _orders[index];
                        return Card(
                          child: ListTile(
                            leading: const Icon(Icons.shopping_cart),
                            title: Text(
                              'Đơn hàng #${order.orderId}',
                              style: const TextStyle(fontWeight: FontWeight.bold),
                            ),
                            subtitle: Text(
                              'KH: ${order.customerPhone}\nNgày: ${order.orderDate.toLocal().toString().substring(0, 16)}\nLoại: ${order.orderType}\nTrạng thái: ${order.status}',
                            ),
                            trailing: order.status == 'CANCELLED'
                                ? const Icon(Icons.cancel, color: Colors.red)
                                : order.status == 'COMPLETED'
                                    ? const Icon(Icons.check_circle, color: Colors.green)
                                    : const Icon(Icons.schedule, color: Colors.blue),
                            onTap: () {
                              Navigator.push(
                                context,
                                MaterialPageRoute(
                                  builder: (context) => EmployeeOrderDetailScreen(orderId: order.orderId),
                                ),
                              );
                            },
                          ),
                        );
                      },
                    ),
            ),
    );
  }
}

