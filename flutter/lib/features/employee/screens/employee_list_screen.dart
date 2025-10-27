import 'package:flutter/material.dart';

class EmployeeListScreen extends StatelessWidget {
  const EmployeeListScreen({super.key});

  @override
  Widget build(BuildContext context) {
    return Padding(
      key: const ValueKey('employee_list'),
      padding: const EdgeInsets.all(16.0),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          const Text('Danh sách nhân viên', style: TextStyle(fontSize: 22, fontWeight: FontWeight.bold)),
          const SizedBox(height: 12),
          Expanded(
            child: ListView.separated(
              itemCount: 6,
              separatorBuilder: (_, __) => const Divider(),
              itemBuilder: (context, index) => ListTile(
                leading: CircleAvatar(child: Text('E${index + 1}')),
                title: Text('Nhân viên ${index + 1}'),
                subtitle: Text('username${index + 1}@example.com'),
                trailing: const Icon(Icons.chevron_right),
                onTap: () {},
              ),
            ),
          )
        ],
      ),
    );
  }
}
