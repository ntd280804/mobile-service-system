import 'package:flutter/material.dart';
import 'sidebar_item.dart';

/// SidebarMenu now uses string ids for navigation so the parent can track
/// the currently selected id and render the corresponding page.
class SidebarMenu extends StatelessWidget {
	final void Function(String id) onNavigate;
	final String selectedId;

	const SidebarMenu({super.key, required this.onNavigate, required this.selectedId});

	@override
	Widget build(BuildContext context) {
		return SafeArea(
			child: Column(
				crossAxisAlignment: CrossAxisAlignment.stretch,
				children: [
					const DrawerHeader(
						child: Text('Admin', style: TextStyle(fontSize: 20, color: Colors.white)),
						decoration: BoxDecoration(color: Colors.blueAccent),
					),
								SidebarItem(
									icon: Icons.group,
									title: 'Danh sách nhân viên',
									selected: selectedId == 'employee_list',
									onTap: () => onNavigate('employee_list'),
								),
								SidebarItem(
									icon: Icons.inventory,
									title: 'Đơn hàng',
									selected: selectedId == 'employee_orders',
									onTap: () => onNavigate('employee_orders'),
								),
								SidebarItem(
									icon: Icons.build_circle,
									title: 'Linh kiện',
									selected: selectedId == 'employee_parts',
									onTap: () => onNavigate('employee_parts'),
								),
								SidebarItem(
									icon: Icons.request_page,
									title: 'Yêu cầu linh kiện',
									selected: selectedId == 'employee_partrequests',
									onTap: () => onNavigate('employee_partrequests'),
								),
								SidebarItem(
									icon: Icons.inventory_2,
									title: 'Nhập kho',
									selected: selectedId == 'employee_imports',
									onTap: () => onNavigate('employee_imports'),
								),
								SidebarItem(
									icon: Icons.schedule,
									title: 'Lịch',
									selected: selectedId == 'employee_schedule',
									onTap: () => onNavigate('employee_schedule'),
								),
					const Spacer(),
					Padding(
						padding: const EdgeInsets.all(8.0),
						child: Text('v1.0', textAlign: TextAlign.center, style: Theme.of(context).textTheme.bodySmall),
					)
				],
			),
		);
	}
}
