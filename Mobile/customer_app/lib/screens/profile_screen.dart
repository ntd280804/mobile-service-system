import 'package:flutter/material.dart';

import '../services/storage_service.dart';

class ProfileScreen extends StatefulWidget {
  const ProfileScreen({super.key});

  @override
  State<ProfileScreen> createState() => _ProfileScreenState();
}

class _ProfileScreenState extends State<ProfileScreen> {
  final _storage = StorageService();
  String? _username;
  String? _role;
  String? _sessionId;
  bool _loading = true;

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    final username = await _storage.getUsername();
    final role = await _storage.getUserRole();
    final sessionId = await _storage.getSessionId();
    if (!mounted) return;
    setState(() {
      _username = username;
      _role = role;
      _sessionId = sessionId;
      _loading = false;
    });
  }

  @override
  Widget build(BuildContext context) {
    if (_loading) {
      return const Center(child: CircularProgressIndicator());
    }

    return Padding(
      padding: const EdgeInsets.all(16),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          const Text(
            'Tài khoản',
            style: TextStyle(fontSize: 20, fontWeight: FontWeight.bold),
          ),
          const SizedBox(height: 12),
          Card(
            child: Padding(
              padding: const EdgeInsets.all(16),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  _InfoRow(label: 'Tên đăng nhập', value: _username ?? '-'),
                  const SizedBox(height: 8),
                  _InfoRow(label: 'Vai trò', value: _role ?? '-'),
                  const SizedBox(height: 8),
                  _InfoRow(label: 'Session', value: _sessionId ?? '-'),
                ],
              ),
            ),
          ),
          const SizedBox(height: 16),
          const Text(
            'Hành động nhanh',
            style: TextStyle(fontSize: 16, fontWeight: FontWeight.w600),
          ),
          const SizedBox(height: 8),
          Wrap(
            spacing: 12,
            runSpacing: 12,
            children: [
              ElevatedButton.icon(
                onPressed: () => Navigator.pushNamed(context, '/appointments'),
                icon: const Icon(Icons.calendar_today),
                label: const Text('Xem lịch hẹn'),
              ),
              ElevatedButton.icon(
                onPressed: () => Navigator.pushNamed(context, '/order/list'),
                icon: const Icon(Icons.shopping_cart),
                label: const Text('Xem đơn hàng'),
              ),
              ElevatedButton.icon(
                onPressed: () => Navigator.pushNamed(context, '/change-password'),
                icon: const Icon(Icons.lock_outline),
                label: const Text('Đổi mật khẩu'),
              ),
            ],
          ),
        ],
      ),
    );
  }
}

class _InfoRow extends StatelessWidget {
  final String label;
  final String value;
  const _InfoRow({required this.label, required this.value});
  @override
  Widget build(BuildContext context) {
    return Row(
      children: [
        SizedBox(width: 140, child: Text(label, style: const TextStyle(color: Colors.black54))),
        Expanded(child: Text(value)),
      ],
    );
  }
}
