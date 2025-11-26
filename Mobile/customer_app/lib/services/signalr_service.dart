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
  
  Function()? onLogoutReceived;

  Future<void> connect(String sessionId) async {
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
      _connection!.onclose(({error}) {
        if (kDebugMode) debugPrint('SignalR connection closed: $error');
        // BUGFIX: Notify UI to log out if SignalR is disconnected (server closed or lost connection)
        if (onLogoutReceived != null) {
          onLogoutReceived!();
        }
      });

      _connection!.onreconnecting(({error}) {
        if (kDebugMode) debugPrint('SignalR reconnecting: $error');
      });

      _connection!.onreconnected(({connectionId}) {
        if (kDebugMode) debugPrint('SignalR reconnected: $connectionId');
      });

      // Start connection
      await _connection!.start();
      if (kDebugMode) debugPrint('SignalR connected successfully');
    } catch (e) {
      if (kDebugMode) debugPrint('SignalR connection error: $e');
      // Don't throw - SignalR is not critical for app function
    }
  }

  void _handleLogout(List<Object?>? arguments) {
    if (kDebugMode) debugPrint('SignalR received logout event');
    
    // Call the callback to trigger logout in UI
    if (onLogoutReceived != null) {
      onLogoutReceived!();
    }
  }

  Future<void> disconnect() async {
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
