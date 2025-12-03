class Part {
  final int partId;
  final String name;
  final String? manufacturer;
  final String serial;
  final String status;
  final int stockinID;
  final int? orderId;
  final double? price;

  Part({
    required this.partId,
    required this.name,
    this.manufacturer,
    required this.serial,
    required this.status,
    required this.stockinID,
    this.orderId,
    this.price,
  });

  factory Part.fromJson(Map<String, dynamic> json) {
    return Part(
      partId: json['partId'] ?? json['PartId'] ?? 0,
      name: json['name'] ?? json['Name'] ?? '',
      manufacturer: json['manufacturer'] ?? json['Manufacturer'],
      serial: json['serial'] ?? json['Serial'] ?? '',
      status: json['status'] ?? json['Status'] ?? '',
      stockinID: json['stockinID'] ?? json['StockinID'] ?? 0,
      orderId: json['orderId'] ?? json['OrderId'],
      price: json['price'] != null 
          ? (json['price'] is num ? json['price'].toDouble() : double.tryParse(json['price'].toString()))
          : null,
    );
  }
}

