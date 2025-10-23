import 'package:flutter/material.dart';
import '../services/api_service.dart';
import '../widgets/custom_button.dart';

class LoginScreen extends StatefulWidget {
  const LoginScreen({super.key});

  @override
  State<LoginScreen> createState() => _LoginScreenState();
}

class _LoginScreenState extends State<LoginScreen> {
  final _userCtrl = TextEditingController();
  final _passCtrl = TextEditingController();
  String _result = "";

  void _login() async {
    setState(() => _result = "Đang xử lý...");
    final msg = await ApiService.login(_userCtrl.text.trim(), _passCtrl.text);
    if (msg == 'OK') {
      setState(() => _result = 'Đăng nhập thành công');
    } else {
      setState(() => _result = msg);
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text("Đăng nhập")),
      body: Padding(
        padding: const EdgeInsets.all(24.0),
        child: Column(
          mainAxisAlignment: MainAxisAlignment.center,
          children: [
            const Icon(Icons.phone_android, size: 80, color: Colors.blueAccent),
            const SizedBox(height: 30),
            TextField(
              controller: _userCtrl,
              decoration: const InputDecoration(
                labelText: "Tài khoản",
                prefixIcon: Icon(Icons.person),
                border: OutlineInputBorder(),
              ),
            ),
            const SizedBox(height: 16),
            TextField(
              controller: _passCtrl,
              obscureText: true,
              decoration: const InputDecoration(
                labelText: "Mật khẩu",
                prefixIcon: Icon(Icons.lock),
                border: OutlineInputBorder(),
              ),
            ),
            const SizedBox(height: 24),
            CustomButton(text: "Đăng nhập", onPressed: _login),
            const SizedBox(height: 16),
            Text(
              _result,
              style: const TextStyle(color: Colors.black87, fontSize: 16),
            )
          ],
        ),
      ),
    );
  }
}
