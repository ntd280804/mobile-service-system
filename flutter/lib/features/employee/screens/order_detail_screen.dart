import 'package:flutter/material.dart';
import '../../../core/models/order.dart';

class OrderDetailScreen extends StatelessWidget {
  final Order order;
  const OrderDetailScreen({super.key, required this.order});

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: Text('Order #${order.orderId}')),
      body: Padding(
        padding: const EdgeInsets.all(16.0),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text('Order ID: ${order.orderId}', style: const TextStyle(fontWeight: FontWeight.bold)),
            const SizedBox(height: 8),
            Text('Customer phone: ${order.customerPhone}'),
            const SizedBox(height: 8),
            Text('Order type: ${order.orderType}'),
            const SizedBox(height: 8),
            Text('Status: ${order.status}'),
            const SizedBox(height: 8),
            Text('Received date: ${order.receivedDate.toLocal()}'),
            const SizedBox(height: 12),
            const Text('Description', style: TextStyle(fontWeight: FontWeight.bold)),
            const SizedBox(height: 6),
            Text(order.description.isNotEmpty ? order.description : '-'),
          ],
        ),
      ),
    );
  }
}
