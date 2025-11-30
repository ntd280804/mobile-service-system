import 'dart:async';
import 'package:flutter/material.dart';
import 'package:mobile_scanner/mobile_scanner.dart';

import '../services/api_service.dart';

class QrScanScreen extends StatefulWidget {
  final Future<void> Function(String code)? onSubmit;
  final String submitButtonLabel;
  final String successMessage;

  const QrScanScreen({
    super.key,
    this.onSubmit,
    this.submitButtonLabel = 'Xác nhận đăng nhập',
    this.successMessage = 'Đăng nhập Web đã được xác nhận.',
  });

  @override
  State<QrScanScreen> createState() => _QrScanScreenState();
}

class _QrScanScreenState extends State<QrScanScreen> {
  final _api = ApiService();
  final _codeController = TextEditingController();
  final MobileScannerController _scannerController = MobileScannerController(
    detectionSpeed: DetectionSpeed.noDuplicates,
    formats: const [BarcodeFormat.qrCode],
    facing: CameraFacing.back,
  );

  bool _scannerLocked = false;
  bool _isSubmitting = false;
  String? _cameraError;
  String? _statusMessage;

  @override
  void initState() {
    super.initState();
    _statusMessage = 'Đưa mã QR vào khung để quét.';
  }

  void _handleDetection(BarcodeCapture capture) {
    if (_scannerLocked || _isSubmitting) return;

    final code = capture.barcodes
        .map((b) => b.rawValue)
        .whereType<String>()
        .map((value) => value.trim())
        .firstWhere(
          (value) => value.isNotEmpty,
      orElse: () => '',
    );

    if (code.isEmpty) return;

    setState(() {
      _scannerLocked = true;
      _codeController.text = code;
      _statusMessage = 'Đã đọc mã QR thành công. Đang xác nhận...';
    });

    unawaited(_scannerController.stop());
    // Tự động xác nhận sau khi quét QR thành công
    unawaited(_handleSubmit());
  }

  Future<void> _resumeScanning() async {
    setState(() {
      _scannerLocked = false;
      _statusMessage = 'Đưa mã QR vào khung để quét.';
      _cameraError = null;
    });
    try {
      await _scannerController.start();
    } on MobileScannerException catch (e) {
      setState(() {
        _cameraError = _formatScannerError(
          e,
          fallback: 'Không thể khởi động lại camera.',
        );
        _scannerLocked = true;
      });
    }
  }

  Future<void> _toggleTorch() async {
    try {
      await _scannerController.toggleTorch();
    } on MobileScannerException catch (e) {
      setState(() {
        _statusMessage = _formatScannerError(
          e,
          fallback: 'Không thể điều khiển đèn flash.',
        );
      });
    }
  }

  Future<void> _handleSubmit() async {
    final code = _codeController.text.trim().toUpperCase();
    if (code.length < 4) {
      setState(() {
        _statusMessage = 'Vui lòng nhập mã hợp lệ (tối thiểu 4 ký tự).';
      });
      return;
    }

    setState(() {
      _isSubmitting = true;
      _statusMessage = widget.onSubmit == null
          ? 'Đang xác nhận với server...'
          : 'Đang xử lý mã QR...';
    });

    try {
      if (widget.onSubmit != null) {
        await widget.onSubmit!(code);
      } else {
        await _api.confirmQrLogin(code);
      }
      if (!mounted) return;
      setState(() {
        _statusMessage = widget.successMessage;
      });
      final navigator = Navigator.of(context);
      if (navigator.canPop()) {
        navigator.pop(code);
      }
    } catch (e) {
      if (!mounted) return;
      setState(() {
        _statusMessage = 'Không thể xác nhận: $e';
      });
    } finally {
      if (mounted) {
        setState(() => _isSubmitting = false);
      }
    }
  }

