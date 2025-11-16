import 'package:flutter/material.dart';
import '../services/api_service.dart';
import '../models/part.dart';

class EmployeeOrderDetailScreen extends StatefulWidget {
  final int orderId;

  const EmployeeOrderDetailScreen({super.key, required this.orderId});

  @override
  State<EmployeeOrderDetailScreen> createState() => _EmployeeOrderDetailScreenState();
}

class _EmployeeOrderDetailScreenState extends State<EmployeeOrderDetailScreen> {
  final ApiService _api = ApiService();
  Map<String, dynamic>? _orderDetails;
  List<Part> _parts = [];
  bool _isLoading = true;
  bool _isLoadingParts = true;

  @override
  void initState() {
    super.initState();
    _loadDetails();
    _loadParts();
  }

  Future<void> _loadDetails() async {
    setState(() => _isLoading = true);
    try {
      final details = await _api.getOrderDetails(widget.orderId);
      setState(() {
        _orderDetails = details;
        _isLoading = false;
      });
    } catch (e) {
      setState(() => _isLoading = false);
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(
            content: Text('Không thể tải chi tiết đơn hàng: ${e.toString()}'),
            backgroundColor: Colors.red,
          ),
        );
      }
    }
  }

  Future<void> _loadParts() async {
    setState(() => _isLoadingParts = true);
    try {
      final parts = await _api.getPartsByOrderId(widget.orderId);
      setState(() {
        _parts = parts;
        _isLoadingParts = false;
      });
    } catch (e) {
      setState(() => _isLoadingParts = false);
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(
            content: Text('Không thể tải danh sách linh kiện: ${e.toString()}'),
            backgroundColor: Colors.orange,
          ),
        );
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: const Text('Chi tiết đơn hàng'),
      ),
      body: _isLoading
          ? const Center(child: CircularProgressIndicator())
          : RefreshIndicator(
              onRefresh: () async {
                await _loadDetails();
                await _loadParts();
              },
              child: SingleChildScrollView(
                padding: const EdgeInsets.all(16),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    // Order Information Card
                    if (_orderDetails != null) ...[
                      Card(
                        child: Padding(
                          padding: const EdgeInsets.all(16),
                          child: Column(
                            crossAxisAlignment: CrossAxisAlignment.start,
                            children: [
                              const Text(
                                'Thông tin đơn hàng',
                                style: TextStyle(
                                  fontSize: 18,
                                  fontWeight: FontWeight.bold,
                                ),
                              ),
                              const SizedBox(height: 12),
                              _buildInfoRow('Mã đơn hàng', _orderDetails!['orderId']?.toString() ?? ''),
                              _buildInfoRow('Số điện thoại KH', _orderDetails!['customerPhone']?.toString() ?? ''),
                              _buildInfoRow('Nhân viên tiếp nhận', _orderDetails!['receiverEmpName']?.toString() ?? ''),
                              _buildInfoRow('Nhân viên xử lý', _orderDetails!['handlerEmpName']?.toString() ?? ''),
                              _buildInfoRow('Loại đơn', _orderDetails!['orderType']?.toString() ?? ''),
                              _buildInfoRow('Ngày tiếp nhận', _orderDetails!['receivedDate'] != null
                                  ? DateTime.parse(_orderDetails!['receivedDate']).toLocal().toString().substring(0, 16)
                                  : ''),
                              _buildInfoRow('Trạng thái', _orderDetails!['status']?.toString() ?? ''),
                              if (_orderDetails!['description'] != null && _orderDetails!['description'].toString().isNotEmpty)
                                _buildInfoRow('Mô tả', _orderDetails!['description']?.toString() ?? ''),
                            ],
                          ),
                        ),
                      ),
                      const SizedBox(height: 16),
                    ],

                    // Parts List Card
                    Card(
                      child: Padding(
                        padding: const EdgeInsets.all(16),
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: [
                            const Text(
                              'Danh sách linh kiện được gán',
                              style: TextStyle(
                                fontSize: 18,
                                fontWeight: FontWeight.bold,
                              ),
                            ),
                            const SizedBox(height: 12),
                            _isLoadingParts
                                ? const Center(child: CircularProgressIndicator())
                                : _parts.isEmpty
                                    ? const Padding(
                                        padding: EdgeInsets.all(16),
                                        child: Text(
                                          'Đơn hàng này chưa có linh kiện nào được gán.',
                                          style: TextStyle(color: Colors.grey),
                                        ),
                                      )
                                    : ListView.separated(
                                        shrinkWrap: true,
                                        physics: const NeverScrollableScrollPhysics(),
                                        itemCount: _parts.length,
                                        separatorBuilder: (_, __) => const Divider(),
                                        itemBuilder: (context, index) {
                                          final part = _parts[index];
                                          return ListTile(
                                            leading: const Icon(Icons.memory, color: Colors.blue),
                                            title: Text(
                                              part.name,
                                              style: const TextStyle(fontWeight: FontWeight.bold),
                                            ),
                                            subtitle: Column(
                                              crossAxisAlignment: CrossAxisAlignment.start,
                                              children: [
                                                if (part.manufacturer != null && part.manufacturer!.isNotEmpty)
                                                  Text('NSX: ${part.manufacturer}'),
                                                Text('Serial: ${part.serial}'),
                                                Text('Trạng thái: ${part.status}'),
                                                if (part.price != null)
                                                  Text('Giá: ${part.price!.toStringAsFixed(0)} đ'),
                                              ],
                                            ),
                                          );
                                        },
                                      ),
                          ],
                        ),
                      ),
                    ),
                  ],
                ),
              ),
            ),
    );
  }

  Widget _buildInfoRow(String label, String value) {
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 4),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          SizedBox(
            width: 120,
            child: Text(
              label,
              style: const TextStyle(
                fontWeight: FontWeight.w500,
                color: Colors.grey,
              ),
            ),
          ),
          Expanded(
            child: Text(
              value,
              style: const TextStyle(fontWeight: FontWeight.w400),
            ),
          ),
        ],
      ),
    );
  }
}

