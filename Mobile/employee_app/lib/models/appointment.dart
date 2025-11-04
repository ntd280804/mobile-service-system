class Appointment {
  final int appointmentId;
  final String customerPhone;
  final DateTime appointmentDate;
  final String status;
  final String? description;

  Appointment({
    required this.appointmentId,
    required this.customerPhone,
    required this.appointmentDate,
    required this.status,
    this.description,
  });

  factory Appointment.fromJson(Map<String, dynamic> json) {
    return Appointment(
      appointmentId: json['appointmentId'] ?? json['AppointmentId'] ?? 0,
      customerPhone: json['customerPhone'] ?? json['CustomerPhone'] ?? '',
      appointmentDate: DateTime.parse(
        json['appointmentDate'] ?? json['AppointmentDate'] ?? DateTime.now().toIso8601String(),
      ),
      status: json['status'] ?? json['Status'] ?? 'UNKNOWN',
      description: json['description'] ?? json['Description'],
    );
  }
}
