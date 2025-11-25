import 'dart:async';
import 'dart:typed_data';
import 'package:camera/camera.dart';
import 'package:flutter/foundation.dart';
import 'package:flutter/material.dart';
import 'package:image/image.dart' as img;
import 'package:zxing2/qrcode.dart';

import 'package:zxing2/zxing2.dart' as zxing;

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
  CameraController? _controller;
  Future<void>? _initializingCamera;
  bool _isDecoding = false;
  bool _isSubmitting = false;
  String? _cameraError;
  String? _statusMessage;

  @override
  void initState() {
    super.initState();
    _initCamera();
  }

  Future<void> _initCamera() async {
    try {
      final cameras = await availableCameras();
      if (cameras.isEmpty) {
        setState(() {
          _cameraError = 'Không tìm thấy camera khả dụng.';
        });
        return;
      }

      final backCameras = cameras.where(
            (c) => c.lensDirection == CameraLensDirection.back,
      );
      final camera = backCameras.isNotEmpty ? backCameras.first : cameras.first;
      final controller = CameraController(
        camera,
        ResolutionPreset.medium,
        enableAudio: false,
      );

      setState(() {
        _controller = controller;
        _initializingCamera = controller.initialize();
      });

      await _initializingCamera;
    } catch (e) {
      setState(() {
        _cameraError =
        'Không thể khởi tạo camera. Kiểm tra quyền truy cập và thử lại.\n$e';
      });
    }
  }

  Future<void> _captureAndDecode() async {
    final controller = _controller;
    if (controller == null || !controller.value.isInitialized) {
      setState(() => _statusMessage = 'Camera chưa sẵn sàng.');
      return;
    }

    setState(() {
      _isDecoding = true;
      _statusMessage = 'Đang chụp và giải mã...';
    });

    try {
      final file = await controller.takePicture();
      final bytes = await file.readAsBytes();
      final result = await compute(_decodeQrSync, bytes);

      if (!mounted) return;

      if (result == null) {
        setState(() {
          _statusMessage =
          'Không tìm thấy mã QR. Hãy đưa máy gần hơn và thử lại.';
        });
      } else {
        setState(() {
          _codeController.text = result;
          _statusMessage = 'Đã đọc mã QR thành công.';
        });
      }
    } on zxing.ChecksumException catch (_) {
      if (!mounted) return;
      setState(() {
        _statusMessage =
        'Mã QR bị lỗi checksum, hãy chụp lại rõ hơn hoặc thử mã khác.';
      });
    } on zxing.FormatReaderException catch (_) {
      if (!mounted) return;
      setState(() {
        _statusMessage =
        'Không thể giải mã vì định dạng QR không hợp lệ. Hãy thử lại.';
      });
    } catch (e) {
      if (!mounted) return;
      setState(() {
        _statusMessage = 'Lỗi khi giải mã: $e';
      });
    } finally {
      if (mounted) {
        setState(() => _isDecoding = false);
      }
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
    _controller?.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: const Text('Quét QR đăng nhập Web'),
        actions: [
          IconButton(
            onPressed: _captureAndDecode,
            icon: const Icon(Icons.camera),
            tooltip: 'Chụp & giải mã thủ công',
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
                onPressed: _isDecoding ? null : _captureAndDecode,
                icon: _isDecoding
                    ? const SizedBox(
                  width: 16,
                  height: 16,
                  child: CircularProgressIndicator(strokeWidth: 2),
                )
                    : const Icon(Icons.qr_code_scanner),
                label: Text(_isDecoding ? 'Đang xử lý...' : 'Chụp & giải mã'),
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
    final controller = _controller;
    final initFuture = _initializingCamera;
    if (controller == null || initFuture == null) {
      return const Center(child: CircularProgressIndicator());
    }

    return FutureBuilder<void>(
      future: initFuture,
      builder: (context, snapshot) {
        if (snapshot.connectionState == ConnectionState.waiting) {
          return const Center(child: CircularProgressIndicator());
        }
        if (snapshot.hasError) {
          return _ErrorLabel(
            message: 'Không thể hiển thị camera: ${snapshot.error}',
          );
        }
        return ClipRRect(
          borderRadius: BorderRadius.circular(12),
          child: AspectRatio(
            aspectRatio: controller.value.aspectRatio,
            child: CameraPreview(controller),
          ),
        );
      },
    );
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

/// ===================
/// QR decode helper
/// ===================

String? _decodeQrSync(Uint8List bytes) {
  final img.Image? raw = img.decodeImage(bytes);
  if (raw == null) return null;

  final variants = _generateImageVariants(raw);
  final hints = zxing.DecodeHints()..put(zxing.DecodeHintType.tryHarder);
  final reader = QRCodeReader();

  zxing.ChecksumException? checksumError;
  zxing.FormatReaderException? formatError;

  for (final variant in variants) {
    final pixels = _imageToPixels(variant);
    final source = zxing.RGBLuminanceSource(
      variant.width,
      variant.height,
      pixels,
    );
    final binarizer = zxing.HybridBinarizer(source);

    try {
      final bitmap = zxing.BinaryBitmap(binarizer);
      final result = reader.decode(bitmap, hints: hints);
      return result.text;
    } on zxing.NotFoundException {
      continue;
    } on zxing.ChecksumException catch (e) {
      checksumError = e;
    } on zxing.FormatReaderException catch (e) {
      formatError = e;
    }
  }

  if (checksumError != null) throw checksumError;
  if (formatError != null) throw formatError;
  return null;
}

List<img.Image> _generateImageVariants(img.Image input) {
  final baked = img.bakeOrientation(input);
  final base = _resizeIfNeeded(baked);
  final variants = <img.Image>[base];

  variants.add(img.grayscale(base.clone()));
  variants.add(img.adjustColor(base.clone(), contrast: 1.2));
  variants.add(img.adjustColor(img.grayscale(base.clone()),
      contrast: 1.4, brightness: 0.05));
  variants.add(img.copyRotate(base.clone(), angle: 90));
  variants.add(img.copyRotate(base.clone(), angle: -90));
  variants.add(img.flipHorizontal(base.clone()));

  return variants;
}

img.Image _resizeIfNeeded(img.Image image, {int maxDimension = 1024}) {
  final largestSide = image.width > image.height ? image.width : image.height;
  if (largestSide <= maxDimension) return image;
  final scale = maxDimension / largestSide;
  final width = (image.width * scale).round().clamp(1, maxDimension);
  final height = (image.height * scale).round().clamp(1, maxDimension);
  return img.copyResize(
    image,
    width: width,
    height: height,
    interpolation: img.Interpolation.cubic,
  );
}
Int32List _imageToPixels(img.Image image) {
  final bytes = image.getBytes(order: img.ChannelOrder.rgba);
  final pixels = Int32List(image.width * image.height);
  var j = 0;
  for (var i = 0; i < pixels.length; i++) {
    final r = bytes[j++];
    final g = bytes[j++];
    final b = bytes[j++];
    final a = bytes[j++];
    // Chú ý zxing2 cần ARGB
    pixels[i] = (a << 24) | (r << 16) | (g << 8) | b;
  }
  return pixels;
}



