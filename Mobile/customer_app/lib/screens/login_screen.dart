import 'package:flutter/material.dart';

import '../services/api_service.dart';
import '../services/signalr_service.dart';
import 'web_qr_login_sheet.dart';
import 'qr_scan_screen.dart';

enum LoginType { customer, employee }

class LoginScreen extends StatefulWidget {
  const LoginScreen({super.key});

  @override
  State<LoginScreen> createState() => _LoginScreenState();
}

class _LoginScreenState extends State<LoginScreen> {
  final _phoneCtrl = TextEditingController();
  final _passwordCtrl = TextEditingController();
  bool _isLoading = false;
  LoginType _loginType = LoginType.customer;

  final _api = ApiService();
  final _signalR = SignalRService();

  @override
  void dispose() {
    _phoneCtrl.dispose();
    _passwordCtrl.dispose();
    super.dispose();
  }

  Future<void> _handleLogin() async {
    final phone = _phoneCtrl.text.trim();
    final password = _passwordCtrl.text;

    if (phone.isEmpty || password.isEmpty) {
      _showSnack('Vui lòng nhập Số điện thoại và Mật khẩu');
      return;
    }

    setState(() => _isLoading = true);

    try {
      Map<String, dynamic> res;

      if (_loginType == LoginType.customer) {
        res = await _api.login(phone, password);

        final roles = (res['roles'] ?? '').toString().toLowerCase();
        if (!roles.contains('role_khachhang')) {
          _showSnack('Chỉ dành cho khách hàng');
          return;
        }
      } else {
        res = await _api.loginEmployee(phone, password);

        final roles = (res['roles'] ?? '').toString().toLowerCase();
        if (!roles.contains('role_nhanvien') &&
            !roles.contains('role_admin')) {
          _showSnack('Chỉ dành cho nhân viên');
          return;
        }
      }

      // --- SignalR connect ---
      final sessionId = res['sessionId'] ?? '';
      if (sessionId is String && sessionId.isNotEmpty) {
        await _signalR.connect(sessionId);
      }

      if (!mounted) return;
      Navigator.of(context).pushReplacementNamed('/home');
    } catch (e) {
      _showSnack(e.toString());
    } finally {
      if (mounted) setState(() => _isLoading = false);
    }
  }

  Future<void> _handleWebQrLogin(String code, {VoidCallback? onSuccess}) async {
    final platform = 'MOBILE';

    try {
      final res = await _api.loginViaWebQr(code, platform: platform);
      final sessionId = res['sessionId'] ?? '';

      if (sessionId is String && sessionId.isNotEmpty) {
        await _signalR.connect(sessionId);
      }

      if (!mounted) return;

      // Đảm bảo token đã được lưu vào storage trước khi navigate
      // Sử dụng một chút delay để đảm bảo mọi thứ đã hoàn tất
      await Future.delayed(const Duration(milliseconds: 100));

      // Gọi callback để đóng bottom sheet trước khi navigate
      onSuccess?.call();

      // Đợi một chút để bottom sheet đóng hoàn toàn
      await Future.delayed(const Duration(milliseconds: 200));

      if (!mounted) return;

      // Sử dụng pushNamedAndRemoveUntil để clear navigation stack và navigate đến home
      // Điều này đảm bảo user không thể quay lại màn hình login
      Navigator.of(context).pushNamedAndRemoveUntil('/home', (route) => false);
    } catch (e) {
      // Re-throw để web_qr_login_sheet có thể hiển thị lỗi
      // Không hiển thị lỗi ở đây để tránh duplicate
      rethrow;
    }
  }

  void _openWebQrLoginSheet() {
    showModalBottomSheet(
      context: context,
      isScrollControlled: true,
      isDismissible: true,
      builder: (sheetContext) => WebQrLoginSheet(
        onSubmit: (code) async {
          // Xử lý login
          await _handleWebQrLogin(code, onSuccess: () {
            // Đóng bottom sheet sau khi login thành công, trước khi navigate
            if (sheetContext.mounted) {
              Navigator.of(sheetContext).pop();
            }
          });
        },
      ),
    );
  }

  Future<void> _openQrScanner() async {
    if (_isLoading) return;
    await Navigator.of(context).push(
      MaterialPageRoute(
        builder: (_) => QrScanScreen(
          submitButtonLabel: 'Đăng nhập bằng mã QR',
          successMessage: 'Đăng nhập thành công. Đang chuyển hướng...',
          onSubmit: (code) => _handleWebQrLogin(code),
        ),
      ),
    );
  }

