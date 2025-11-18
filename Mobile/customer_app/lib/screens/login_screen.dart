import 'package:flutter/material.dart';

import '../services/api_service.dart';
import '../services/signalr_service.dart';

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
        if (!roles.contains('role_nhanvien') && !roles.contains('role_admin')) {
          _showSnack('Chỉ dành cho nhân viên');
          return;
        }
      }
      
      // Connect to SignalR after successful login
      final sessionId = res['sessionId'] ?? '';
      if (sessionId.isNotEmpty) {
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
              // Login type selector
              Container(
                decoration: BoxDecoration(
                  border: Border.all(color: Colors.grey.shade300),
                  borderRadius: BorderRadius.circular(8),
                ),
                child: Row(
                  children: [
                    Expanded(
                      child: GestureDetector(
                        onTap: () {
                          if (!_isLoading) {
                            setState(() => _loginType = LoginType.customer);
                          }
                        },
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
                        onTap: () {
                          if (!_isLoading) {
                            setState(() => _loginType = LoginType.employee);
                          }
                        },
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
                      : Text(_loginType == LoginType.customer
                          ? 'Đăng nhập'
                          : 'Đăng nhập nhân viên'),
                ),
              ),
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
            ],
          ),
        ),
      ),
    );
  }
}
