import 'package:flutter/material.dart';
import '../../../core/services/api_service.dart';
import '../../../core/models/import_stock.dart';

class EmployeeImportsScreen extends StatefulWidget {
  const EmployeeImportsScreen({super.key});

  @override
  State<EmployeeImportsScreen> createState() => _EmployeeImportsScreenState();
}

class _EmployeeImportsScreenState extends State<EmployeeImportsScreen> {
  late Future<List<ImportStock>> _future;

  @override
  void initState() {
    super.initState();
    _future = ApiService.getImports();
  }

  Future<void> _refresh() async {
    setState(() => _future = ApiService.getImports());
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      body: Padding(
        key: const ValueKey('employee_imports'),
        padding: const EdgeInsets.all(8.0),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            const SizedBox(height: 8),
            const Text('Nhập kho', style: TextStyle(fontSize: 22, fontWeight: FontWeight.bold)),
            const SizedBox(height: 8),
            Expanded(
              child: FutureBuilder<List<ImportStock>>(
                future: _future,
                builder: (context, snapshot) {
                  if (snapshot.connectionState == ConnectionState.waiting) return const Center(child: CircularProgressIndicator());
                  if (snapshot.hasError) return Center(child: Text('Lỗi: ${snapshot.error}'));
                  final list = snapshot.data ?? [];
                  if (list.isEmpty) return const Center(child: Text('Không có bản ghi nhập kho'));

                  return RefreshIndicator(
                    onRefresh: _refresh,
                    child: ListView.separated(
                      itemCount: list.length,
                      separatorBuilder: (_, __) => const Divider(),
                      itemBuilder: (context, index) {
                        final it = list[index];
                        return ListTile(
                          title: Text('StockIn #${it.stockInId}'),
                          subtitle: Text('${it.empUsername} • ${it.inDate.toLocal()}'),
                          trailing: const Icon(Icons.chevron_right),
                          onTap: () {
                            // TODO: navigate to import details
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
