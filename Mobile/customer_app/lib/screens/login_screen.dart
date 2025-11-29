import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../theme/app_theme.dart';
import '../widgets/gradient_button.dart';
import '../widgets/gradient_form_field.dart';
import '../widgets/error_banner.dart';
import '../providers/auth_provider.dart';
import '../services/signalr_service.dart';
import 'web_qr_login_sheet.dart';

enum LoginType { customer, employee }

class LoginScreen extends StatefulWidget {
  const LoginScreen({super.key});

  @override
  State<LoginScreen> createState() => _LoginScreenState();
}

class _LoginScreenState extends State<LoginScreen> {
  final _formKey = GlobalKey<FormState>();
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
      try {
        await _signalR.connect('session-placeholder');
      } catch (_) {}
      nav.pushReplacementNamed('/home');
    } else {
      setState(() {
        _errorMessage = result['message'] ?? 'Đăng nhập thất bại';
      });
    }
  }

  Future<void> _handleWebQrLogin(String code, {VoidCallback? onSuccess}) async {
    try {
      if (mounted) {
        showErrorSnackBar(context, 'QR login chưa được cấu hình cho Provider');
      }
    } catch (e) {
      rethrow;
    }
  }

  void _openWebQrLoginSheet() {
    showModalBottomSheet(
      context: context,
      isScrollControlled: true,
      isDismissible: true,
      shape: const RoundedRectangleBorder(
        borderRadius: BorderRadius.vertical(top: Radius.circular(16)),
      ),
      builder: (sheetContext) => WebQrLoginSheet(
        onSubmit: (code) async {
          await _handleWebQrLogin(code, onSuccess: () {
            if (sheetContext.mounted) {
              Navigator.of(sheetContext).pop();
            }
          });
        },
      ),
    );
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
                if (_loginType == LoginType.customer) ...[
                  OutlinedButton.icon(
                    onPressed: _openWebQrLoginSheet,
                    icon: const Icon(Icons.qr_code),
                    label: const Text('Đăng nhập bằng QR'),
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
                ],
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

