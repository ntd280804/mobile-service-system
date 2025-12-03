class Order {
  final String orderId;
  final String customerPhone;
  final String receiverEmpName;
  final String handlerEmpName;
  final String orderType;
  final DateTime? receivedDate;
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

  factory Order.fromJson(Map<String, dynamic> json) {
    return Order(
      orderId: json['orderId']?.toString() ?? '',
      customerPhone: json['customerPhone']?.toString() ?? '',
      receiverEmpName: json['receiverEmpName'] ?? '',
      handlerEmpName: json['handlerEmpName'] ?? '',
      orderType: json['orderType'] ?? '',
      receivedDate: json['receivedDate'] != null
          ? DateTime.tryParse(json['receivedDate'])
          : null,
      status: json['status'] ?? '',
      description: json['description'] ?? '',
    );
  }
}
