import 'package:flutter/material.dart';
import '../services/api_service.dart';
import '../widgets/status_chip.dart';
import '../widgets/error_banner.dart';

class OrderDetailScreen extends StatefulWidget {
  final int orderId;
  const OrderDetailScreen({super.key, required this.orderId});

  @override
  State<OrderDetailScreen> createState() => _OrderDetailScreenState();
}

class _OrderDetailScreenState extends State<OrderDetailScreen> {
  final ApiService _api = ApiService();
  Map<String, dynamic>? _detail;
  List<dynamic> _parts = [];
  bool _loading = true;
  String? _error;

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    setState(() { _loading = true; _error = null; });
    try {
      final detail = await _api.getOrderDetails(widget.orderId);
      List<dynamic> parts = [];
      try {
        parts = await _api.getPartsByOrderId(widget.orderId);
      } catch (_) {}
      setState(() { _detail = detail; _parts = parts; _loading = false; });
    } catch (e) {
      setState(() { _error = e.toString(); _loading = false; });
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: Text('Đơn hàng #${widget.orderId}')),
      body: _loading
          ? const Center(child: CircularProgressIndicator())
          : _error != null
              ? Padding(
                  padding: const EdgeInsets.all(16.0),
                  child: ErrorBanner(message: _error!),
                )
              : RefreshIndicator(
                  onRefresh: _load,
                  child: SingleChildScrollView(
                    physics: const AlwaysScrollableScrollPhysics(),
                    padding: const EdgeInsets.all(16),
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        _buildSummaryCard(),
                        const SizedBox(height: 16),
                        _buildPartsCard(),
                      ],
                    ),
                  ),
                ),
    );
  }

  Widget _buildSummaryCard() {
    final d = _detail ?? {};
    final status = d['Status']?.toString() ?? d['status']?.toString() ?? '';
    return Card(
      elevation: 4,
      shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(12)),
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              mainAxisAlignment: MainAxisAlignment.spaceBetween,
              children: [
                const Text('Thông tin đơn hàng',
                    style: TextStyle(fontSize: 18, fontWeight: FontWeight.bold)),
                if (status.isNotEmpty) StatusChip(status: status),
              ],
            ),
            const SizedBox(height: 12),
            _infoRow('Ngày nhận', d['ReceivedDate'] ?? d['receivedDate'] ?? ''),
            _infoRow('SĐT khách', d['CustomerPhone'] ?? d['customerPhone'] ?? ''),
            _infoRow('Người nhận', d['ReceiverEmpName'] ?? d['receiverEmpName'] ?? ''),
            _infoRow('Người xử lý', d['HandlerEmpName'] ?? d['handlerEmpName'] ?? ''),
            _infoRow('Loại', d['OrderType'] ?? d['orderType'] ?? ''),
          ],
        ),
      ),
    );
  }

  Widget _buildPartsCard() {
    return Card(
      elevation: 4,
      shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(12)),
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            const Text('Vật tư/Phụ tùng',
                style: TextStyle(fontSize: 18, fontWeight: FontWeight.bold)),
            const SizedBox(height: 12),
            if (_parts.isEmpty)
              const Text('Không có dữ liệu vật tư', style: TextStyle(color: Colors.grey))
            else
              ListView.separated(
                shrinkWrap: true,
                physics: const NeverScrollableScrollPhysics(),
                itemCount: _parts.length,
                separatorBuilder: (_, __) => const Divider(height: 16),
                itemBuilder: (_, i) {
                  final p = _parts[i] as Map<String, dynamic>;
                  return Row(
                    mainAxisAlignment: MainAxisAlignment.spaceBetween,
                    children: [
                      Expanded(child: Text(p['PartName'] ?? p['partName'] ?? '')),
                      Text('SL: ${p['Quantity'] ?? p['quantity'] ?? ''}'),
                    ],
                  );
                },
              ),
          ],
        ),
      ),
    );
  }

  Widget _infoRow(String label, dynamic value) {
    final v = (value ?? '').toString();
    if (v.isEmpty) return const SizedBox.shrink();
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 6),
      child: Row(
        children: [
          SizedBox(width: 120, child: Text(label, style: const TextStyle(color: Colors.grey))),
          Expanded(child: Text(v)),
        ],
      ),
    );
  }
}
