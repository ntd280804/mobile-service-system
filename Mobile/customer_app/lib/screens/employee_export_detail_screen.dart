import 'dart:io';
import 'package:flutter/material.dart';
import 'package:path_provider/path_provider.dart';
import 'package:open_filex/open_filex.dart';
import '../services/api_service.dart';

class EmployeeExportDetailScreen extends StatefulWidget {
  final int stockOutId;

  const EmployeeExportDetailScreen({super.key, required this.stockOutId});

  @override
  State<EmployeeExportDetailScreen> createState() => _EmployeeExportDetailScreenState();
}

class _EmployeeExportDetailScreenState extends State<EmployeeExportDetailScreen> {
  final ApiService _api = ApiService();
  Map<String, dynamic>? _details;
  bool _isLoading = true;
  bool _isDownloading = false;
  bool _isVerifying = false;

  @override
  void initState() {
    super.initState();
    _loadDetails();
  }

  Future<void> _loadDetails() async {
    setState(() => _isLoading = true);
    try {
      final details = await _api.getExportDetails(widget.stockOutId);
      setState(() {
        _details = details;
        _isLoading = false;
      });
    } catch (e) {
      setState(() => _isLoading = false);
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(
            content: Text('Không thể tải chi tiết: ${e.toString()}'),
            backgroundColor: Colors.red,
          ),
        );
      }
    }
  }

  Future<void> _downloadPdf() async {
    setState(() => _isDownloading = true);
    try {
      final pdfBytes = await _api.downloadExportPdf(widget.stockOutId);
      
      final directory = await getApplicationDocumentsDirectory();
      final file = File('${directory.path}/ExportInvoice_${widget.stockOutId}.pdf');
      await file.writeAsBytes(pdfBytes);
      
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('Đã tải PDF thành công')),
      );
      
      await OpenFilex.open(file.path);
    } catch (e) {
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: Text('Không thể tải PDF: ${e.toString()}'),
          backgroundColor: Colors.red,
        ),
      );
    } finally {
      if (mounted) {
        setState(() => _isDownloading = false);
      }
    }
  }

  Future<void> _verifySign() async {
    setState(() => _isVerifying = true);
    try {
      final result = await _api.verifyExportSign(widget.stockOutId);
      final isValid = result['isValid'] == true || result['isValid'] == 1;
      final message = result['message'] ?? result['Message'] ?? '';
      final stockOutId = result['StockOutId'] ?? result['stockOutId'] ?? widget.stockOutId;
      
      if (!mounted) return;
      showDialog(
        context: context,
        builder: (ctx) => AlertDialog(
          title: Text(isValid ? 'Xác thực thành công ✅' : 'Xác thực thất bại ❌'),
          content: Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(message.toString().isNotEmpty ? message.toString() : 
                  (isValid ? 'Chữ ký số hợp lệ' : 'Chữ ký số không hợp lệ')),
              const SizedBox(height: 8),
              Text('StockOut ID: $stockOutId'),
            ],
          ),
          actions: [
            TextButton(
              onPressed: () => Navigator.of(ctx).pop(),
              child: const Text('Đóng'),
            ),
          ],
        ),
      );
    } catch (e) {
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: Text('Không thể xác thực: ${e.toString()}'),
          backgroundColor: Colors.red,
        ),
      );
    } finally {
      if (mounted) {
        setState(() => _isVerifying = false);
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: Text('Chi tiết hóa đơn xuất #${widget.stockOutId}'),
      ),
      body: _isLoading
          ? const Center(child: CircularProgressIndicator())
          : _details == null
              ? const Center(child: Text('Không tìm thấy dữ liệu'))
              : SingleChildScrollView(
                  padding: const EdgeInsets.all(16),
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.stretch,
                    children: [
                      Card(
                        child: Padding(
                          padding: const EdgeInsets.all(16),
                          child: Column(
                            crossAxisAlignment: CrossAxisAlignment.start,
                            children: [
                              Text(
                                'Thông tin chung',
                                style: Theme.of(context).textTheme.titleLarge,
                              ),
                              const SizedBox(height: 12),
                              _buildInfoRow('ID', _details!['stockOutId']?.toString() ?? ''),
                              _buildInfoRow('Nhân viên', _details!['empUsername']?.toString() ?? ''),
                              _buildInfoRow('Ngày', _formatDate(_details!['outDate'])),
                              if (_details!['note'] != null)
                                _buildInfoRow('Ghi chú', _details!['note']?.toString() ?? ''),
                            ],
                          ),
                        ),
                      ),
                      const SizedBox(height: 16),
                      if (_details!['items'] != null)
                        Card(
                          child: Padding(
                            padding: const EdgeInsets.all(16),
                            child: Column(
                              crossAxisAlignment: CrossAxisAlignment.start,
                              children: [
                                Text(
                                  'Chi tiết sản phẩm',
                                  style: Theme.of(context).textTheme.titleLarge,
                                ),
                                const SizedBox(height: 12),
                                ...((_details!['items'] as List?) ?? []).map((item) {
                                  return Padding(
                                    padding: const EdgeInsets.only(bottom: 8),
                                    child: Column(
                                      crossAxisAlignment: CrossAxisAlignment.start,
                                      children: [
                                        Text(
                                          item['partName']?.toString() ?? item['PartName']?.toString() ?? '',
                                          style: const TextStyle(fontWeight: FontWeight.bold),
                                        ),
                                        if (item['manufacturer'] != null)
                                          Text('Nhà sản xuất: ${item['manufacturer'] ?? item['Manufacturer']}'),
                                        if (item['serial'] != null)
                                          Text('Serial: ${item['serial'] ?? item['Serial']}'),
                                        if (item['price'] != null)
                                          Text('Giá: ${item['price'] ?? item['Price']}'),
                                      ],
                                    ),
                                  );
                                }),
                              ],
                            ),
                          ),
                        ),
                      const SizedBox(height: 24),
                      Row(
                        children: [
                          Expanded(
                            child: ElevatedButton.icon(
                              onPressed: _isDownloading ? null : _downloadPdf,
                              icon: _isDownloading
                                  ? const SizedBox(
                                      width: 16,
                                      height: 16,
                                      child: CircularProgressIndicator(strokeWidth: 2),
                                    )
                                  : const Icon(Icons.download),
                              label: const Text('Tải PDF'),
                              style: ElevatedButton.styleFrom(
                                backgroundColor: Colors.green,
                                foregroundColor: Colors.white,
                                padding: const EdgeInsets.symmetric(vertical: 12),
                              ),
                            ),
                          ),
                          const SizedBox(width: 12),
                          Expanded(
                            child: ElevatedButton.icon(
                              onPressed: _isVerifying ? null : _verifySign,
                              icon: _isVerifying
                                  ? const SizedBox(
                                      width: 16,
                                      height: 16,
                                      child: CircularProgressIndicator(strokeWidth: 2),
                                    )
                                  : const Icon(Icons.verified),
                              label: const Text('Xác thực'),
                              style: ElevatedButton.styleFrom(
                                backgroundColor: Colors.orange,
                                foregroundColor: Colors.white,
                                padding: const EdgeInsets.symmetric(vertical: 12),
                              ),
                            ),
                          ),
                        ],
                      ),
                    ],
                  ),
                ),
    );
  }

  Widget _buildInfoRow(String label, String value) {
    return Padding(
      padding: const EdgeInsets.only(bottom: 8),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          SizedBox(
            width: 100,
            child: Text(
              '$label:',
              style: const TextStyle(fontWeight: FontWeight.bold),
            ),
          ),
          Expanded(child: Text(value)),
        ],
      ),
    );
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
}

