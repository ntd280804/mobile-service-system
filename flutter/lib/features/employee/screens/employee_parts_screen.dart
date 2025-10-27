import 'package:flutter/material.dart';
import '../../../core/services/api_service.dart';
import '../../../core/models/part.dart';

class EmployeePartsScreen extends StatefulWidget {
  const EmployeePartsScreen({super.key});

  @override
  State<EmployeePartsScreen> createState() => _EmployeePartsScreenState();
}

class _EmployeePartsScreenState extends State<EmployeePartsScreen> {
  late Future<List<Part>> _future;

  @override
  void initState() {
    super.initState();
    _future = ApiService.getParts();
  }

  Future<void> _refresh() async {
    setState(() => _future = ApiService.getParts());
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      body: Padding(
        key: const ValueKey('employee_parts'),
        padding: const EdgeInsets.all(8.0),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            const SizedBox(height: 8),
            const Text('Linh kiện', style: TextStyle(fontSize: 22, fontWeight: FontWeight.bold)),
            const SizedBox(height: 8),
            Expanded(
              child: FutureBuilder<List<Part>>(
                future: _future,
                builder: (context, snapshot) {
                  if (snapshot.connectionState == ConnectionState.waiting) return const Center(child: CircularProgressIndicator());
                  if (snapshot.hasError) return Center(child: Text('Lỗi: ${snapshot.error}'));
                  final list = snapshot.data ?? [];
                  if (list.isEmpty) return const Center(child: Text('Không có linh kiện'));

                  return RefreshIndicator(
                    onRefresh: _refresh,
                    child: ListView.separated(
                      itemCount: list.length,
                      separatorBuilder: (_, __) => const Divider(),
                      itemBuilder: (context, index) {
                        final p = list[index];
                        return ListTile(
                          title: Text(p.name),
                          subtitle: Text('${p.serial} • ${p.status}'),
                          trailing: const Icon(Icons.chevron_right),
                          onTap: () {
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
