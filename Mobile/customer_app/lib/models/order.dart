class Order {
  final String id;
  final String status;

  Order({required this.id, required this.status});

  factory Order.fromJson(Map<String, dynamic> json) => Order(
        id: json['id']?.toString() ?? '',
        status: json['status']?.toString() ?? '',
      );
}
