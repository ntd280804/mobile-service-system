class Order {
  final int orderId;
  final String customerPhone;
  final DateTime orderDate;
  final String status;
  final String orderType;
  final double? totalAmount;

  Order({
    required this.orderId,
    required this.customerPhone,
    required this.orderDate,
    required this.status,
    required this.orderType,
    this.totalAmount,
  });

  factory Order.fromJson(Map<String, dynamic> json) {
    return Order(
      orderId: json['orderId'] ?? json['OrderId'] ?? 0,
      customerPhone: json['customerPhone'] ?? json['CustomerPhone'] ?? '',
      orderDate: DateTime.parse(
        json['orderDate'] ?? json['OrderDate'] ?? DateTime.now().toIso8601String(),
      ),
      status: json['status'] ?? json['Status'] ?? 'UNKNOWN',
      orderType: json['orderType'] ?? json['OrderType'] ?? 'UNKNOWN',
      totalAmount: (json['totalAmount'] ?? json['TotalAmount'])?.toDouble(),
    );
  }
}
