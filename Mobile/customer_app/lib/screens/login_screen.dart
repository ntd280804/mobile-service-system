import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../theme/app_theme.dart';
import '../widgets/gradient_button.dart';
import '../widgets/gradient_form_field.dart';
import '../widgets/error_banner.dart';
import '../providers/auth_provider.dart';
import '../services/signalr_service.dart';
import '../services/api_service.dart';
import '../services/storage_service.dart';
import 'qr_scan_screen.dart';
enum LoginType { customer, employee }

class LoginScreen extends StatefulWidget {
  const LoginScreen({super.key});

  @override
  State<LoginScreen> createState() => _LoginScreenState();
}

class _LoginScreenState extends State<LoginScreen> {
  final _formKey = GlobalKey<FormState>();
  bool _isLoading = false;
  final _usernameCtrl = TextEditingController();
  final _passwordCtrl = TextEditingController();
  LoginType _loginType = LoginType.customer;
  String? _errorMessage;

  final _signalR = SignalRService();

  @override
  void dispose() {
    _usernameCtrl.dispose();
    _passwordCtrl.dispose();
    super.dispose();
  }
  Future<void> _openQrScanner() async {
    if (_isLoading) return;
    
    final code = await Navigator.of(context).push<String>(
      MaterialPageRoute(
        builder: (_) => QrScanScreen(
          submitButtonLabel: 'Đăng nhập bằng mã QR',
          successMessage: 'Đăng nhập thành công. Đang chuyển hướng...',
          onSubmit: (code) async {
            // Xử lý đăng nhập - sẽ tự động navigate đến home screen
            await _handleWebQrLogin(code);
          },
        ),
      ),
    );
    
    // Nếu có code trả về nhưng chưa login (trường hợp không dùng onSubmit)
    if (mounted && code != null) {
      await _handleWebQrLogin(code);
    }
  }

  void _showSnack(String msg) {
    ScaffoldMessenger.of(context).showSnackBar(
      SnackBar(content: Text(msg)),
    );
  }
  Future<void> _handleLogin() async {
    if (!_formKey.currentState!.validate()) {
      return;
    }

    setState(() => _errorMessage = null);

    final authProvider = context.read<AuthProvider>();
    final nav = Navigator.of(context);
    final username = _usernameCtrl.text.trim();
    final password = _passwordCtrl.text;

    Map<String, dynamic> result;

    if (_loginType == LoginType.customer) {
      result = await authProvider.loginCustomer(
        phone: username,
        password: password,
      );
    } else {
      result = await authProvider.loginEmployee(
        username: username,
        password: password,
      );
    }

    if (result['success'] == true) {
      // Lấy sessionId từ storage sau khi login thành công
      final storage = StorageService();
      final sessionId = await storage.getSessionId();
      if (sessionId != null && sessionId.isNotEmpty) {
        try {
          await _signalR.connect(sessionId);
        } catch (_) {}
      }
      nav.pushReplacementNamed('/home');
    } else {
      setState(() {
        _errorMessage = result['message'] ?? 'Đăng nhập thất bại';
      });
    }
  }

  Future<void> _handleWebQrLogin(String code, {VoidCallback? onSuccess}) async {
    if (_isLoading) return;
    
    setState(() {
      _isLoading = true;
      _errorMessage = null;
    });

    try {
      final api = ApiService();
      final result = await api.loginViaWebQr(code);
      
      if (!mounted) return;

      // Update auth provider with the login result
      final authProvider = context.read<AuthProvider>();
      // The API service already saved token to storage, so we just need to update provider state
      await authProvider.initialize();

      // Connect SignalR với sessionId thực tế
      final storage = StorageService();
      final sessionId = await storage.getSessionId();
      if (sessionId != null && sessionId.isNotEmpty) {
        try {
          await _signalR.connect(sessionId);
        } catch (_) {}
      }

      if (mounted) {
        // Pop QR screen nếu đang mở
        if (Navigator.of(context).canPop()) {
          Navigator.of(context).pop();
        }
        // Navigate đến home screen
        Navigator.of(context).pushReplacementNamed('/home');
      }
    } catch (e) {
      if (!mounted) return;
      setState(() {
        _errorMessage = e.toString();
      });
      _showSnack('Đăng nhập thất bại: $e');
    } finally {
      if (mounted) {
        setState(() => _isLoading = false);
            }
    }
  }


