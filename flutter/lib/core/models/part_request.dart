class PartRequest {
  final int requestId;
  final int? orderId;
  final String empUsername;
  final DateTime requestDate;
  final String status;

  PartRequest({
    required this.requestId,
    this.orderId,
    required this.empUsername,
    required this.requestDate,
    required this.status,
  });

  factory PartRequest.fromJson(Map<String, dynamic> json) => PartRequest(
        requestId: json['REQUEST_ID'] is int ? json['REQUEST_ID'] : (json['REQUEST_ID'] as num).toInt(),
        orderId: json['ORDER_ID'] == null ? null : (json['ORDER_ID'] as num).toInt(),
        empUsername: json['EmpUsername'] ?? '',
        requestDate: DateTime.parse(json['REQUEST_DATE']),
        status: json['STATUS'] ?? '',
      );
}
