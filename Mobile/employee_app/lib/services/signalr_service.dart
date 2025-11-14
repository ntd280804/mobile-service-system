import 'package:signalr_netcore/signalr_client.dart';
import '../config/api_config.dart';

class SignalRService {
  static final SignalRService _instance = SignalRService._internal();
  factory SignalRService() => _instance;
  SignalRService._internal();

  HubConnection? _connection;
  Function()? onLogoutReceived;

  Future<void> connect(String sessionId) async {
    if (_connection != null) {
      await disconnect();
    }

    final hubUrl = '${ApiConfig.baseUrl}/notificationHub?sessionId=$sessionId';
    
    _connection = HubConnectionBuilder()
        .withUrl(hubUrl,
            options: HttpConnectionOptions(
              skipNegotiation: true,
              transport: HttpTransportType.WebSockets,
            ))
        .withAutomaticReconnect()
        .build();

    _connection!.on('ReceiveLogout', (arguments) {
      if (onLogoutReceived != null) {
        onLogoutReceived!();
      }
    });

    try {
      await _connection!.start();
    } catch (e) {
      print('SignalR connection error: $e');
    }
  }

  Future<void> disconnect() async {
    if (_connection != null) {
      await _connection!.stop();
      _connection = null;
    }
  }
}
