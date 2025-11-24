import 'package:flutter/material.dart';
import '../services/api_service.dart';
import '../models/order.dart';

class OrderListScreen extends StatefulWidget {
  const OrderListScreen({super.key});

  @override
  State<OrderListScreen> createState() => _OrderListScreenState();
}

class _OrderListScreenState extends State<OrderListScreen> {
  final ApiService _api = ApiService();
  late Future<List<Order>> _ordersFuture;

  @override
  void initState() {
    super.initState();
    _ordersFuture = _api.getOrders();
  }

  Future<void> _refreshOrders() async {
    setState(() {
      _ordersFuture = _api.getOrders();
    });
    await _ordersFuture;
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: const Text('Đơn hàng'),
        actions: [
          IconButton(
            icon: const Icon(Icons.refresh),
            onPressed: _refreshOrders,
            tooltip: 'Làm mới',
          ),
        ],
      ),
      body: FutureBuilder<List<Order>>(
        future: _ordersFuture,
        builder: (context, snapshot) {
          if (snapshot.connectionState == ConnectionState.waiting) {
            return const Center(child: CircularProgressIndicator());
          }

          if (snapshot.hasError) {
            return Center(
              child: Column(
                mainAxisAlignment: MainAxisAlignment.center,
                children: [
                  Icon(Icons.error_outline, size: 64, color: Colors.red[300]),
                  const SizedBox(height: 16),
                  Text(
                    'Lỗi: ${snapshot.error}',
                    style: TextStyle(color: Colors.red[700], fontSize: 16),
                    textAlign: TextAlign.center,
                  ),
                  const SizedBox(height: 16),
                  ElevatedButton.icon(
                    onPressed: _refreshOrders,
                    icon: const Icon(Icons.refresh),
                    label: const Text('Thử lại'),
                  ),
                ],
              ),
            );
          }

          final orders = snapshot.data ?? [];

          if (orders.isEmpty) {
            return RefreshIndicator(
              onRefresh: _refreshOrders,
              child: SingleChildScrollView(
                physics: const AlwaysScrollableScrollPhysics(),
                child: SizedBox(
                  height: MediaQuery.of(context).size.height * 0.7,
                  child: const Center(
                    child: Column(
                      mainAxisAlignment: MainAxisAlignment.center,
                      children: [
                        Icon(Icons.inbox_outlined, size: 64, color: Colors.grey),
                        SizedBox(height: 16),
                        Text(
                          'Chưa có đơn hàng',
                          style: TextStyle(color: Colors.grey, fontSize: 16),
                        ),
                      ],
                    ),
                  ),
                ),
              ),
            );
          }

          return RefreshIndicator(
            onRefresh: _refreshOrders,
            child: ListView.separated(
              padding: const EdgeInsets.all(12),
              itemCount: orders.length,
              separatorBuilder: (_, __) => const SizedBox(height: 8),
              itemBuilder: (context, index) {
                final order = orders[index];
                final date = order.receivedDate != null
                    ? order.receivedDate!.toLocal().toString().split(' ').first
                    : 'Không rõ ngày';

                // Xác định icon và màu dựa trên trạng thái
                IconData statusIcon;
                Color statusColor;
                if (order.status?.toUpperCase().contains('CANCELLED') == true ||
                    order.status?.toUpperCase().contains('HỦY') == true) {
                  statusIcon = Icons.cancel;
                  statusColor = Colors.red;
                } else if (order.status?.toUpperCase().contains('COMPLETED') == true ||
                    order.status?.toUpperCase().contains('HOÀN THÀNH') == true) {
                  statusIcon = Icons.check_circle;
                  statusColor = Colors.green;
                } else {
                  statusIcon = Icons.schedule;
                  statusColor = Colors.blue;
                }

                return Card(
                  elevation: 2,
                  shape: RoundedRectangleBorder(
                    borderRadius: BorderRadius.circular(12),
                  ),
                  child: ListTile(
                    leading: CircleAvatar(
                      backgroundColor: Colors.green,
                      radius: 28,
                      child: Text(
                        order.orderId,
                        style: const TextStyle(
                          color: Colors.white,
                          fontWeight: FontWeight.bold,
                          fontSize: 12,
                        ),
                      ),
                    ),
                    title: Text(
                      'Đơn hàng #${order.orderId}',
                      style: const TextStyle(
                        fontWeight: FontWeight.bold,
                        fontSize: 16,
                      ),
                    ),
                    subtitle: Padding(
                      padding: const EdgeInsets.only(top: 8),
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Text('Ngày nhận: $date'),
                          if (order.customerPhone != null)
                            Text('SĐT: ${order.customerPhone}'),
                          if (order.receiverEmpName != null)
                            Text('Người nhận: ${order.receiverEmpName}'),
                          if (order.handlerEmpName != null)
                            Text('Người xử lý: ${order.handlerEmpName}'),
                          if (order.orderType != null)
                            Text('Loại: ${order.orderType}'),
                          if (order.status != null)
                            Row(
                              children: [
                                Icon(statusIcon, size: 16, color: statusColor),
                                const SizedBox(width: 4),
                                Text(
                                  'Trạng thái: ${order.status}',
                                  style: TextStyle(color: statusColor),
                                ),
                              ],
                            ),
                        ],
                      ),
                    ),
                    isThreeLine: true,
                  ),
                );
              },
            ),
          );
        },
      ),
    );
  }
}
