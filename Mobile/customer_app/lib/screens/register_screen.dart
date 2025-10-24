import 'package:flutter/material.dart';
import '../services/api_service.dart';

class RegisterScreen extends StatefulWidget {
  const RegisterScreen({super.key});

  @override
  State<RegisterScreen> createState() => _RegisterScreenState();
}

class _RegisterScreenState extends State<RegisterScreen> {
  final _nameCtrl = TextEditingController();
  final _phoneCtrl = TextEditingController();
  final _emailCtrl = TextEditingController();
  final _addressCtrl = TextEditingController();
  final _pwdCtrl = TextEditingController();
  final _pwd2Ctrl = TextEditingController();

  bool _isLoading = false;
  final _api = ApiService();

  @override
  void dispose() {
    _nameCtrl.dispose();
    _phoneCtrl.dispose();
    _emailCtrl.dispose();
    _addressCtrl.dispose();
    _pwdCtrl.dispose();
    _pwd2Ctrl.dispose();
    super.dispose();
  }

  Future<void> _handleRegister() async {
    final name = _nameCtrl.text.trim();
    final phone = _phoneCtrl.text.trim();
    final email = _emailCtrl.text.trim();
    final address = _addressCtrl.text.trim();
    final pwd = _pwdCtrl.text;
    final pwd2 = _pwd2Ctrl.text;

    // Validation
    if (name.isEmpty) {
      _showSnack('Vui lòng nhập họ tên');
      return;
    }
    if (phone.isEmpty) {
      _showSnack('Vui lòng nhập số điện thoại');
      return;
    }
    if (email.isEmpty) {
      _showSnack('Vui lòng nhập email');
      return;
    }
    if (!email.contains('@')) {
      _showSnack('Email không hợp lệ');
      return;
    }
    if (pwd.isEmpty) {
      _showSnack('Vui lòng nhập mật khẩu');
      return;
    }
    if (pwd.length < 6) {
      _showSnack('Mật khẩu phải ít nhất 6 ký tự');
      return;
    }
    if (pwd != pwd2) {
      _showSnack('Mật khẩu xác nhận không khớp');
      return;
    }

    setState(() => _isLoading = true);
    try {
      await _api.register(
        fullName: name,
        phone: phone,
        email: email,
        password: pwd,
        address: address.isEmpty ? null : address,
      );
      
      if (!mounted) return;
      _showSnack('Đăng ký thành công! Vui lòng đăng nhập.');
      
      // Quay về màn hình login sau 1 giây
      await Future.delayed(const Duration(seconds: 1));
      if (!mounted) return;
      Navigator.pop(context);
    } catch (e) {
      _showSnack(e.toString());
    } finally {
      if (mounted) setState(() => _isLoading = false);
    }
  }

  void _showSnack(String msg) {
    ScaffoldMessenger.of(context).showSnackBar(
      SnackBar(content: Text(msg)),
    );
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('Đăng ký')),
      body: SingleChildScrollView(
        padding: const EdgeInsets.all(16),
        child: Column(
          children: [
            TextField(
              controller: _nameCtrl,
              decoration: const InputDecoration(labelText: 'Họ tên *'),
              enabled: !_isLoading,
            ),
            const SizedBox(height: 12),
            TextField(
              controller: _phoneCtrl,
              decoration: const InputDecoration(labelText: 'Số điện thoại *'),
              keyboardType: TextInputType.phone,
              enabled: !_isLoading,
            ),
            const SizedBox(height: 12),
            TextField(
              controller: _emailCtrl,
              decoration: const InputDecoration(labelText: 'Email *'),
              keyboardType: TextInputType.emailAddress,
              enabled: !_isLoading,
            ),
            const SizedBox(height: 12),
            TextField(
              controller: _addressCtrl,
              decoration: const InputDecoration(labelText: 'Địa chỉ'),
              enabled: !_isLoading,
            ),
            const SizedBox(height: 12),
            TextField(
              controller: _pwdCtrl,
              decoration: const InputDecoration(labelText: 'Mật khẩu *'),
              obscureText: true,
              enabled: !_isLoading,
            ),
            const SizedBox(height: 12),
            TextField(
              controller: _pwd2Ctrl,
              decoration: const InputDecoration(labelText: 'Xác nhận mật khẩu *'),
              obscureText: true,
              enabled: !_isLoading,
            ),
            const SizedBox(height: 20),
            SizedBox(
              width: double.infinity,
              child: ElevatedButton(
                onPressed: _isLoading ? null : _handleRegister,
                child: _isLoading
                    ? const SizedBox(
                        height: 18,
                        width: 18,
                        child: CircularProgressIndicator(strokeWidth: 2),
                      )
                    : const Text('Tạo tài khoản'),
              ),
            ),
          ],
        ),
      ),
    );
  }
}
