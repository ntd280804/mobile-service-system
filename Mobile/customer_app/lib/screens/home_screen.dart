import 'package:flutter/material.dart';

import '../services/api_service.dart';
import '../services/signalr_service.dart';
import '../services/storage_service.dart';
import 'appointment_list_screen.dart';
import 'order_list_screen.dart';
import 'employee_home_screen.dart';
import 'customer_dashboard_screen.dart';
import 'qr_web_login_sheet.dart';
import 'qr_scan_screen.dart';


class HomeScreen extends StatefulWidget {
  const HomeScreen({super.key});

  @override
  State<HomeScreen> createState() => _HomeScreenState();
}

class _HomeScreenState extends State<HomeScreen> {
  int _index = 0;
  final _api = ApiService();
  final _signalR = SignalRService();
  final _storage = StorageService();
  String? _userRole;
  bool _isLoading = true;

  @override
  void initState() {
    super.initState();
    // Listen to logout event from SignalR
    _signalR.onLogoutReceived = _handleRemoteLogout;
    _checkUserRole();
  }

  Future<void> _checkUserRole() async {
    final role = await _storage.getUserRole();
    setState(() {
      _userRole = role;
      _isLoading = false;
    });
  }

  bool get _isEmployee {
    if (_userRole == null) return false;
    final roleLower = _userRole!.toLowerCase();
    return roleLower.contains('role_nhanvien') || roleLower.contains('role_admin');
  }

  void _handleRemoteLogout() {
    if (mounted) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('Bạn đã đăng xuất')),
      );
      _logout();
    }
  }

  Future<void> _showLogoutDialog() async {
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (context) => AlertDialog(
        title: const Text('Đăng xuất'),
        content: const Text('Bạn có chắc chắn muốn đăng xuất?'),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(context, false),
            child: const Text('Hủy'),
          ),
          TextButton(
            onPressed: () => Navigator.pop(context, true),
            child: const Text('Đăng xuất'),
          ),
        ],
      ),
    );

    if (confirmed == true) {
      await _logout();
    }
  }

  Future<void> _logout() async {
    try {
      await _signalR.disconnect();
      await _api.logout();
      if (!mounted) return;
      Navigator.of(context).pushNamedAndRemoveUntil('/login', (route) => false);
    } catch (e) {
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text('Đăng xuất thất bại: ${e.toString()}')),
      );
    }
  }

  void _openQrLoginSheet() {
    showQrLoginSheet(context);
  }

  Future<void> _openQrScanner() async {
    final code = await Navigator.of(context).push<String>(
      MaterialPageRoute(builder: (_) => const QrScanScreen()),
    );
    if (!mounted || code == null) return;
    ScaffoldMessenger.of(context).showSnackBar(
      SnackBar(content: Text('Đã xử lý mã: $code')),
    );
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
    if (_isLoading) {
      return const Scaffold(
        body: Center(child: CircularProgressIndicator()),
      );
    }

    // If employee, show employee home screen
    if (_isEmployee) {
      return const EmployeeHomeScreen();
    }

    // Otherwise show customer home screen
    final pages = [
      const CustomerDashboardScreen(),
      const OrderListScreen(),
      const AppointmentListScreen(),
    ];

    return Scaffold(
      appBar: AppBar(
        title: const Text('HealthCare - Khách hàng'),
        actions: [
          IconButton(
            icon: const Icon(Icons.qr_code_scanner),
            tooltip: 'Quét QR đăng nhập Web',
            onPressed: _openQrScanner,
          ),
          IconButton(
            icon: const Icon(Icons.qr_code),
            tooltip: 'Đăng nhập Web (nhập mã từ trình duyệt)',
            onPressed: _openQrLoginSheet,
          ),
          IconButton(
            icon: const Icon(Icons.lock_outline),
            tooltip: 'Đổi mật khẩu',
            onPressed: _showChangePasswordDialog,
          ),
          IconButton(
            icon: const Icon(Icons.logout),
            tooltip: 'Đăng xuất',
            onPressed: _showLogoutDialog,
          ),
        ],
      ),
      body: pages[_index],
      bottomNavigationBar: NavigationBar(
        selectedIndex: _index,
        onDestinationSelected: (i) => setState(() => _index = i),
        destinations: const [
          NavigationDestination(
            icon: Icon(Icons.dashboard_outlined),
            selectedIcon: Icon(Icons.dashboard),
            label: 'Dashboard',
          ),
          NavigationDestination(
            icon: Icon(Icons.shopping_cart_outlined),
            selectedIcon: Icon(Icons.shopping_cart),
            label: 'Đơn hàng',
          ),
          NavigationDestination(
            icon: Icon(Icons.calendar_today_outlined),
            selectedIcon: Icon(Icons.calendar_today),
            label: 'Lịch hẹn',
          ),
        ],
      ),
    );
  }
}
