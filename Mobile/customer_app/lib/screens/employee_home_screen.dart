import 'package:flutter/material.dart';
import 'employee_dashboard_screen.dart';
import 'employee_appointment_list_screen.dart';
import 'employee_order_list_screen.dart';
import 'employee_import_list_screen.dart';
import 'employee_export_list_screen.dart';
import 'employee_invoice_list_screen.dart';

class EmployeeHomeScreen extends StatefulWidget {
  const EmployeeHomeScreen({super.key});

  @override
  State<EmployeeHomeScreen> createState() => _EmployeeHomeScreenState();
}

class _EmployeeHomeScreenState extends State<EmployeeHomeScreen> {
  int _currentIndex = 0;

  final List<Widget> _screens = [
    const EmployeeDashboardScreen(),
    const EmployeeAppointmentListScreen(),
    const EmployeeOrderListScreen(),
    const EmployeeImportListScreen(),
    const EmployeeExportListScreen(),
    const EmployeeInvoiceListScreen(),
  ];

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      body: _screens[_currentIndex],
      bottomNavigationBar: NavigationBar(
        selectedIndex: _currentIndex,
        onDestinationSelected: (index) {
          setState(() {
            _currentIndex = index;
          });
        },
        destinations: const [
          NavigationDestination(
            icon: Icon(Icons.dashboard_outlined),
            selectedIcon: Icon(Icons.dashboard),
            label: 'Dashboard',
          ),
          NavigationDestination(
            icon: Icon(Icons.calendar_today_outlined),
            selectedIcon: Icon(Icons.calendar_today),
            label: 'Lịch hẹn',
          ),
          NavigationDestination(
            icon: Icon(Icons.shopping_cart_outlined),
            selectedIcon: Icon(Icons.shopping_cart),
            label: 'Đơn hàng',
          ),
          NavigationDestination(
            icon: Icon(Icons.arrow_downward_outlined),
            selectedIcon: Icon(Icons.arrow_downward),
            label: 'Nhập',
          ),
          NavigationDestination(
            icon: Icon(Icons.arrow_upward_outlined),
            selectedIcon: Icon(Icons.arrow_upward),
            label: 'Xuất',
          ),
          NavigationDestination(
            icon: Icon(Icons.receipt_outlined),
            selectedIcon: Icon(Icons.receipt),
            label: 'Invoice',
          ),
        ],
      ),
    );
  }
}

