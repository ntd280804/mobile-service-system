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

  Future<void> _refreshAppointments() async {
    setState(() {
      _appointmentsFuture = _api.getAppointments();
    });
    await _appointmentsFuture;
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: const Text('Lịch hẹn'),
        actions: [
          IconButton(
            icon: const Icon(Icons.refresh),
            onPressed: _refreshAppointments,
            tooltip: 'Làm mới',
          ),
        ],
      ),
      body: FutureBuilder<List<Map<String, dynamic>>>(
        future: _appointmentsFuture,
        builder: (context, snapshot) {
          if (snapshot.connectionState == ConnectionState.waiting) {
            return const Center(child: CircularProgressIndicator());
          }

          if (snapshot.hasError) {
            return RefreshIndicator(
              onRefresh: _refreshAppointments,
              child: SingleChildScrollView(
                physics: const AlwaysScrollableScrollPhysics(),
                child: SizedBox(
                  height: MediaQuery.of(context).size.height * 0.7,
                  child: Center(
                    child: Column(
                      mainAxisAlignment: MainAxisAlignment.center,
                      children: [
                        Icon(Icons.error_outline, size: 64, color: Colors.red[300]),
                        const SizedBox(height: 16),
                        Text(
                          'Lỗi: ${snapshot.error}',
                          style: TextStyle(color: Colors.red[700], fontSize: 16),
                          textAlign: TextAlign.center,
                        ),
                        const SizedBox(height: 16),
                        ElevatedButton.icon(
                          onPressed: _refreshAppointments,
                          icon: const Icon(Icons.refresh),
                          label: const Text('Thử lại'),
                        ),
                      ],
                    ),
                  ),
                ),
              ),
            );
          }

          final appointments = snapshot.data ?? [];

          if (appointments.isEmpty) {
            return RefreshIndicator(
              onRefresh: _refreshAppointments,
              child: SingleChildScrollView(
                physics: const AlwaysScrollableScrollPhysics(),
                child: SizedBox(
                  height: MediaQuery.of(context).size.height * 0.7,
                  child: const Center(
                    child: Column(
                      mainAxisAlignment: MainAxisAlignment.center,
                      children: [
                        Icon(Icons.event_busy, size: 64, color: Colors.grey),
                        SizedBox(height: 16),
                        Text(
                          'Chưa có lịch hẹn',
                          style: TextStyle(color: Colors.grey, fontSize: 16),
                        ),
                      ],
                    ),
                  ),
                ),
              ),
            );
          }

          return RefreshIndicator(
            onRefresh: _refreshAppointments,
            child: ListView.separated(
              padding: const EdgeInsets.all(12),
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

                // Xác định icon và màu dựa trên trạng thái
                IconData statusIcon;
                Color statusColor;
                if (status.toUpperCase().contains('CANCELLED') ||
                    status.toUpperCase().contains('HỦY')) {
                  statusIcon = Icons.cancel;
                  statusColor = Colors.red;
                } else if (status.toUpperCase().contains('COMPLETED') ||
                    status.toUpperCase().contains('HOÀN THÀNH')) {
                  statusIcon = Icons.check_circle;
                  statusColor = Colors.green;
                } else {
                  statusIcon = Icons.schedule;
                  statusColor = Colors.blue;
                }

                return Card(
                  elevation: 2,
                  shape: RoundedRectangleBorder(
                    borderRadius: BorderRadius.circular(12),
                  ),
                  child: ListTile(
                    leading: CircleAvatar(
                      backgroundColor: Colors.blue,
                      radius: 28,
                      child: Text(
                        id.isNotEmpty ? id : '?',
                        style: const TextStyle(
                          color: Colors.white,
                          fontWeight: FontWeight.bold,
                        ),
                      ),
                    ),
                    title: Text(
                      'Lịch hẹn #$id',
                      style: const TextStyle(
                        fontWeight: FontWeight.bold,
                        fontSize: 16,
                      ),
                    ),
                    subtitle: Padding(
                      padding: const EdgeInsets.only(top: 8),
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Text('Ngày hẹn: $date'),
                          if (status.isNotEmpty)
                            Padding(
                              padding: const EdgeInsets.only(top: 4),
                              child: Row(
                                children: [
                                  Icon(statusIcon, size: 16, color: statusColor),
                                  const SizedBox(width: 4),
                                  Text(
                                    'Trạng thái: $status',
                                    style: TextStyle(color: statusColor),
                                  ),
                                ],
                              ),
                            ),
                          if (desc.isNotEmpty)
                            Padding(
                              padding: const EdgeInsets.only(top: 4),
                              child: Text(
                                'Mô tả: $desc',
                                style: TextStyle(color: Colors.grey[600]),
                              ),
                            ),
                        ],
                      ),
                    ),
                    isThreeLine: true,
                  ),
                );
              },
            ),
          );
        },
      ),
      floatingActionButton: FloatingActionButton.extended(
        onPressed: _openCreateAppointmentSheet,
        tooltip: 'Đặt lịch hẹn',
        icon: const Icon(Icons.add),
        label: const Text('Đặt lịch'),
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
