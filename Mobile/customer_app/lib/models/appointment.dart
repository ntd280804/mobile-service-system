class Appointment {
  final String id;
  final String date;

  Appointment({required this.id, required this.date});

  factory Appointment.fromJson(Map<String, dynamic> json) => Appointment(
        id: json['id']?.toString() ?? '',
        date: json['date']?.toString() ?? '',
      );
}
