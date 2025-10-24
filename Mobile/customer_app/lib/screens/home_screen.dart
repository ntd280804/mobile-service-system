import 'package:flutter/material.dart';
import '../services/api_service.dart';
import '../services/signalr_service.dart';
import 'order_list_screen.dart';
import 'appointment_list_screen.dart';

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
    // User was logged out from another session (e.g., web)
    if (mounted) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('Bạn đã đăng xuất từ thiết bị khác')),
      );
      _logout();
    }
  }

  Future<void> _logout() async {
    // Disconnect SignalR first
    await _signalR.disconnect();
    
    // Then logout from API
    await _api.logout();
    
    if (!mounted) return;
    Navigator.of(context).pushNamedAndRemoveUntil('/login', (route) => false);
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
            icon: const Icon(Icons.logout),
            onPressed: _logout,
            tooltip: 'Logout',
          )
        ],
      ),
      body: pages[_index],
      bottomNavigationBar: NavigationBar(
        selectedIndex: _index,
        onDestinationSelected: (i) => setState(() => _index = i),
        destinations: const [
          NavigationDestination(icon: Icon(Icons.timeline_outlined), label: 'Tiến độ'),
          NavigationDestination(icon: Icon(Icons.event_outlined), label: 'Lịch hẹn'),
        ],
      ),
    );
  }
}
