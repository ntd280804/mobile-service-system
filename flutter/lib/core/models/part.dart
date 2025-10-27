class Part {
  final int partId;
  final String name;
  final String? manufacturer;
  final String serial;
  final String? qrBase64; // backend returns byte[] base64
  final String status;
  final int stockinItemId;
  final int? orderId;
  final double? price;

  Part({
    required this.partId,
    required this.name,
    this.manufacturer,
    required this.serial,
    this.qrBase64,
    required this.status,
    required this.stockinItemId,
    this.orderId,
    this.price,
  });

  factory Part.fromJson(Map<String, dynamic> json) => Part(
        partId: (json['PartId'] as num).toInt(),
        name: json['Name'] ?? '',
        manufacturer: json['Manufacturer'],
        serial: json['Serial'] ?? '',
        qrBase64: json['QRImage'] != null ? (json['QRImage'] as String) : null,
        status: json['Status'] ?? '',
        stockinItemId: (json['StockinItemId'] as num).toInt(),
        orderId: json['OrderId'] == null ? null : (json['OrderId'] as num).toInt(),
        price: json['Price'] == null ? null : (json['Price'] as num).toDouble(),
      );
}
