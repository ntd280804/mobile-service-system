import 'package:flutter/material.dart';

import 'screens/home_screen.dart';
import 'screens/login_screen.dart';
import 'screens/register_screen.dart';
import 'screens/employee_import_detail_screen.dart';
import 'screens/employee_export_detail_screen.dart';
import 'screens/employee_invoice_detail_screen.dart';
import 'services/storage_service.dart';

void main() {
  WidgetsFlutterBinding.ensureInitialized();
  runApp(const MyApp());
}

class MyApp extends StatelessWidget {
  const MyApp({super.key});

  @override
  Widget build(BuildContext context) {
    return MaterialApp(
      title: 'Mobile Service',
      theme: ThemeData(
        colorScheme: ColorScheme.fromSeed(seedColor: Colors.blue),
        useMaterial3: true,
      ),
      routes: {
        '/login': (_) => const LoginScreen(),
        '/home': (_) => const HomeScreen(),
        '/register': (_) => const RegisterScreen(),
        '/employee/import/detail': (context) {
          final stockInId = ModalRoute.of(context)!.settings.arguments as int;
          return EmployeeImportDetailScreen(stockInId: stockInId);
        },
        '/employee/export/detail': (context) {
          final stockOutId = ModalRoute.of(context)!.settings.arguments as int;
          return EmployeeExportDetailScreen(stockOutId: stockOutId);
        },
        '/employee/invoice/detail': (context) {
          final invoiceId = ModalRoute.of(context)!.settings.arguments as int;
          return EmployeeInvoiceDetailScreen(invoiceId: invoiceId);
        },
      },
      home: const _InitialRouteDecider(),
    );
  }
}

class _InitialRouteDecider extends StatefulWidget {
  const _InitialRouteDecider();

  @override
  State<_InitialRouteDecider> createState() => _InitialRouteDeciderState();
}

class _InitialRouteDeciderState extends State<_InitialRouteDecider> {
  final _storage = StorageService();
  Future<bool>? _hasToken;

  @override
  void initState() {
    super.initState();
    _hasToken = _storage.getToken().then((t) => t != null && t.isNotEmpty);
  }

  @override
  Widget build(BuildContext context) {
    return FutureBuilder<bool>(
      future: _hasToken,
      builder: (context, snapshot) {
        if (!snapshot.hasData) {
          return const Scaffold(
            body: Center(child: CircularProgressIndicator()),
          );
        }
        if (snapshot.data == true) {
          return const HomeScreen();
        }
        return const LoginScreen();
      },
    );
  }
}
