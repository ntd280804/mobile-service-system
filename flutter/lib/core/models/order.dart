class Order {
  final int orderId;
  final String customerPhone;
  final String receiverEmpName;
  final String handlerEmpName;
  final String orderType;
  final DateTime receivedDate;
  final String status;
  final String description;

  Order({
    required this.orderId,
    required this.customerPhone,
    required this.receiverEmpName,
    required this.handlerEmpName,
    required this.orderType,
    required this.receivedDate,
    required this.status,
    required this.description,
  });

  factory Order.fromJson(Map<String, dynamic> json) => Order(
        orderId: (json['OrderId'] is int) ? json['OrderId'] : (json['OrderId'] as num).toInt(),
        customerPhone: json['CustomerPhone'] ?? '',
        receiverEmpName: json['ReceiverEmpName'] ?? '',
        handlerEmpName: json['HandlerEmpName'] ?? '',
        orderType: json['OrderType'] ?? '',
        receivedDate: DateTime.parse(json['ReceivedDate']),
        status: json['Status'] ?? '',
        description: json['Description'] ?? '',
      );
}
