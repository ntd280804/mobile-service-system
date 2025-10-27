import 'package:flutter/material.dart';
import '../../../core/services/api_service.dart';
import '../../../core/models/order.dart';
import 'order_detail_screen.dart';

class EmployeeOrdersScreen extends StatefulWidget {
  const EmployeeOrdersScreen({super.key});

  @override
  State<EmployeeOrdersScreen> createState() => _EmployeeOrdersScreenState();
}

class _EmployeeOrdersScreenState extends State<EmployeeOrdersScreen> {
  late Future<List<Order>> _future;

  @override
  void initState() {
    super.initState();
    _future = ApiService.getOrders();
  }

  Future<void> _refresh() async {
    setState(() => _future = ApiService.getOrders());
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      body: Padding(
        key: const ValueKey('employee_orders'),
        padding: const EdgeInsets.all(8.0),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            const SizedBox(height: 8),
            const Text('Đơn hàng', style: TextStyle(fontSize: 22, fontWeight: FontWeight.bold)),
            const SizedBox(height: 8),
            Expanded(
              child: FutureBuilder<List<Order>>(
                future: _future,
                builder: (context, snapshot) {
                  if (snapshot.connectionState == ConnectionState.waiting) {
                    return const Center(child: CircularProgressIndicator());
                  }
                  if (snapshot.hasError) {
                    return Center(child: Text('Lỗi: ${snapshot.error}'));
                  }
                  final list = snapshot.data ?? [];
                  if (list.isEmpty) return const Center(child: Text('Không có đơn hàng'));

                  return RefreshIndicator(
                    onRefresh: _refresh,
                    child: ListView.separated(
                      itemCount: list.length,
                      separatorBuilder: (_, __) => const Divider(),
                      itemBuilder: (context, index) {
                        final o = list[index];
                        return ListTile(
                          title: Text('#${o.orderId} - ${o.orderType}'),
                          subtitle: Text('${o.customerPhone} • ${o.status}'),
                          trailing: const Icon(Icons.chevron_right),
                          onTap: () {
                            Navigator.of(context).push(MaterialPageRoute(builder: (_) => OrderDetailScreen(order: o)));
                          },
                        );
                      },
                    ),
                  );
                },
              ),
            )
          ],
        ),
      ),
    );
  }
}