  void _showSnack(String msg) {
    ScaffoldMessenger.of(context).showSnackBar(
      SnackBar(content: Text(msg)),
    );
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      body: Center(
        child: SingleChildScrollView(
          padding: const EdgeInsets.all(24),
          child: Column(
            mainAxisAlignment: MainAxisAlignment.center,
            children: [
              // --- Login type switch ---
              Container(
                decoration: BoxDecoration(
                  border: Border.all(color: Colors.grey.shade300),
                  borderRadius: BorderRadius.circular(8),
                ),
                child: Row(
                  children: [
                    Expanded(
                      child: GestureDetector(
                        onTap: _isLoading
                            ? null
                            : () => setState(
                                () => _loginType = LoginType.customer),
                        child: Container(
                          padding: const EdgeInsets.symmetric(vertical: 12),
                          decoration: BoxDecoration(
                            color: _loginType == LoginType.customer
                                ? Theme.of(context).primaryColor
                                : Colors.transparent,
                            borderRadius: const BorderRadius.only(
                              topLeft: Radius.circular(8),
                              bottomLeft: Radius.circular(8),
                            ),
                          ),
                          child: Text(
                            'Khách hàng',
                            textAlign: TextAlign.center,
                            style: TextStyle(
                              color: _loginType == LoginType.customer
                                  ? Colors.white
                                  : Colors.black,
                              fontWeight: _loginType == LoginType.customer
                                  ? FontWeight.bold
                                  : FontWeight.normal,
                            ),
                          ),
                        ),
                      ),
                    ),
                    Expanded(
                      child: GestureDetector(
                        onTap: _isLoading
                            ? null
                            : () => setState(
                                () => _loginType = LoginType.employee),
                        child: Container(
                          padding: const EdgeInsets.symmetric(vertical: 12),
                          decoration: BoxDecoration(
                            color: _loginType == LoginType.employee
                                ? Theme.of(context).primaryColor
                                : Colors.transparent,
                            borderRadius: const BorderRadius.only(
                              topRight: Radius.circular(8),
                              bottomRight: Radius.circular(8),
                            ),
                          ),
                          child: Text(
                            'Nhân viên',
                            textAlign: TextAlign.center,
                            style: TextStyle(
                              color: _loginType == LoginType.employee
                                  ? Colors.white
                                  : Colors.black,
                              fontWeight: _loginType == LoginType.employee
                                  ? FontWeight.bold
                                  : FontWeight.normal,
                            ),
                          ),
                        ),
                      ),
                    ),
                  ],
                ),
              ),

              const SizedBox(height: 24),

              // --- Input fields ---
              TextField(
                controller: _phoneCtrl,
                decoration: const InputDecoration(
                  labelText: 'Số điện thoại',
                ),
                keyboardType: TextInputType.phone,
              ),
              const SizedBox(height: 16),
              TextField(
                controller: _passwordCtrl,
                decoration: const InputDecoration(
                  labelText: 'Password',
                ),
                obscureText: true,
              ),

              const SizedBox(height: 24),

              // --- Main login button ---
              SizedBox(
                width: double.infinity,
                child: ElevatedButton(
                  onPressed: _isLoading ? null : _handleLogin,
                  child: _isLoading
                      ? const SizedBox(
                          height: 18,
                          width: 18,
                          child: CircularProgressIndicator(strokeWidth: 2),
                        )
                      : Text(
                          _loginType == LoginType.customer
                              ? 'Đăng nhập'
                              : 'Đăng nhập nhân viên',
                        ),
                ),
              ),

              // --- Customer extra buttons ---
              if (_loginType == LoginType.customer) ...[
                const SizedBox(height: 8),
                SizedBox(
                  width: double.infinity,
                  child: TextButton(
                    onPressed: _isLoading
                        ? null
                        : () => Navigator.pushNamed(context, '/register'),
                    child: const Text('Đăng ký'),
                  ),
                ),
              ],
              const SizedBox(height: 8),
              SizedBox(
                width: double.infinity,
                child: OutlinedButton.icon(
                  onPressed: _isLoading ? null : _openQrScanner,
                  icon: const Icon(Icons.qr_code_scanner),
                  label: const Text('Quét mã QR bằng camera'),
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}
