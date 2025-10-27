import 'package:flutter/material.dart';
import 'employee_list_screen.dart';
import 'employee_orders_screen.dart';
import 'employee_parts_screen.dart';
import 'employee_partrequests_screen.dart';
import 'employee_imports_screen.dart';
import 'employee_schedule_screen.dart';
import '../widgets/sidebar_menu.dart';

class EmployeeHomeScreen extends StatefulWidget {
	const EmployeeHomeScreen({super.key});

	@override
	State<EmployeeHomeScreen> createState() => _EmployeeHomeScreenState();
}

class _EmployeeHomeScreenState extends State<EmployeeHomeScreen> {
	// current selected id
	String _selectedId = 'employee_list';
	Widget get _current {
		switch (_selectedId) {
			case 'employee_orders':
				return const EmployeeOrdersScreen();
				case 'employee_parts':
					return const EmployeePartsScreen();
				case 'employee_partrequests':
					return const EmployeePartRequestsScreen();
				case 'employee_imports':
					return const EmployeeImportsScreen();
			case 'employee_schedule':
				return const EmployeeScheduleScreen();
			case 'employee_list':
			default:
				return const EmployeeListScreen();
		}
	}

	void _navigateToId(String id) {
		setState(() => _selectedId = id);
		// close drawer if open
		Navigator.of(context).maybePop();
	}

	@override
	Widget build(BuildContext context) {
		return Scaffold(
			appBar: AppBar(
				title: const Text('Employee Dashboard'),
				// AppBar will automatically show hamburger icon when drawer exists
			),
			drawer: Drawer(
				child: SidebarMenu(onNavigate: _navigateToId, selectedId: _selectedId),
			),
			body: AnimatedSwitcher(
				duration: const Duration(milliseconds: 200),
				child: _current,
			),
		);
	}
}
