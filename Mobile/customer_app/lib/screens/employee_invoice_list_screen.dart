import 'package:flutter/material.dart';
import '../services/api_service.dart';

class EmployeeInvoiceListScreen extends StatefulWidget {
  const EmployeeInvoiceListScreen({super.key});

  @override
  State<EmployeeInvoiceListScreen> createState() => _EmployeeInvoiceListScreenState();
}

class _EmployeeInvoiceListScreenState extends State<EmployeeInvoiceListScreen> {
  final ApiService _api = ApiService();
  List<Map<String, dynamic>> _invoices = [];
  bool _isLoading = true;

  @override
  void initState() {
    super.initState();
    _loadInvoices();
  }

  Future<void> _loadInvoices() async {
    setState(() => _isLoading = true);
    try {
      final invoicesData = await _api.getAllInvoices();
      // Sort by date descending (newest first)
      invoicesData.sort((a, b) {
        final dateA = _parseDate(a['invoiceDate'] ?? a['InvoiceDate']);
        final dateB = _parseDate(b['invoiceDate'] ?? b['InvoiceDate']);
        return dateB.compareTo(dateA);
      });
      setState(() {
        _invoices = invoicesData;
        _isLoading = false;
      });
    } catch (e) {
      setState(() => _isLoading = false);
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(
            content: Text('Không thể tải hóa đơn: ${e.toString()}'),
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

  void _navigateToDetail(Map<String, dynamic> invoice) {
    final invoiceId = invoice['invoiceId'] ?? invoice['InvoiceId'] ?? '';
    if (invoiceId.toString().isEmpty) return;
    Navigator.pushNamed(
      context,
      '/employee/invoice/detail',
      arguments: invoiceId,
    );
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: const Text('Hóa đơn'),
        actions: [
          IconButton(
            icon: const Icon(Icons.refresh),
            onPressed: _loadInvoices,
          ),
        ],
      ),
      body: _isLoading
          ? const Center(child: CircularProgressIndicator())
          : RefreshIndicator(
              onRefresh: _loadInvoices,
              child: _invoices.isEmpty
                  ? const Center(child: Text('Không có hóa đơn nào.'))
                  : ListView.separated(
                      padding: const EdgeInsets.all(12),
                      itemCount: _invoices.length,
                      separatorBuilder: (_, __) => const SizedBox(height: 8),
                      itemBuilder: (context, index) {
                        final invoice = _invoices[index];
                        final invoiceId = invoice['invoiceId'] ?? invoice['InvoiceId'] ?? '';
                        final stockOutId = invoice['stockOutId'] ?? invoice['StockOutId'] ?? '';
                        final customerPhone = invoice['customerPhone'] ?? invoice['CustomerPhone'] ?? '';
                        final employee = invoice['employee'] ?? invoice['Employee'] ?? '';
                        final invoiceDate = invoice['invoiceDate'] ?? invoice['InvoiceDate'];
                        final totalAmount = invoice['totalAmount'] ?? invoice['TotalAmount'];
                        final status = invoice['status'] ?? invoice['Status'] ?? '';

                        return Card(
                          child: ListTile(
                            leading: const Icon(Icons.receipt, color: Colors.purple),
                            title: Text(
                              'Mã: ${invoiceId.toString()}',
                              style: const TextStyle(fontWeight: FontWeight.bold),
                            ),
                            subtitle: Column(
                              crossAxisAlignment: CrossAxisAlignment.start,
                              children: [
                                if (stockOutId.toString().isNotEmpty)
                                  Text('StockOut ID: $stockOutId'),
                                if (customerPhone.toString().isNotEmpty)
                                  Text('SĐT: $customerPhone'),
                                if (employee.toString().isNotEmpty)
                                  Text('Nhân viên: $employee'),
                                Text('Ngày: ${_formatDate(invoiceDate)}'),
                                if (totalAmount != null)
                                  Text('Tổng tiền: ${totalAmount.toString()}'),
                                if (status.toString().isNotEmpty)
                                  Text('Trạng thái: $status'),
                              ],
                            ),
                            trailing: const Icon(Icons.chevron_right),
                            onTap: () => _navigateToDetail(invoice),
                          ),
                        );
                      },
                    ),
            ),
    );
  }
}

