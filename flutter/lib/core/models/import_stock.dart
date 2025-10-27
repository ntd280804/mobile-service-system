class ImportItem {
  final String partName;
  final String? manufacturer;
  final String serial;
  final int price;

  ImportItem({required this.partName, this.manufacturer, required this.serial, required this.price});

  factory ImportItem.fromJson(Map<String, dynamic> json) => ImportItem(
        partName: json['PartName'] ?? '',
        manufacturer: json['Manufacturer'],
        serial: json['Serial'] ?? '',
        price: json['Price'] is int ? json['Price'] : (json['Price'] as num).toInt(),
      );
}

class ImportStock {
  final int stockInId;
  final String empUsername;
  final DateTime inDate;
  final String? note;
  final List<ImportItem> items;

  ImportStock({required this.stockInId, required this.empUsername, required this.inDate, this.note, required this.items});

  factory ImportStock.fromJson(Map<String, dynamic> json) => ImportStock(
        stockInId: json['StockInId'] is int ? json['StockInId'] : (json['StockInId'] as num).toInt(),
        empUsername: json['EmpUsername'] ?? '',
        inDate: DateTime.parse(json['InDate']),
        note: json['Note'],
        items: (json['Items'] as List<dynamic>?)?.map((e) => ImportItem.fromJson(e as Map<String, dynamic>)).toList() ?? [],
      );}
