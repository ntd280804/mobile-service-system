import 'package:flutter/material.dart';
import '../services/api_service.dart'; // 👈 nhớ đúng đường dẫn file

class AppointmentListScreen extends StatefulWidget {
  const AppointmentListScreen({super.key});

  @override
  State<AppointmentListScreen> createState() => _AppointmentListScreenState();
}

class _AppointmentListScreenState extends State<AppointmentListScreen> {
  final ApiService _api = ApiService();
  late Future<List<Map<String, dynamic>>> _appointmentsFuture;

  @override
  void initState() {
    super.initState();
    _appointmentsFuture = _api.getAppointments();
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('Danh sách lịch hẹn')),
      body: FutureBuilder<List<Map<String, dynamic>>>(
        future: _appointmentsFuture,
        builder: (context, snapshot) {
          if (snapshot.connectionState == ConnectionState.waiting) {
            return const Center(child: CircularProgressIndicator());
          }

          if (snapshot.hasError) {
            return Center(
              child: Text('Lỗi: ${snapshot.error}', style: const TextStyle(color: Colors.red)),
            );
          }

          final appointments = snapshot.data ?? [];

          if (appointments.isEmpty) {
            return const Center(
              child: Column(
                mainAxisAlignment: MainAxisAlignment.center,
                children: [
                  Icon(Icons.event_busy, size: 64, color: Colors.grey),
                  SizedBox(height: 16),
                  Text('Chưa có lịch hẹn', style: TextStyle(color: Colors.grey)),
                ],
              ),
            );
          }

          return ListView.separated(
            padding: const EdgeInsets.all(16),
            itemCount: appointments.length,
            separatorBuilder: (_, __) => const SizedBox(height: 8),
            itemBuilder: (context, index) {
              final appt = appointments[index];
              final id = appt['id']?.toString() ?? '';
              final date = appt['appointmentDate'] ??
                  appt['date'] ??
                  'Không rõ ngày';
              final status = appt['status'] ?? '';
              final desc = appt['description'] ?? appt['moTa'] ?? '';

              return Card(
                elevation: 2,
                child: ListTile(
                  leading: CircleAvatar(
                    backgroundColor: Colors.blueAccent,
                    child: Text(id, style: const TextStyle(color: Colors.white)),
                  ),
                  title: Text('Ngày hẹn: $date'),
                  subtitle: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text('Trạng thái: $status'),
                      Text('Mô tả: $desc'),
                    ],
                  ),
                ),
              );
            },
          );
        },
      ),
      floatingActionButton: FloatingActionButton(
        onPressed: _openCreateAppointmentSheet,
        tooltip: 'Đặt lịch hẹn',
        child: const Icon(Icons.add),
      ),
    );
  }

  void _openCreateAppointmentSheet() {
    final descController = TextEditingController();
    DateTime? selectedDate = DateTime.now();

    showModalBottomSheet(
      context: context,
      isScrollControlled: true,
      builder: (ctx) {
        return Padding(
          padding: EdgeInsets.only(
            left: 16,
            right: 16,
            top: 16,
            bottom: MediaQuery.of(ctx).viewInsets.bottom + 16,
          ),
          child: StatefulBuilder(
            builder: (context, setSheetState) {
              return Column(
                mainAxisSize: MainAxisSize.min,
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  const Text(
                    'Tạo lịch hẹn',
                    style: TextStyle(fontSize: 18, fontWeight: FontWeight.bold),
                  ),
                  const SizedBox(height: 12),
                  Row(
                    children: [
                      Expanded(
                        child: Text(
                          selectedDate == null
                              ? 'Chưa chọn ngày'
                              : 'Ngày: ${selectedDate!.toLocal().toString().split(' ').first}',
                        ),
                      ),
                      TextButton.icon(
                        icon: const Icon(Icons.date_range),
                        label: const Text('Chọn ngày'),
                        onPressed: () async {
                          final now = DateTime.now();
                          final picked = await showDatePicker(
                            context: context,
                            initialDate: selectedDate ?? now,
                            firstDate: DateTime(now.year, now.month, now.day),
                            lastDate: DateTime(now.year + 1),
                          );
                          if (picked != null) {
                            setSheetState(() => selectedDate = picked);
                          }
                        },
                      )
                    ],
                  ),
                  const SizedBox(height: 8),
                  TextField(
                    controller: descController,
                    decoration: const InputDecoration(
                      labelText: 'Mô tả (tuỳ chọn)',
                      border: OutlineInputBorder(),
                    ),
                    maxLines: 2,
                  ),
                  const SizedBox(height: 12),
                  SizedBox(
                    width: double.infinity,
                    child: FilledButton.icon(
                      icon: const Icon(Icons.save_outlined),
                      label: const Text('Tạo lịch hẹn'),
                      onPressed: () async {
                        if (selectedDate == null) return;

                        // Không cho chọn ngày quá khứ
                        final today = DateTime.now();
                        final onlyDate = DateTime(
                          selectedDate!.year,
                          selectedDate!.month,
                          selectedDate!.day,
                        );
                        final onlyToday = DateTime(today.year, today.month, today.day);
                        if (onlyDate.isBefore(onlyToday)) {
                          if (mounted) {
                            ScaffoldMessenger.of(context).showSnackBar(
                              const SnackBar(content: Text('Ngày hẹn phải từ hôm nay trở đi')),
                            );
                          }
                          return;
                        }

                        try {
                          await _api.createAppointment(onlyDate, description: descController.text);
                          if (!mounted) return;
                          Navigator.of(context).pop();
                          setState(() {
                            _appointmentsFuture = _api.getAppointments();
                          });
                          ScaffoldMessenger.of(context).showSnackBar(
                            const SnackBar(content: Text('Đặt lịch thành công')),
                          );
                        } catch (e) {
                          if (mounted) {
                            ScaffoldMessenger.of(context).showSnackBar(
                              SnackBar(content: Text(e.toString())),
                            );
                          }
                        }
                      },
                    ),
                  ),
                ],
              );
            },
          ),
        );
      },
    );
  }
}
