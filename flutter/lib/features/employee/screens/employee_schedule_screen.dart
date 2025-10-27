import 'package:flutter/material.dart';

class EmployeeScheduleScreen extends StatelessWidget {
  const EmployeeScheduleScreen({super.key});

  @override
  Widget build(BuildContext context) {
    return Padding(
      key: const ValueKey('employee_schedule'),
      padding: const EdgeInsets.all(16.0),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          const Text('Lịch làm việc', style: TextStyle(fontSize: 22, fontWeight: FontWeight.bold)),
          const SizedBox(height: 12),
          const Center(child: Text('Hiện thị lịch làm việc'))
        ],
      ),
    );
  }
}
