import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../providers/appointments_provider.dart';
import '../widgets/status_chip.dart';
import '../widgets/error_banner.dart';
import '../theme/app_theme.dart';

class AppointmentsScreen extends StatefulWidget {
  const AppointmentsScreen({super.key});

  @override
  State<AppointmentsScreen> createState() => _AppointmentsScreenState();
}

class _AppointmentsScreenState extends State<AppointmentsScreen> {
  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addPostFrameCallback((_) {
      context.read<AppointmentsProvider>().fetchAppointments();
    });
  }

  void _openCreateSheet() {
    showModalBottomSheet(
      context: context,
      isScrollControlled: true,
      shape: const RoundedRectangleBorder(
        borderRadius: BorderRadius.vertical(top: Radius.circular(16)),
      ),
      builder: (ctx) => const CreateAppointmentSheet(),
    );
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: const Text('Lịch hẹn'),
        actions: [
          IconButton(
            onPressed: () => context.read<AppointmentsProvider>().refreshAppointments(),
            icon: const Icon(Icons.refresh),
          ),
          IconButton(
            onPressed: _openCreateSheet,
            icon: const Icon(Icons.add_circle_outline),
          ),
        ],
      ),
      body: Consumer<AppointmentsProvider>(
        builder: (context, provider, _) {
          if (provider.isLoading) {
            return const Center(child: CircularProgressIndicator());
          }
          if (provider.errorMessage != null) {
            return Padding(
              padding: const EdgeInsets.all(16.0),
              child: ErrorBanner(message: provider.errorMessage!),
            );
          }
          final upcoming = provider.upcomingAppointments;
          final past = provider.pastAppointments;
          return RefreshIndicator(
            onRefresh: () => provider.refreshAppointments(),
            child: ListView(
              padding: const EdgeInsets.all(16),
              children: [
                _sectionHeader('Sắp tới', Icons.event_available),
                if (upcoming.isEmpty)
                  const Text('Không có lịch hẹn sắp tới', style: TextStyle(color: Colors.grey))
                else
                  ...upcoming.map(_appointmentTile),
                const SizedBox(height: 16),
                _sectionHeader('Đã qua', Icons.history),
                if (past.isEmpty)
                  const Text('Không có lịch hẹn đã qua', style: TextStyle(color: Colors.grey))
                else
                  ...past.map(_appointmentTile),
              ],
            ),
          );
        },
      ),
      floatingActionButton: FloatingActionButton(
        onPressed: _openCreateSheet,
        child: const Icon(Icons.add),
      ),
    );
  }

  Widget _sectionHeader(String title, IconData icon) {
    return Row(
      children: [
        Icon(icon, color: AppTheme.primary),
        const SizedBox(width: 8),
        Text(title, style: const TextStyle(fontSize: 18, fontWeight: FontWeight.bold)),
      ],
    );
  }

  Widget _appointmentTile(Map<String, dynamic> a) {
    final date = a['appointmentDate']?.toString() ?? '';
    final desc = a['description']?.toString() ?? '';
    final status = a['status']?.toString() ?? 'Đặt lịch';
    return Card(
      elevation: 2,
      shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(12)),
      child: ListTile(
        leading: const Icon(Icons.event_note),
        title: Text(date.isNotEmpty ? date : 'Không rõ ngày'),
        subtitle: desc.isNotEmpty ? Text(desc) : null,
        trailing: StatusChip(status: status),
      ),
    );
  }
}

class CreateAppointmentSheet extends StatefulWidget {
  const CreateAppointmentSheet({super.key});

  @override
  State<CreateAppointmentSheet> createState() => _CreateAppointmentSheetState();
}

class _CreateAppointmentSheetState extends State<CreateAppointmentSheet> {
  DateTime? _selectedDate;
  final TextEditingController _descCtrl = TextEditingController();
  bool _submitting = false;
  String? _error;

  @override
  void dispose() {
    _descCtrl.dispose();
    super.dispose();
  }

  Future<void> _pickDate() async {
    final now = DateTime.now();
    final picked = await showDatePicker(
      context: context,
      initialDate: now,
      firstDate: now,
      lastDate: DateTime(now.year + 1),
    );
    if (picked != null) {
      setState(() => _selectedDate = picked);
    }
  }

  Future<void> _submit() async {
    if (_selectedDate == null) {
      setState(() => _error = 'Vui lòng chọn ngày hẹn');
      return;
    }
    setState(() { _submitting = true; _error = null; });
    try {
      await context.read<AppointmentsProvider>().createAppointment(date: _selectedDate!, description: _descCtrl.text.trim());
      if (mounted) Navigator.of(context).pop();
    } catch (e) {
      setState(() => _error = e.toString());
    } finally {
      if (mounted) setState(() => _submitting = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: EdgeInsets.only(bottom: MediaQuery.of(context).viewInsets.bottom),
      child: SingleChildScrollView(
        padding: const EdgeInsets.all(16),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            const Text('Đặt lịch hẹn', style: TextStyle(fontSize: 18, fontWeight: FontWeight.bold)),
            const SizedBox(height: 12),
            if (_error != null) ...[
              ErrorBanner(message: _error!),
              const SizedBox(height: 12),
            ],
            Row(
              children: [
                Expanded(
                  child: Text(_selectedDate != null
                      ? 'Ngày: ${_selectedDate!.toLocal().toString().split(' ').first}'
                      : 'Chưa chọn ngày'),
                ),
                TextButton.icon(
                  onPressed: _submitting ? null : _pickDate,
                  icon: const Icon(Icons.calendar_today),
                  label: const Text('Chọn ngày'),
                ),
              ],
            ),
            const SizedBox(height: 12),
            TextField(
              controller: _descCtrl,
              decoration: const InputDecoration(labelText: 'Ghi chú (không bắt buộc)'),
              maxLines: 3,
            ),
            const SizedBox(height: 16),
            ElevatedButton(
              onPressed: _submitting ? null : _submit,
              child: _submitting
                  ? const SizedBox(height: 18, width: 18, child: CircularProgressIndicator(strokeWidth: 2))
                  : const Text('Đặt lịch'),
            ),
          ],
        ),
      ),
    );
  }
}
