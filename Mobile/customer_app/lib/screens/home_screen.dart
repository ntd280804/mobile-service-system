import 'package:flutter/material.dart';

import '../services/api_service.dart';
import '../services/signalr_service.dart';
import 'appointment_list_screen.dart';
import 'order_list_screen.dart';


class HomeScreen extends StatefulWidget {
  const HomeScreen({super.key});

  @override
  State<HomeScreen> createState() => _HomeScreenState();
}

class _HomeScreenState extends State<HomeScreen> {
  int _index = 0;
  final _api = ApiService();
  final _signalR = SignalRService();

  @override
  void initState() {
    super.initState();
    // Listen to logout event from SignalR
    _signalR.onLogoutReceived = _handleRemoteLogout;
  }

  void _handleRemoteLogout() {
    if (mounted) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('Bạn đã đăng xuất từ thiết bị khác')),
      );
      _logout();
    }
  }

  Future<void> _logout() async {
    await _signalR.disconnect();
    await _api.logout();
    if (!mounted) return;
    Navigator.of(context).pushNamedAndRemoveUntil('/login', (route) => false);
  }

  // ================= Change Password =================
  void _showChangePasswordDialog() {
    final oldController = TextEditingController();
    final newController = TextEditingController();
    final confirmController = TextEditingController();

    showDialog(
      context: context,
      builder: (ctx) => AlertDialog(
        title: const Text('Đổi mật khẩu'),
        content: SingleChildScrollView(
          child: Column(
            children: [
              TextField(
                controller: oldController,
                decoration: const InputDecoration(
                  labelText: 'Mật khẩu cũ',
                ),
                obscureText: true,
              ),
              const SizedBox(height: 8),
              TextField(
                controller: newController,
                decoration: const InputDecoration(
                  labelText: 'Mật khẩu mới',
                ),
                obscureText: true,
              ),
              const SizedBox(height: 8),
              TextField(
                controller: confirmController,
                decoration: const InputDecoration(
                  labelText: 'Xác nhận mật khẩu mới',
                ),
                obscureText: true,
              ),
            ],
          ),
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.of(ctx).pop(),
            child: const Text('Hủy'),
          ),
          ElevatedButton(
            onPressed: () async {
              final oldPass = oldController.text.trim();
              final newPass = newController.text.trim();
              final confirmPass = confirmController.text.trim();

              if (newPass != confirmPass) {
                ScaffoldMessenger.of(context).showSnackBar(
                  const SnackBar(content: Text('Mật khẩu mới không khớp')),
                );
                return;
              }

              try {
                await _api.changePassword(oldPass, newPass);
                if (!mounted) return;
                Navigator.of(ctx).pop();
                ScaffoldMessenger.of(context).showSnackBar(
                  const SnackBar(content: Text('Đổi mật khẩu thành công')),
                );
              } catch (e) {
                if (mounted) {
                  ScaffoldMessenger.of(context).showSnackBar(
                    SnackBar(content: Text('Lỗi: $e')),
                  );
                }
              }
            },
            child: const Text('Đổi mật khẩu'),
          ),
        ],
      ),
    );
  }

  @override
  void dispose() {
    _signalR.onLogoutReceived = null;
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final pages = [
      const OrderListScreen(),
      const AppointmentListScreen(),
    ];

    return Scaffold(
      appBar: AppBar(
        title: const Text('Mobile Service'),
        actions: [
          IconButton(
            icon: const Icon(Icons.lock_outline),
            tooltip: 'Đổi mật khẩu',
            onPressed: _showChangePasswordDialog,
          ),
          IconButton(
            icon: const Icon(Icons.logout),
            tooltip: 'Logout',
            onPressed: _logout,
          ),
        ],
      ),
      body: pages[_index],

      bottomNavigationBar: NavigationBar(
        selectedIndex: _index,
        onDestinationSelected: (i) => setState(() => _index = i),
        destinations: const [
          NavigationDestination(
              icon: Icon(Icons.timeline_outlined), label: 'Tiến độ'),
          NavigationDestination(
              icon: Icon(Icons.event_outlined), label: 'Lịch hẹn'),
        ],
      ),
    );
  }
}
