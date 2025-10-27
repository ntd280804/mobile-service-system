import 'package:flutter/material.dart';
import 'package:shared_preferences/shared_preferences.dart';
import '../../../core/services/api_service.dart';
import '../../auth/widgets/custom_button.dart';
import '../../employee/screens/employee_home_screen.dart';
import '../../customer/screens/customer_home_screen.dart';

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
      // Sau khi đăng nhập thành công, đọc role đã được lưu và chuyển hướng
      await _handlePostLogin();
    } else {
      setState(() => _result = msg);
    }
  }

  Future<void> _handlePostLogin() async {
    final prefs = await SharedPreferences.getInstance();
    final roles = prefs.getString('oracle_roles') ?? '';
    // roles có thể là chuỗi phân tách bằng dấu phẩy, ví dụ "ADMIN,THUKO"
    final roleList = roles.split(',').map((r) => r.trim().toUpperCase()).where((r) => r.isNotEmpty).toList();

    // Nếu là employee/admin (có role ADMIN) thì chuyển đến employee UI,
    // ngược lại chuyển đến customer UI.
    if (roleList.contains('ADMIN')) {
      if (!mounted) return;
      Navigator.of(context).pushReplacement(MaterialPageRoute(builder: (_) => const EmployeeHomeScreen()));
    } else {
      if (!mounted) return;
      Navigator.of(context).pushReplacement(MaterialPageRoute(builder: (_) => const CustomerHomeScreen()));
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
