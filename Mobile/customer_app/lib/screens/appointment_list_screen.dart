import 'package:flutter/material.dart';

class AppointmentListScreen extends StatelessWidget {
  const AppointmentListScreen({super.key});

  @override
  Widget build(BuildContext context) {
    // Empty state đơn giản
    return Center(
      child: Column(
        mainAxisAlignment: MainAxisAlignment.center,
        children: const [
          Icon(Icons.event_outlined, size: 64, color: Colors.grey),
          SizedBox(height: 16),
          Text('Chưa có lịch hẹn', style: TextStyle(color: Colors.grey)),
        ],
      ),
    );
  }
}
