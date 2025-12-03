import 'dart:async';
import 'dart:io'; // ✅ Import để bypass SSL

import 'package:flutter/foundation.dart';
import 'package:signalr_netcore/signalr_client.dart';

import '../config/api_config.dart';

class SignalRService {
  SignalRService._internal() {
    // ⚠️ DEVELOPMENT ONLY: Bypass SSL for SignalR
    HttpOverrides.global = _DevHttpOverrides();
  }
  
  static final SignalRService _instance = SignalRService._internal();
  factory SignalRService() => _instance;

  HubConnection? _connection;
  Timer? _reconnectTimer;
  bool _wasConnected = false;
  bool _isReconnecting = false;
  
  Function()? onLogoutReceived;
  Function()? onConnectionClosed;

  Future<bool> connect(String sessionId) async {
    if (_connection != null) {
      await disconnect();
    }

    try {
      // Build SignalR hub URL with sessionId
      final hubUrl = '${ApiConfig.baseUrl}/Hubs/notification?sessionId=$sessionId';
      
      if (kDebugMode) debugPrint('SignalR connecting: $hubUrl');

      _connection = HubConnectionBuilder()
          .withUrl(hubUrl)
          .withAutomaticReconnect()
          .build();

      // Listen to 'ForceLogout' event from server (matching WebApp)
      _connection!.on('ForceLogout', _handleLogout);

      // Connection state listeners
      _wasConnected = false;
      
      _connection!.onclose(({error}) {
        if (kDebugMode) debugPrint('SignalR connection closed: $error');
        // Nếu đã từng connect và bây giờ bị đóng
        if (_wasConnected) {
          _cancelReconnectTimer();
          // Nếu không đang reconnect, logout ngay lập tức
          if (!_isReconnecting) {
            if (onConnectionClosed != null) {
              onConnectionClosed!();
            }
          } else {
            // Nếu đang reconnect, đợi 1.5 giây để xem có reconnect được không
            _reconnectTimer = Timer(const Duration(milliseconds: 1500), () {
              if (onConnectionClosed != null && !isConnected) {
                onConnectionClosed!();
              }
            });
          }
        }
        _wasConnected = false;
        _isReconnecting = false;
      });

      _connection!.onreconnecting(({error}) {
        if (kDebugMode) debugPrint('SignalR reconnecting: $error');
        _isReconnecting = true;
        // Hủy timer nếu đang reconnect
        _cancelReconnectTimer();
      });

      _connection!.onreconnected(({connectionId}) {
        if (kDebugMode) debugPrint('SignalR reconnected: $connectionId');
        _cancelReconnectTimer();
        _wasConnected = true;
        _isReconnecting = false;
      });
      
      // Start connection with timeout
      if (_connection == null) {
        throw Exception('SignalR connection is null');
      }
      
      final startFuture = _connection!.start();
      if (startFuture == null) {
        throw Exception('SignalR start() returned null');
      }
      
      await startFuture.timeout(
        const Duration(seconds: 10),
        onTimeout: () {
          throw Exception('SignalR connection timeout');
        },
      );
      
      // Mark as connected only after successful start
      _wasConnected = true;
      
      if (kDebugMode) debugPrint('SignalR connected successfully');
      return true;
    } catch (e) {
      if (kDebugMode) debugPrint('SignalR connection error: $e');
      _wasConnected = false;
      _isReconnecting = false;
      _connection = null;
      return false;
    }
  }

  void _handleLogout(List<Object?>? arguments) {
    if (kDebugMode) debugPrint('SignalR received logout event');
    
    // Call the callback to trigger logout in UI
    if (onLogoutReceived != null) {
      onLogoutReceived!();
    }
  }

  void _cancelReconnectTimer() {
    _reconnectTimer?.cancel();
    _reconnectTimer = null;
  }

  Future<void> disconnect() async {
    _cancelReconnectTimer();
    _wasConnected = false;
    _isReconnecting = false;
    if (_connection != null) {
      try {
        await _connection!.stop();
        if (kDebugMode) debugPrint('SignalR disconnected');
      } catch (e) {
        if (kDebugMode) debugPrint('SignalR disconnect error: $e');
      }
      _connection = null;
    }
  }

  bool get isConnected => _connection?.state == HubConnectionState.Connected;
}

// ⚠️ DEVELOPMENT ONLY: Bypass SSL validation
class _DevHttpOverrides extends HttpOverrides {
  @override
  HttpClient createHttpClient(SecurityContext? context) {
    return super.createHttpClient(context)
      ..badCertificateCallback = (cert, host, port) => true;
  }
}
