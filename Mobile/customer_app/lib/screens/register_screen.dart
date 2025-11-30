import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../theme/app_theme.dart';
import '../widgets/gradient_button.dart';
import '../widgets/gradient_form_field.dart';
import '../widgets/error_banner.dart';
import '../providers/auth_provider.dart';

class RegisterScreen extends StatefulWidget {
  const RegisterScreen({super.key});

  @override
  State<RegisterScreen> createState() => _RegisterScreenState();
}

class _RegisterScreenState extends State<RegisterScreen> {
  final _formKey = GlobalKey<FormState>();
  final _fullNameCtrl = TextEditingController();
  final _phoneCtrl = TextEditingController();
  final _emailCtrl = TextEditingController();
  final _addressCtrl = TextEditingController();
  final _passwordCtrl = TextEditingController();
  final _confirmPasswordCtrl = TextEditingController();
  
  String? _errorMessage;
  String? _successMessage;

  @override
  void dispose() {
    _fullNameCtrl.dispose();
    _phoneCtrl.dispose();
    _emailCtrl.dispose();
    _addressCtrl.dispose();
    _passwordCtrl.dispose();
    _confirmPasswordCtrl.dispose();
    super.dispose();
  }

  Future<void> _handleRegister() async {
    if (!_formKey.currentState!.validate()) {
      return;
    }

    setState(() {
      _errorMessage = null;
      _successMessage = null;
    });

    final authProvider = context.read<AuthProvider>();
    final result = await authProvider.registerCustomer(
      fullName: _fullNameCtrl.text.trim(),
      phone: _phoneCtrl.text.trim(),
      email: _emailCtrl.text.trim(),
      password: _passwordCtrl.text,
      address: _addressCtrl.text.trim().isEmpty ? null : _addressCtrl.text.trim(),
    );

    if (!mounted) return;

    if (result['success'] == true) {
      setState(() {
        _successMessage = 'Đăng ký thành công! Vui lòng đăng nhập.';
      });
      // Clear form
      _formKey.currentState!.reset();
      _fullNameCtrl.clear();
      _phoneCtrl.clear();
      _emailCtrl.clear();
      _addressCtrl.clear();
      _passwordCtrl.clear();
      _confirmPasswordCtrl.clear();
      
      // Navigate to login after 2 seconds
      Future.delayed(const Duration(seconds: 2), () {
        if (mounted) {
          Navigator.of(context).pushReplacementNamed('/login');
        }
      });
    } else {
      setState(() {
        _errorMessage = result['message'] ?? 'Đăng ký thất bại';
      });
    }
  }

  String? _validateFullName(String? value) {
    if (value == null || value.trim().isEmpty) {
      return 'Vui lòng nhập họ tên';
    }
    if (value.trim().length < 3) {
      return 'Họ tên phải có ít nhất 3 ký tự';
    }
    return null;
  }

  String? _validatePhone(String? value) {
    if (value == null || value.trim().isEmpty) {
      return 'Vui lòng nhập số điện thoại';
    }
    final phoneRegex = RegExp(r'^\d{10,11}$');
    if (!phoneRegex.hasMatch(value.trim())) {
      return 'Số điện thoại không hợp lệ (10-11 chữ số)';
    }
    return null;
  }

  String? _validateEmail(String? value) {
    if (value == null || value.trim().isEmpty) {
      return 'Vui lòng nhập email';
    }
    final emailRegex = RegExp(r'^[\w-\.]+@([\w-]+\.)+[\w-]{2,4}$');
    if (!emailRegex.hasMatch(value.trim())) {
      return 'Email không hợp lệ';
    }
    return null;
  }

  String? _validatePassword(String? value) {
    if (value == null || value.isEmpty) {
      return 'Vui lòng nhập mật khẩu';
    }
    if (value.length < 6) {
      return 'Mật khẩu phải có ít nhất 6 ký tự';
    }
    return null;
  }

