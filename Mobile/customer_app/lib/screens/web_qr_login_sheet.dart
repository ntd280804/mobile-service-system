import 'package:flutter/material.dart';

typedef WebQrLoginSubmit = Future<void> Function(String code);

class WebQrLoginSheet extends StatefulWidget {
  final WebQrLoginSubmit onSubmit;

  const WebQrLoginSheet({super.key, required this.onSubmit});

  @override
  State<WebQrLoginSheet> createState() => _WebQrLoginSheetState();
}

class _WebQrLoginSheetState extends State<WebQrLoginSheet> {
  final _codeController = TextEditingController();
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
      await widget.onSubmit(code);
      if (!mounted) return;
      Navigator.of(context).pop();
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
                'Đăng nhập bằng mã Web',
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
            'Nhập mã đang hiển thị trên Web để đăng nhập vào ứng dụng di động.',
            style: TextStyle(color: Colors.black54),
          ),
          const SizedBox(height: 16),
          TextField(
            controller: _codeController,
            textCapitalization: TextCapitalization.characters,
            decoration: const InputDecoration(
              labelText: 'Mã QR/Code',
              hintText: 'Ví dụ: AB12CD34',
              border: OutlineInputBorder(),
            ),
            maxLength: 12,
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
                  : const Text('Đăng nhập'),
            ),
          ),
        ],
      ),
    );
  }
}

