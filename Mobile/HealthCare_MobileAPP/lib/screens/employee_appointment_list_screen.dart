import 'package:flutter/material.dart';
import '../models/employee_appointment.dart';
import '../services/api_service.dart';

class EmployeeAppointmentListScreen extends StatefulWidget {
  const EmployeeAppointmentListScreen({super.key});

  @override
  State<EmployeeAppointmentListScreen> createState() => _EmployeeAppointmentListScreenState();
}

class _EmployeeAppointmentListScreenState extends State<EmployeeAppointmentListScreen> {
  final ApiService _api = ApiService();
  List<EmployeeAppointment> _appointments = [];
  bool _isLoading = true;

  @override
  void initState() {
    super.initState();
    _loadAppointments();
  }

  Future<void> _loadAppointments() async {
    setState(() => _isLoading = true);
    try {
      final appointmentsData = await _api.getAllAppointments();
      setState(() {
        _appointments = appointmentsData
            .map((json) => EmployeeAppointment.fromJson(json))
            .toList();
        _isLoading = false;
      });
    } catch (e) {
      setState(() => _isLoading = false);
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(
            content: Text('Không thể tải lịch hẹn: ${e.toString()}'),
            backgroundColor: Colors.red,
          ),
        );
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: const Text('Lịch hẹn'),
        actions: [
          IconButton(
            icon: const Icon(Icons.refresh),
            onPressed: _loadAppointments,
          ),
        ],
      ),
      body: _isLoading
          ? const Center(child: CircularProgressIndicator())
          : RefreshIndicator(
              onRefresh: _loadAppointments,
              child: _appointments.isEmpty
                  ? const Center(child: Text('Không có lịch hẹn nào.'))
                  : ListView.separated(
                      padding: const EdgeInsets.all(12),
                      itemCount: _appointments.length,
                      separatorBuilder: (_, __) => const SizedBox(height: 8),
                      itemBuilder: (context, index) {
                        final appt = _appointments[index];
                        return Card(
                          child: ListTile(
                            leading: const Icon(Icons.event),
                            title: Text(
                              appt.customerPhone,
                              style: const TextStyle(fontWeight: FontWeight.bold),
                            ),
                            subtitle: Text(
                              'Ngày: ${appt.appointmentDate.toLocal().toString().substring(0, 16)}\nTrạng thái: ${appt.status}',
                            ),
                            trailing: appt.status == 'CANCELLED'
                                ? const Icon(Icons.cancel, color: Colors.red)
                                : appt.status == 'COMPLETED'
                                    ? const Icon(Icons.check_circle, color: Colors.green)
                                    : const Icon(Icons.schedule, color: Colors.blue),
                          ),
                        );
                      },
                    ),
            ),
    );
  }
}