  @override
  Widget build(BuildContext context) {
    final authProvider = context.watch<AuthProvider>();

    return Scaffold(
      backgroundColor: AppTheme.background,
      body: SafeArea(
        child: Center(
          child: SingleChildScrollView(
            padding: const EdgeInsets.all(AppTheme.spacing24),
            child: Column(
              mainAxisAlignment: MainAxisAlignment.center,
              children: [
                Container(
                  width: 100,
                  height: 100,
                  decoration: BoxDecoration(
                    gradient: AppTheme.primaryGradient,
                    shape: BoxShape.circle,
                    boxShadow: AppTheme.cardShadow,
                  ),
                  child: const Icon(
                    Icons.phone_android,
                    size: 50,
                    color: Colors.white,
                  ),
                ),
                const SizedBox(height: AppTheme.spacing32),
                Text(
                  'Mobile Service System',
                  style: Theme.of(context).textTheme.displaySmall?.copyWith(
                        fontWeight: FontWeight.bold,
                      ),
                  textAlign: TextAlign.center,
                ),
                const SizedBox(height: AppTheme.spacing8),
                Text(
                  'Đăng nhập để tiếp tục',
                  style: Theme.of(context).textTheme.bodyLarge?.copyWith(
                        color: AppTheme.textSecondary,
                      ),
                ),
                const SizedBox(height: AppTheme.spacing32),
                Container(
                  decoration: BoxDecoration(
                    color: AppTheme.cardBackground,
                    borderRadius: BorderRadius.circular(AppTheme.radiusSmall),
                    border: Border.all(color: AppTheme.borderLight),
                    boxShadow: AppTheme.cardShadow,
                  ),
                  child: Row(
                    children: [
                      Expanded(
                        child: InkWell(
                          onTap: () {
                            setState(() => _loginType = LoginType.customer);
                          },
                          borderRadius: const BorderRadius.only(
                            topLeft: Radius.circular(AppTheme.radiusSmall),
                            bottomLeft: Radius.circular(AppTheme.radiusSmall),
                          ),
                          child: Container(
                            padding: const EdgeInsets.symmetric(
                              vertical: AppTheme.spacing12,
                            ),
                            decoration: BoxDecoration(
                              gradient: _loginType == LoginType.customer
                                  ? AppTheme.primaryGradient
                                  : null,
                              borderRadius: const BorderRadius.only(
                                topLeft: Radius.circular(AppTheme.radiusSmall),
                                bottomLeft: Radius.circular(AppTheme.radiusSmall),
                              ),
                            ),
                            child: Text(
                              'Khách hàng',
                              textAlign: TextAlign.center,
                              style: TextStyle(
                                color: _loginType == LoginType.customer
                                    ? Colors.white
                                    : AppTheme.textPrimary,
                                fontWeight: _loginType == LoginType.customer
                                    ? FontWeight.w600
                                    : FontWeight.normal,
                              ),
                            ),
                          ),
                        ),
                      ),
                      Container(
                        width: 1,
                        height: 40,
                        color: AppTheme.borderLight,
                      ),
                      Expanded(
                        child: InkWell(
                          onTap: () {
                            setState(() => _loginType = LoginType.employee);
                          },
                          borderRadius: const BorderRadius.only(
                            topRight: Radius.circular(AppTheme.radiusSmall),
                            bottomRight: Radius.circular(AppTheme.radiusSmall),
                          ),
                          child: Container(
                            padding: const EdgeInsets.symmetric(
                              vertical: AppTheme.spacing12,
                            ),
                            decoration: BoxDecoration(
                              gradient: _loginType == LoginType.employee
                                  ? AppTheme.primaryGradient
                                  : null,
                              borderRadius: const BorderRadius.only(
                                topRight: Radius.circular(AppTheme.radiusSmall),
                                bottomRight: Radius.circular(AppTheme.radiusSmall),
                              ),
                            ),
                            child: Text(
                              'Nhân viên',
                              textAlign: TextAlign.center,
                              style: TextStyle(
                                color: _loginType == LoginType.employee
                                    ? Colors.white
                                    : AppTheme.textPrimary,
                                fontWeight: _loginType == LoginType.employee
                                    ? FontWeight.w600
                                    : FontWeight.normal,
                              ),
                            ),
                          ),
                        ),
                      ),
                    ],
                  ),
                ),
                const SizedBox(height: AppTheme.spacing24),
                if (_errorMessage != null) ...[
                  ErrorBanner(
                    message: _errorMessage!,
                    onDismiss: () {
                      setState(() => _errorMessage = null);
                    },
                  ),
                  const SizedBox(height: AppTheme.spacing16),
                ],
                Form(
                  key: _formKey,
                  child: Column(
                    children: [
                      GradientFormField(
                        controller: _usernameCtrl,
                        label: _loginType == LoginType.customer
                            ? 'Số điện thoại'
                            : 'Tên đăng nhập',
                        hint: _loginType == LoginType.customer
                            ? 'Nhập số điện thoại'
                            : 'Nhập tên đăng nhập',
                        prefixIcon: _loginType == LoginType.customer
                            ? Icons.phone
                            : Icons.person,
                        keyboardType: _loginType == LoginType.customer
                            ? TextInputType.phone
                            : TextInputType.text,
                        validator: (value) {
                          if (value == null || value.trim().isEmpty) {
                            return 'Vui lòng nhập ${_loginType == LoginType.customer ? "số điện thoại" : "tên đăng nhập"}';
                          }
                          return null;
                        },
                      ),
                      const SizedBox(height: AppTheme.spacing16),
                      PasswordFormField(
                        controller: _passwordCtrl,
                        label: 'Mật khẩu',
                        hint: 'Nhập mật khẩu',
                        validator: (value) {
                          if (value == null || value.isEmpty) {
                            return 'Vui lòng nhập mật khẩu';
                          }
                          return null;
                        },
                        onSubmitted: (_) => _handleLogin(),
                      ),
                    ],
                  ),
                ),
                const SizedBox(height: AppTheme.spacing24),
                GradientButton(
                  text: 'Đăng nhập',
                  onPressed: authProvider.isLoading ? null : _handleLogin,
                  isLoading: authProvider.isLoading,
                  width: double.infinity,
                  icon: Icons.login,
                ),
                const SizedBox(height: AppTheme.spacing16),
                  OutlinedButton.icon(
                  onPressed: _isLoading ? null : _openQrScanner,
                  icon: const Icon(Icons.qr_code_scanner),
                  label: const Text('Quét QR đăng nhập'),
                    style: OutlinedButton.styleFrom(
                      foregroundColor: AppTheme.primary,
                      side: const BorderSide(color: AppTheme.primary, width: 2),
                      padding: const EdgeInsets.symmetric(
                        horizontal: AppTheme.spacing24,
                        vertical: AppTheme.spacing12,
                      ),
                      shape: RoundedRectangleBorder(
                        borderRadius: BorderRadius.circular(AppTheme.radiusSmall),
                      ),
                      minimumSize: const Size(double.infinity, 48),
                    ),
                  ),
                  const SizedBox(height: AppTheme.spacing16),
                if (_loginType == LoginType.customer) ...[
                  Row(
                    mainAxisAlignment: MainAxisAlignment.center,
                    children: [
                      Text(
                        'Chưa có tài khoản? ',
                        style: Theme.of(context).textTheme.bodyMedium,
                      ),
                      TextButton(
                        onPressed: () {
                          Navigator.of(context).pushNamed('/register');
                        },
                        child: const Text(
                          'Đăng ký ngay',
                          style: TextStyle(
                            fontWeight: FontWeight.w600,
                          ),
                        ),
                      ),
                    ],
                  ),
                ],
              ],
            ),
          ),
        ),
      ),
    );
  }
}

