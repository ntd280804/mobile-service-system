import 'package:flutter/material.dart';
import '../models/appointment.dart';
import '../services/api_service.dart';
import 'package:url_launcher/url_launcher.dart';

class AppointmentListScreen extends StatefulWidget {
  const AppointmentListScreen({super.key});

  @override
  State<AppointmentListScreen> createState() => _AppointmentListScreenState();
}

class _AppointmentListScreenState extends State<AppointmentListScreen> {
  final ApiService _api = ApiService();
  List<Appointment> _appointments = [];
  bool _isLoading = true;

  @override
  void initState() {
    super.initState();
    _loadAppointments();
  }

  Future<void> _loadAppointments() async {
    setState(() => _isLoading = true);
    try {
      final appointments = await _api.getAllAppointments();
      setState(() {
        _appointments = appointments;
        _isLoading = false;
      });
    } catch (e) {
      setState(() => _isLoading = false);
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(
            content: Text('Failed to load appointments: ${e.toString()}'),
            backgroundColor: Colors.red,
          ),
        );
      }
    }
  }

  Future<void> _launchUrl(String url) async {
    if (!await launchUrl(Uri.parse(url))) {
      throw 'Could not launch $url';
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: const Text('Appointments'),
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
                  ? const Center(child: Text('No appointments found.'))
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
                              'Date: ${appt.appointmentDate.toLocal().toString().substring(0, 16)}\nStatus: ${appt.status}',
                            ),
                            trailing: appt.status == 'CANCELLED'
                                ? const Icon(Icons.cancel, color: Colors.red)
                                : appt.status == 'COMPLETED'
                                    ? const Icon(Icons.check_circle, color: Colors.green)
                                    : const Icon(Icons.schedule, color: Colors.blue),
                            onTap: () => _launchUrl('https://example.com/appointment/${appt.id}'),
                          ),
                        );
                      },
                    ),
            ),
    );
  }
}