  @override
  void dispose() {
    _codeController.dispose();
    _scannerController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: const Text('Quét QR đăng nhập Web'),
        actions: [
          ValueListenableBuilder<TorchState>(
            valueListenable: _scannerController.torchState,
            builder: (context, state, _) {
              final isOn = state == TorchState.on;
              return IconButton(
                onPressed: _toggleTorch,
                icon: Icon(isOn ? Icons.flash_on : Icons.flash_off),
                tooltip: isOn ? 'Tắt đèn flash' : 'Bật đèn flash',
              );
            },
          ),
          IconButton(
            onPressed: _scannerLocked ? _resumeScanning : null,
            icon: const Icon(Icons.refresh),
            tooltip: 'Quét lại',
          ),
        ],
      ),
      body: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          children: [
            Expanded(
              child: _cameraError != null
                  ? _ErrorLabel(message: _cameraError!)
                  : _buildCameraPreview(),
            ),
            const SizedBox(height: 16),
            TextField(
              controller: _codeController,
              textCapitalization: TextCapitalization.characters,
              decoration: const InputDecoration(
                labelText: 'Mã QR / Code',
                border: OutlineInputBorder(),
              ),
            ),
            const SizedBox(height: 12),
            if (_statusMessage != null)
              Text(
                _statusMessage!,
                style: const TextStyle(color: Colors.black54),
              ),
            const SizedBox(height: 12),
            SizedBox(
              width: double.infinity,
              child: ElevatedButton.icon(
                onPressed: _scannerLocked ? _resumeScanning : null,
                icon: _scannerLocked
                    ? const Icon(Icons.refresh)
                    : const Icon(Icons.qr_code_scanner),
                label: Text(_scannerLocked ? 'Quét lại' : 'Đang quét...'),
              ),
            ),
            const SizedBox(height: 8),
            SizedBox(
              width: double.infinity,
              child: FilledButton(
                onPressed: _isSubmitting ? null : _handleSubmit,
                child: _isSubmitting
                    ? const SizedBox(
                  height: 18,
                  width: 18,
                  child: CircularProgressIndicator(
                    strokeWidth: 2,
                    color: Colors.white,
                  ),
                )
                    : Text(widget.submitButtonLabel),
              ),
            ),
          ],
        ),
      ),
    );
  }

  Widget _buildCameraPreview() {
    return ClipRRect(
      borderRadius: BorderRadius.circular(12),
      child: Stack(
        fit: StackFit.expand,
        children: [
          MobileScanner(
            controller: _scannerController,
            fit: BoxFit.cover,
            onDetect: _handleDetection,
            errorBuilder: (context, error, child) {
              return _ErrorLabel(
                message: _formatScannerError(
                  error,
                  fallback: 'Không thể hiển thị camera.',
                ),
              );
            },
          ),
          Align(
            alignment: Alignment.center,
            child: Container(
              margin: const EdgeInsets.symmetric(horizontal: 24),
              height: 220,
              decoration: BoxDecoration(
                borderRadius: BorderRadius.circular(16),
                border: Border.all(
                  color: Colors.white.withOpacity(0.85),
                  width: 2,
                ),
              ),
            ),
          ),
          Positioned(
            bottom: 24,
            left: 16,
            right: 16,
            child: Text(
              'Đưa mã QR vào khung để hệ thống tự quét.',
              textAlign: TextAlign.center,
              style: TextStyle(
                color: Colors.white.withOpacity(0.9),
                fontWeight: FontWeight.w600,
              ),
            ),
          ),
          if (_scannerLocked)
            Container(
              color: Colors.black.withOpacity(0.45),
              alignment: Alignment.center,
              child: Column(
                mainAxisSize: MainAxisSize.min,
                children: const [
                  Icon(Icons.check_circle, color: Colors.white, size: 64),
                  SizedBox(height: 8),
                  Text(
                    'Đã đọc mã QR',
                    style: TextStyle(
                      color: Colors.white,
                      fontSize: 18,
                      fontWeight: FontWeight.w600,
                    ),
                  ),
                ],
              ),
            ),
        ],
      ),
    );
  }

  String _formatScannerError(
      MobileScannerException error, {
        required String fallback,
      }) {
    final details = error.errorDetails;
    if (details != null) {
      final msg = details.toString();
      if (msg.isNotEmpty) return msg;
    }
    final codeName = error.errorCode.name;
    if (codeName.isNotEmpty) {
      return 'Lỗi camera: $codeName';
    }
    return fallback;
  }
}

class _ErrorLabel extends StatelessWidget {
  final String message;

  const _ErrorLabel({required this.message});

  @override
  Widget build(BuildContext context) {
    return Container(
      width: double.infinity,
      padding: const EdgeInsets.all(16),
      decoration: BoxDecoration(
        borderRadius: BorderRadius.circular(12),
        color: Colors.red.shade50,
      ),
      child: Text(
        message,
        style: TextStyle(color: Colors.red.shade700),
      ),
    );
  }
}
