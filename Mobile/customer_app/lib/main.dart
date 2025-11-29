import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import 'screens/home_screen.dart';
import 'screens/login_screen.dart';
import 'screens/register_screen.dart';
import 'screens/order_detail_screen.dart';
import 'screens/appointments_screen.dart';
import 'screens/change_password_screen.dart';
import 'screens/employee_import_detail_screen.dart';
import 'screens/employee_export_detail_screen.dart';
import 'screens/employee_invoice_detail_screen.dart';
import 'theme/app_theme.dart';
import 'providers/auth_provider.dart';
import 'providers/orders_provider.dart';
import 'providers/appointments_provider.dart';

void main() {
  WidgetsFlutterBinding.ensureInitialized();
  runApp(const MyApp());
}

class MyApp extends StatelessWidget {
  const MyApp({super.key});

  @override
  Widget build(BuildContext context) {
    return MultiProvider(
      providers: [
        ChangeNotifierProvider(create: (_) => AuthProvider()),
        ChangeNotifierProvider(create: (_) => OrdersProvider()),
        ChangeNotifierProvider(create: (_) => AppointmentsProvider()),
      ],
      child: MaterialApp(
        title: 'Mobile Service System',
        debugShowCheckedModeBanner: false,
        theme: AppTheme.lightTheme,
        routes: {
          '/login': (_) => const LoginScreen(),
          '/home': (_) => const HomeScreen(),
          '/register': (_) => const RegisterScreen(),
          '/order/detail': (context) {
            final orderId = ModalRoute.of(context)!.settings.arguments as int;
            return OrderDetailScreen(orderId: orderId);
          },
          '/appointments': (_) => const AppointmentsScreen(),
          '/change-password': (_) => const ChangePasswordScreen(),
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
      ),
    );
  }
}

class _InitialRouteDecider extends StatefulWidget {
  const _InitialRouteDecider();

  @override
  State<_InitialRouteDecider> createState() => _InitialRouteDeciderState();
}

class _InitialRouteDeciderState extends State<_InitialRouteDecider> {
  bool _isInitializing = true;

  @override
  void initState() {
    super.initState();
    _initializeAuth();
  }

  Future<void> _initializeAuth() async {
    // Initialize auth provider from storage
    final authProvider = context.read<AuthProvider>();
    await authProvider.initialize();
    if (mounted) {
      setState(() {
        _isInitializing = false;
      });
    }
  }

  @override
  Widget build(BuildContext context) {
    // Show loading while initializing
    if (_isInitializing) {
      return const Scaffold(
        body: Center(child: CircularProgressIndicator()),
      );
    }

    return Consumer<AuthProvider>(
      builder: (context, authProvider, _) {
        // Navigate based on auth state
        if (authProvider.isAuthenticated) {
          return const HomeScreen();
        }

        return const LoginScreen();
      },
    );
  }
}
