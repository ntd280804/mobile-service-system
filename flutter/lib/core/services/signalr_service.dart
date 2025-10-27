import 'dart:async';
import 'package:signalr_core/signalr_core.dart';
import 'package:shared_preferences/shared_preferences.dart';

/// SignalR service singleton to manage a HubConnection to NotificationHub.
///
/// Usage:
/// - Call `SignalRService.instance.start(onForceLogout: (msg) { ... })` after login.
/// - Call `SignalRService.instance.stop()` before app exit or logout.
///
/// NOTE: Requires dependency in pubspec.yaml: `signalr_core: ^1.1.1`
class SignalRService {
  SignalRService._();
  static final SignalRService instance = SignalRService._();

  HubConnection? _hubConnection;
  bool _connected = false;
  Timer? _reconnectTimer;

  /// Candidate hub base URLs (adjust if needed)
  final List<String> _hubBaseCandidates = [
    'https://10.147.20.199:5131',
    'http://10.147.20.199:5131',
  ];

  /// Start and connect to the hub. Provide a callback to handle ForceLogout messages.
  Future<void> start({required void Function(String message) onForceLogout}) async {
    if (_connected && _hubConnection != null) {
      // Already connected, just update the callback
      _hubConnection!.on('ForceLogout', (args) {
        final msg = args != null && args.isNotEmpty ? args[0]?.toString() ?? '' : '';
        onForceLogout(msg);
      });
      return;
    }

    final prefs = await SharedPreferences.getInstance();
    final sessionId = prefs.getString('oracle_sessionId') ?? '';
    final token = prefs.getString('api_token') ?? '';

    if (sessionId.isEmpty) return;

    Exception? lastEx;

    for (final base in _hubBaseCandidates) {
      final hubUrl = '$base/Hubs/notification?sessionId=${Uri.encodeComponent(sessionId)}';

      try {
        // Cập nhật cú pháp mới của signalr_core
        final hubConnection = HubConnectionBuilder()
            .withUrl(
          hubUrl,
          HttpConnectionOptions(
            accessTokenFactory: () async => token,
          ),
        )
            .withAutomaticReconnect()
            .build();

        // Đăng ký event
        hubConnection.on('ForceLogout', (args) {
          final msg = args != null && args.isNotEmpty ? args[0]?.toString() ?? '' : '';
          try {
            onForceLogout(msg);
          } catch (_) {}
        });

        // Khi mất kết nối -> tự reconnect
        hubConnection.onclose((error) {
          _connected = false;
          _scheduleReconnect(onForceLogout);
        });

        await hubConnection.start();
        _hubConnection = hubConnection;
        _connected = true;
        _reconnectTimer?.cancel();
        return;
      } catch (e) {
        lastEx = e is Exception ? e : Exception(e.toString());
        continue;
      }
    }

    if (lastEx != null) {
      _scheduleReconnect(onForceLogout);
    }
  }

  void _scheduleReconnect(void Function(String message) onForceLogout) {
    if (_reconnectTimer?.isActive ?? false) return;
    _reconnectTimer = Timer(const Duration(seconds: 5), () async {
      await start(onForceLogout: onForceLogout);
    });
  }

  Future<void> stop() async {
    _reconnectTimer?.cancel();
    if (_hubConnection != null) {
      try {
        await _hubConnection!.stop();
      } catch (_) {}
      _hubConnection = null;
    }
    _connected = false;
  }

  bool get isConnected => _connected;
}
