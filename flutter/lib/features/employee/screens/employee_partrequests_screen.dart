import 'package:flutter/material.dart';
import '../../../core/services/api_service.dart';
import '../../../core/models/part_request.dart';

class EmployeePartRequestsScreen extends StatefulWidget {
  const EmployeePartRequestsScreen({super.key});

  @override
  State<EmployeePartRequestsScreen> createState() => _EmployeePartRequestsScreenState();
}

class _EmployeePartRequestsScreenState extends State<EmployeePartRequestsScreen> {
  late Future<List<PartRequest>> _future;

  @override
  void initState() {
    super.initState();
    _future = ApiService.getPartRequests();
  }

  Future<void> _refresh() async {
    setState(() => _future = ApiService.getPartRequests());
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      body: Padding(
        key: const ValueKey('employee_partrequests'),
        padding: const EdgeInsets.all(8.0),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            const SizedBox(height: 8),
            const Text('Yêu cầu linh kiện', style: TextStyle(fontSize: 22, fontWeight: FontWeight.bold)),
            const SizedBox(height: 8),
            Expanded(
              child: FutureBuilder<List<PartRequest>>(
                future: _future,
                builder: (context, snapshot) {
                  if (snapshot.connectionState == ConnectionState.waiting) return const Center(child: CircularProgressIndicator());
                  if (snapshot.hasError) return Center(child: Text('Lỗi: ${snapshot.error}'));
                  final list = snapshot.data ?? [];
                  if (list.isEmpty) return const Center(child: Text('Không có yêu cầu'));

                  return RefreshIndicator(
                    onRefresh: _refresh,
                    child: ListView.separated(
                      itemCount: list.length,
                      separatorBuilder: (_, __) => const Divider(),
                      itemBuilder: (context, index) {
                        final r = list[index];
                        return ListTile(
                          title: Text('Request #${r.requestId}'),
                          subtitle: Text('${r.empUsername} • ${r.status}'),
                          trailing: const Icon(Icons.chevron_right),
                          onTap: () {},
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
