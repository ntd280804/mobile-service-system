import 'package:flutter/material.dart';
import '../services/api_service.dart';

class EmployeeExportListScreen extends StatefulWidget {
  const EmployeeExportListScreen({super.key});

  @override
  State<EmployeeExportListScreen> createState() => _EmployeeExportListScreenState();
}

class _EmployeeExportListScreenState extends State<EmployeeExportListScreen> {
  final ApiService _api = ApiService();
  List<Map<String, dynamic>> _exports = [];
  bool _isLoading = true;

  @override
  void initState() {
    super.initState();
    _loadExports();
  }

  Future<void> _loadExports() async {
    setState(() => _isLoading = true);
    try {
      final exportsData = await _api.getAllExports();
      // Sort by date descending (newest first)
      exportsData.sort((a, b) {
        final dateA = _parseDate(a['outDate'] ?? a['OutDate']);
        final dateB = _parseDate(b['outDate'] ?? b['OutDate']);
        return dateB.compareTo(dateA);
      });
      setState(() {
        _exports = exportsData;
        _isLoading = false;
      });
    } catch (e) {
      setState(() => _isLoading = false);
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(
            content: Text('Không thể tải hóa đơn xuất: ${e.toString()}'),
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

  void _navigateToDetail(Map<String, dynamic> export) {
    final stockOutId = export['stockOutId'] ?? export['StockOutId'] ?? '';
    if (stockOutId.toString().isEmpty) return;
    Navigator.pushNamed(
      context,
      '/employee/export/detail',
      arguments: stockOutId,
    );
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: const Text('Hóa đơn xuất'),
        actions: [
          IconButton(
            icon: const Icon(Icons.refresh),
            onPressed: _loadExports,
          ),
        ],
      ),
      body: _isLoading
          ? const Center(child: CircularProgressIndicator())
          : RefreshIndicator(
              onRefresh: _loadExports,
              child: _exports.isEmpty
                  ? const Center(child: Text('Không có hóa đơn xuất nào.'))
                  : ListView.separated(
                      padding: const EdgeInsets.all(12),
                      itemCount: _exports.length,
                      separatorBuilder: (_, __) => const SizedBox(height: 8),
                      itemBuilder: (context, index) {
                        final export = _exports[index];
                        final stockOutId = export['stockOutId'] ?? export['StockOutId'] ?? '';
                        final empUsername = export['empUsername'] ?? export['EmpUsername'] ?? '';
                        final outDate = export['outDate'] ?? export['OutDate'] ?? '';
                        final note = export['note'] ?? export['Note'] ?? '';

                        return Card(
                          child: ListTile(
                            leading: const Icon(Icons.arrow_upward, color: Colors.orange),
                            title: Text(
                              'ID: ${stockOutId.toString()}',
                              style: const TextStyle(fontWeight: FontWeight.bold),
                            ),
                            subtitle: Column(
                              crossAxisAlignment: CrossAxisAlignment.start,
                              children: [
                                if (empUsername.toString().isNotEmpty)
                                  Text('Nhân viên: $empUsername'),
                                Text('Ngày: ${_formatDate(outDate)}'),
                                if (note.toString().isNotEmpty)
                                  Text('Ghi chú: $note'),
                              ],
                            ),
                            trailing: const Icon(Icons.chevron_right),
                            onTap: () => _navigateToDetail(export),
                          ),
                        );
                      },
                    ),
            ),
    );
  }
}

