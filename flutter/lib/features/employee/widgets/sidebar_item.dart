import 'package:flutter/material.dart';

class SidebarItem extends StatelessWidget {
	final IconData icon;
	final String title;
	final bool selected;
	final VoidCallback onTap;

	const SidebarItem({
		super.key,
		required this.icon,
		required this.title,
		required this.onTap,
		this.selected = false,
	});

	@override
	Widget build(BuildContext context) {
		return ListTile(
			selected: selected,
			selectedTileColor: Colors.blue.withOpacity(0.08),
			leading: Icon(icon, color: selected ? Colors.blueAccent : Colors.black54),
			title: Text(title, style: TextStyle(color: selected ? Colors.blueAccent : Colors.black87)),
			onTap: onTap,
		);
	}
}
