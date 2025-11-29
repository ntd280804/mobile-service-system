import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../providers/auth_provider.dart';
import '../widgets/gradient_button.dart';
import '../widgets/gradient_form_field.dart';
import '../widgets/error_banner.dart';

class ChangePasswordScreen extends StatefulWidget {
  const ChangePasswordScreen({super.key});

  @override
  State<ChangePasswordScreen> createState() => _ChangePasswordScreenState();
}

class _ChangePasswordScreenState extends State<ChangePasswordScreen> {
  final _formKey = GlobalKey<FormState>();
  final _oldController = TextEditingController();
  final _newController = TextEditingController();
  final _confirmController = TextEditingController();

  String? _error;
  String? _success;
  bool _submitting = false;

  @override
  void dispose() {
    _oldController.dispose();
    _newController.dispose();
    _confirmController.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    if (!_formKey.currentState!.validate()) return;
    final oldPass = _oldController.text.trim();
    final newPass = _newController.text.trim();
    final confirmPass = _confirmController.text.trim();

    if (newPass != confirmPass) {
      setState(() {
        _error = 'Mật khẩu mới không khớp';
        _success = null;
      });
      return;
    }

    setState(() {
      _error = null;
      _success = null;
      _submitting = true;
    });

    try {
      await context.read<AuthProvider>().changePassword(oldPassword: oldPass, newPassword: newPass);
      if (!mounted) return;
      setState(() {
        _success = 'Đổi mật khẩu thành công';
        _submitting = false;
      });
    } catch (e) {
      if (!mounted) return;
      setState(() {
        _error = e.toString();
        _submitting = false;
      });
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('Đổi mật khẩu')),
      body: SingleChildScrollView(
        padding: const EdgeInsets.all(16),
        child: Form(
          key: _formKey,
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              if (_error != null) ErrorBanner(message: _error!),
              if (_success != null) const SizedBox(height: 8),
              if (_success != null) SuccessBanner(message: _success!),
              const SizedBox(height: 12),
              PasswordFormField(
                controller: _oldController,
                label: 'Mật khẩu cũ',
                validator: (v) => (v == null || v.trim().isEmpty) ? 'Nhập mật khẩu cũ' : null,
              ),
              const SizedBox(height: 12),
              PasswordFormField(
                controller: _newController,
                label: 'Mật khẩu mới',
                validator: (v) => (v == null || v.trim().length < 6) ? 'Ít nhất 6 ký tự' : null,
              ),
              const SizedBox(height: 12),
              PasswordFormField(
                controller: _confirmController,
                label: 'Xác nhận mật khẩu mới',
                validator: (v) => (v == null || v.trim().isEmpty) ? 'Nhập lại mật khẩu' : null,
              ),
              const SizedBox(height: 20),
              GradientButton(
                text: 'Đổi mật khẩu',
                onPressed: _submitting ? null : _submit,
              ),
            ],
          ),
        ),
      ),
    );
  }
}
