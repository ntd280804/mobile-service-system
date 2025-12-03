import 'package:flutter/material.dart';
import '../theme/app_theme.dart';

/// Status badge/chip matching WebApp status badges
/// Auto-colors based on status text
class StatusChip extends StatelessWidget {
  final String status;
  final StatusType? type;
  final double? fontSize;
  final EdgeInsetsGeometry? padding;

  const StatusChip({
    super.key,
    required this.status,
    this.type,
    this.fontSize,
    this.padding,
  });

  @override
  Widget build(BuildContext context) {
    final statusType = type ?? _detectStatusType(status);
    final colors = _getStatusColors(statusType);

    return Container(
      padding: padding ??
          const EdgeInsets.symmetric(
            horizontal: AppTheme.spacing12,
            vertical: AppTheme.spacing4,
          ),
      decoration: BoxDecoration(
        color: colors.background,
        borderRadius: BorderRadius.circular(AppTheme.radiusSmall),
        border: Border.all(
          color: colors.border,
          width: 1,
        ),
      ),
      child: Text(
        status,
        style: TextStyle(
          color: colors.text,
          fontSize: fontSize ?? 12,
          fontWeight: FontWeight.w600,
        ),
      ),
    );
  }

  StatusType _detectStatusType(String status) {
    final normalizedStatus = status.toLowerCase().trim();

    // Pending states
    if (normalizedStatus.contains('pending') ||
        normalizedStatus.contains('chờ') ||
        normalizedStatus.contains('waiting')) {
      return StatusType.pending;
    }

    // Approved/Active states
    if (normalizedStatus.contains('approved') ||
        normalizedStatus.contains('active') ||
        normalizedStatus.contains('đã duyệt') ||
        normalizedStatus.contains('hoạt động')) {
      return StatusType.approved;
    }

    // Completed/Success states
    if (normalizedStatus.contains('completed') ||
        normalizedStatus.contains('success') ||
        normalizedStatus.contains('paid') ||
        normalizedStatus.contains('hoàn thành') ||
        normalizedStatus.contains('đã thanh toán')) {
      return StatusType.completed;
    }

    // Processing/In progress states
    if (normalizedStatus.contains('processing') ||
        normalizedStatus.contains('in progress') ||
        normalizedStatus.contains('đang xử lý')) {
      return StatusType.processing;
    }

    // Cancelled/Rejected states
    if (normalizedStatus.contains('cancelled') ||
        normalizedStatus.contains('rejected') ||
        normalizedStatus.contains('failed') ||
        normalizedStatus.contains('hủy') ||
        normalizedStatus.contains('từ chối')) {
      return StatusType.cancelled;
    }

    // Default to info
    return StatusType.info;
  }

  _StatusColors _getStatusColors(StatusType type) {
    switch (type) {
      case StatusType.pending:
        return _StatusColors(
          background: AppTheme.warning.withValues(alpha: 0.1),
          border: AppTheme.warning.withValues(alpha: 0.3),
          text: AppTheme.warningDark,
        );
      case StatusType.approved:
        return _StatusColors(
          background: AppTheme.success.withValues(alpha: 0.1),
          border: AppTheme.success.withValues(alpha: 0.3),
          text: AppTheme.successDark,
        );
      case StatusType.completed:
        return _StatusColors(
          background: AppTheme.primary.withValues(alpha: 0.1),
          border: AppTheme.primary.withValues(alpha: 0.3),
          text: AppTheme.primaryDark,
        );
      case StatusType.processing:
        return _StatusColors(
          background: AppTheme.info.withValues(alpha: 0.1),
          border: AppTheme.info.withValues(alpha: 0.3),
          text: const Color(0xFF0891B2), // cyan-600
        );
      case StatusType.cancelled:
        return _StatusColors(
          background: AppTheme.error.withValues(alpha: 0.1),
          border: AppTheme.error.withValues(alpha: 0.3),
          text: AppTheme.errorDark,
        );
      case StatusType.info:
        return _StatusColors(
          background: AppTheme.textSecondary.withValues(alpha: 0.1),
          border: AppTheme.textSecondary.withValues(alpha: 0.3),
          text: AppTheme.textSecondary,
        );
    }
  }
}

enum StatusType {
  pending,
  approved,
  completed,
  processing,
  cancelled,
  info,
}

class _StatusColors {
  final Color background;
  final Color border;
  final Color text;

  _StatusColors({
    required this.background,
    required this.border,
    required this.text,
  });
}
