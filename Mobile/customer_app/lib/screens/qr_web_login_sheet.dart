import 'package:flutter/material.dart';

import '../services/api_service.dart';

class QrWebLoginSheet extends StatefulWidget {
  const QrWebLoginSheet({super.key});

  @override
  State<QrWebLoginSheet> createState() => _QrWebLoginSheetState();
}

class _QrWebLoginSheetState extends State<QrWebLoginSheet> {
  final _codeController = TextEditingController();
  final _api = ApiService();
  bool _isSubmitting = false;

  @override
  void dispose() {
    _codeController.dispose();
    super.dispose();
  }

  Future<void> _handleSubmit() async {
    final code = _codeController.text.trim().toUpperCase();
    if (code.length < 4) {
      _showSnack('Vui lòng nhập mã hợp lệ (tối thiểu 4 ký tự).');
      return;
    }

    setState(() => _isSubmitting = true);
    try {
      await _api.confirmQrLogin(code);
      if (!mounted) return;
      Navigator.of(context).pop();
      _showSnack('Đã xác nhận đăng nhập Web. Vui lòng chuyển sang trình duyệt.');
    } catch (e) {
      _showSnack(e.toString());
    } finally {
      if (mounted) setState(() => _isSubmitting = false);
    }
  }

  void _showSnack(String message) {
    ScaffoldMessenger.of(context).showSnackBar(
      SnackBar(content: Text(message)),
    );
  }

  @override
  Widget build(BuildContext context) {
    final viewInsets = MediaQuery.of(context).viewInsets.bottom;
    return Padding(
      padding: EdgeInsets.only(
        left: 24,
        right: 24,
        top: 24,
        bottom: viewInsets + 24,
      ),
      child: Column(
        mainAxisSize: MainAxisSize.min,
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            mainAxisAlignment: MainAxisAlignment.spaceBetween,
            children: [
              const Text(
                'Đăng nhập Web',
                style: TextStyle(
                  fontSize: 20,
                  fontWeight: FontWeight.bold,
                ),
              ),
              IconButton(
                icon: const Icon(Icons.close),
                onPressed: () => Navigator.of(context).pop(),
              ),
            ],
          ),
          const SizedBox(height: 8),
          const Text(
            'Quét QR hiển thị trên trang Web hoặc nhập mã thủ công phía dưới.',
            style: TextStyle(color: Colors.black54),
          ),
          const SizedBox(height: 16),
          TextField(
            controller: _codeController,
            textCapitalization: TextCapitalization.characters,
            decoration: const InputDecoration(
              labelText: 'Mã QR/Code',
              hintText: 'Nhập mã gồm chữ và số',
              border: OutlineInputBorder(),
            ),
            maxLength: 16,
          ),
          const SizedBox(height: 12),
          SizedBox(
            width: double.infinity,
            child: ElevatedButton(
              onPressed: _isSubmitting ? null : _handleSubmit,
              child: _isSubmitting
                  ? const SizedBox(
                      height: 18,
                      width: 18,
                      child: CircularProgressIndicator(strokeWidth: 2),
                    )
                  : const Text('Xác nhận'),
            ),
          ),
        ],
      ),
    );
  }
}

Future<void> showQrLoginSheet(BuildContext context) {
  return showModalBottomSheet(
    context: context,
    isScrollControlled: true,
    builder: (_) => const QrWebLoginSheet(),
  );
}



