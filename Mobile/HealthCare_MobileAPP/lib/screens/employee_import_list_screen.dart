import 'package:flutter/material.dart';
import '../services/api_service.dart';

class EmployeeImportListScreen extends StatefulWidget {
  const EmployeeImportListScreen({super.key});

  @override
  State<EmployeeImportListScreen> createState() => _EmployeeImportListScreenState();
}

class _EmployeeImportListScreenState extends State<EmployeeImportListScreen> {
  final ApiService _api = ApiService();
  List<Map<String, dynamic>> _imports = [];
  bool _isLoading = true;

  @override
  void initState() {
    super.initState();
    _loadImports();
  }

  Future<void> _loadImports() async {
    setState(() => _isLoading = true);
    try {
      final importsData = await _api.getAllImports();
      // Sort by date descending (newest first)
      importsData.sort((a, b) {
        final dateA = _parseDate(a['outDate'] ?? a['OutDate'] ?? a['inDate'] ?? a['InDate']);
        final dateB = _parseDate(b['outDate'] ?? b['OutDate'] ?? b['inDate'] ?? b['InDate']);
        return dateB.compareTo(dateA);
      });
      setState(() {
        _imports = importsData;
        _isLoading = false;
      });
    } catch (e) {
      setState(() => _isLoading = false);
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(
            content: Text('Không thể tải hóa đơn nhập: ${e.toString()}'),
            backgroundColor: Colors.red,
          ),
        );
      }
    }
  }

  DateTime _parseDate(dynamic date) {
    if (date == null) return DateTime(1970);
    try {
      if (date is String) {
        final parsed = DateTime.tryParse(date);
        if (parsed != null) return parsed;
      } else if (date is DateTime) {
        return date;
      }
    } catch (_) {}
    return DateTime(1970);
  }

  String _formatDate(dynamic date) {
    if (date == null) return 'N/A';
    try {
      if (date is String) {
        final parsed = DateTime.tryParse(date);
        if (parsed != null) {
          return parsed.toLocal().toString().substring(0, 16);
        }
        return date;
      } else if (date is DateTime) {
        return date.toLocal().toString().substring(0, 16);
      }
      return date.toString();
    } catch (_) {
      return date.toString();
    }
  }

  void _navigateToDetail(Map<String, dynamic> import) {
    final stockInId = import['stockInId'] ?? import['StockInId'] ?? '';
    if (stockInId.toString().isEmpty) return;
    Navigator.pushNamed(
      context,
      '/employee/import/detail',
      arguments: stockInId,
    );
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: const Text('Hóa đơn nhập'),
        actions: [
          IconButton(
            icon: const Icon(Icons.refresh),
            onPressed: _loadImports,
          ),
        ],
      ),
      body: _isLoading
          ? const Center(child: CircularProgressIndicator())
          : RefreshIndicator(
              onRefresh: _loadImports,
              child: _imports.isEmpty
                  ? const Center(child: Text('Không có hóa đơn nhập nào.'))
                  : ListView.separated(
                      padding: const EdgeInsets.all(12),
                      itemCount: _imports.length,
                      separatorBuilder: (_, __) => const SizedBox(height: 8),
                      itemBuilder: (context, index) {
                        final import = _imports[index];
                        final stockInId = import['stockInId'] ?? import['StockInId'] ?? '';
                        final empUsername = import['empUsername'] ?? import['EmpUsername'] ?? '';
                        final inDate = import['outDate'] ?? import['OutDate'] ?? import['inDate'] ?? import['InDate'];
                        final note = import['note'] ?? import['Note'] ?? '';

                        return Card(
                          child: ListTile(
                            leading: const Icon(Icons.arrow_downward, color: Colors.blue),
                            title: Text(
                              'ID: ${stockInId.toString()}',
                              style: const TextStyle(fontWeight: FontWeight.bold),
                            ),
                            subtitle: Column(
                              crossAxisAlignment: CrossAxisAlignment.start,
                              children: [
                                if (empUsername.toString().isNotEmpty)
                                  Text('Nhân viên: $empUsername'),
                                Text('Ngày: ${_formatDate(inDate)}'),
                                if (note.toString().isNotEmpty)
                                  Text('Ghi chú: $note'),
                              ],
                            ),
                            trailing: const Icon(Icons.chevron_right),
                            onTap: () => _navigateToDetail(import),
                          ),
                        );
                      },
                    ),
            ),
    );
  }
}