  String? _validateConfirmPassword(String? value) {
    if (value == null || value.isEmpty) {
      return 'Vui lòng xác nhận mật khẩu';
    }
    if (value != _passwordCtrl.text) {
      return 'Mật khẩu xác nhận không khớp';
    }
    return null;
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      body: Container(
        decoration: BoxDecoration(
          gradient: AppTheme.headerGradient,
        ),
        child: SafeArea(
          child: Center(
            child: SingleChildScrollView(
              padding: const EdgeInsets.all(24.0),
              child: Column(
                mainAxisAlignment: MainAxisAlignment.center,
                children: [
                  // Logo/Icon
                  Container(
                    padding: const EdgeInsets.all(20),
                    decoration: BoxDecoration(
                      color: Colors.white.withValues(alpha: 0.15),
                      shape: BoxShape.circle,
                    ),
                    child: const Icon(
                      Icons.person_add_rounded,
                      size: 64,
                      color: Colors.white,
                    ),
                  ),
                  const SizedBox(height: 24),
                  const Text(
                    'Đăng ký tài khoản',
                    style: TextStyle(
                      fontSize: 32,
                      fontWeight: FontWeight.bold,
                      color: Colors.white,
                      shadows: [
                        Shadow(
                          offset: Offset(0, 2),
                          blurRadius: 4,
                          color: Colors.black26,
                        ),
                      ],
                    ),
                  ),
                  const SizedBox(height: 8),
                  const Text(
                    'Tạo tài khoản mới để sử dụng dịch vụ',
                    style: TextStyle(
                      fontSize: 16,
                      color: Colors.white70,
                    ),
                  ),
                  const SizedBox(height: 32),

                  // Form Card
                  Card(
                    elevation: 8,
                    shadowColor: Colors.black.withValues(alpha: 0.3),
                    shape: RoundedRectangleBorder(
                      borderRadius: BorderRadius.circular(16),
                    ),
                    child: Padding(
                      padding: const EdgeInsets.all(24.0),
                      child: Form(
                        key: _formKey,
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.stretch,
                          children: [
                            // Error/Success banners
                            if (_errorMessage != null)
                              Padding(
                                padding: const EdgeInsets.only(bottom: 16),
                                child: ErrorBanner(
                                  message: _errorMessage!,
                                ),
                              ),
                            if (_successMessage != null)
                              Padding(
                                padding: const EdgeInsets.only(bottom: 16),
                                child: SuccessBanner(
                                  message: _successMessage!,
                                ),
                              ),

                            // Full Name
                            GradientFormField(
                              controller: _fullNameCtrl,
                              label: 'Họ và tên',
                              prefixIcon: Icons.person_outline,
                              validator: _validateFullName,
                            ),
                            const SizedBox(height: 16),

                            // Phone
                            GradientFormField(
                              controller: _phoneCtrl,
                              label: 'Số điện thoại',
                              prefixIcon: Icons.phone_outlined,
                              keyboardType: TextInputType.phone,
                              validator: _validatePhone,
                            ),
                            const SizedBox(height: 16),

                            // Email
                            GradientFormField(
                              controller: _emailCtrl,
                              label: 'Email',
                              prefixIcon: Icons.email_outlined,
                              keyboardType: TextInputType.emailAddress,
                              validator: _validateEmail,
                            ),
                            const SizedBox(height: 16),

                            // Address (Optional)
                            GradientFormField(
                              controller: _addressCtrl,
                              label: 'Địa chỉ (Không bắt buộc)',
                              prefixIcon: Icons.location_on_outlined,
                              keyboardType: TextInputType.streetAddress,
                            ),
                            const SizedBox(height: 16),

                            // Password
                            PasswordFormField(
                              controller: _passwordCtrl,
                              label: 'Mật khẩu',
                              validator: _validatePassword,
                            ),
                            const SizedBox(height: 16),

                            // Confirm Password
                            PasswordFormField(
                              controller: _confirmPasswordCtrl,
                              label: 'Xác nhận mật khẩu',
                              validator: _validateConfirmPassword,
                            ),
                            const SizedBox(height: 24),

                            // Register Button
                            Consumer<AuthProvider>(
                              builder: (context, authProvider, _) {
                                return GradientButton(
                                  text: authProvider.isLoading ? 'Đang xử lý...' : 'Đăng ký',
                                  onPressed: authProvider.isLoading ? null : _handleRegister,
                                  isLoading: authProvider.isLoading,
                                );
                              },
                            ),
                            const SizedBox(height: 16),

                            // Already have account
                            Row(
                              mainAxisAlignment: MainAxisAlignment.center,
                              children: [
                                const Text(
                                  'Đã có tài khoản? ',
                                  style: TextStyle(color: Colors.grey),
                                ),
                                GestureDetector(
                                  onTap: () {
                                    Navigator.of(context).pushReplacementNamed('/login');
                                  },
                                  child: ShaderMask(
                                    shaderCallback: (bounds) => AppTheme.primaryGradient.createShader(bounds),
                                    child: const Text(
                                      'Đăng nhập',
                                      style: TextStyle(
                                        color: Colors.white,
                                        fontWeight: FontWeight.bold,
                                      ),
                                    ),
                                  ),
                                ),
                              ],
                            ),
                          ],
                        ),
                      ),
                    ),
                  ),
                ],
              ),
            ),
          ),
        ),
      ),
    );
  }
}
