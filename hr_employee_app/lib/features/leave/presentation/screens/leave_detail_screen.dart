import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:hr_employee_app/app/theme/app_spacing.dart';
import 'package:hr_employee_app/app/theme/app_theme.dart';
import 'package:hr_employee_app/core/extensions/string_extensions.dart';
import 'package:hr_employee_app/core/utils/color_utils.dart';
import 'package:hr_employee_app/core/utils/date_formatter.dart';
import 'package:hr_employee_app/core/widgets/app_button.dart';
import 'package:hr_employee_app/core/widgets/app_snackbar.dart';
import 'package:hr_employee_app/core/widgets/async_value_view.dart';
import 'package:hr_employee_app/core/widgets/confirm_dialog.dart';
import 'package:hr_employee_app/features/leave/data/models/leave_enum_mappers.dart';
import 'package:hr_employee_app/features/leave/domain/entities/leave_request.dart';
import 'package:hr_employee_app/features/leave/domain/entities/leave_status.dart';
import 'package:hr_employee_app/features/leave/presentation/providers/leave_controllers.dart';
import 'package:hr_employee_app/features/leave/presentation/widgets/leave_status_chip.dart';
import 'package:hr_employee_app/shared/helpers/currency_formatter.dart';
import 'package:hr_employee_app/shared/models/submission_state.dart';
import 'package:material_ui/material_ui.dart';

/// One leave request in full: what was asked for, how it has progressed,
/// what was attached, and — while it is pending — a way to withdraw it.
class LeaveDetailScreen extends ConsumerWidget {
  const LeaveDetailScreen({required this.leaveRequestId, super.key});

  final String leaveRequestId;

  /// How far the summary card rides up over the header's foot.
  static const double _overlap = 32;

  Future<void> _cancel(
    BuildContext context,
    WidgetRef ref,
    LeaveRequest request,
  ) async {
    final bool confirmed = await ConfirmDialog.show(
      context,
      title: 'Withdraw this request?',
      message:
          'Your ${request.leaveTypeName} request for '
          '${DateFormatter.dateRange(request.startDate, request.endDate)} '
          'will be withdrawn. You can apply again if you need to.',
      confirmLabel: 'Withdraw',
      isDestructive: true,
      icon: Icons.event_busy_outlined,
    );

    if (!confirmed || !context.mounted) {
      return;
    }

    final bool succeeded = await ref
        .read(cancelLeaveControllerProvider.notifier)
        .cancel(request.id, current: request);

    if (!context.mounted) {
      return;
    }

    if (succeeded) {
      AppSnackBar.success(context, 'Leave request withdrawn.');
      return;
    }
    final SubmissionState state = ref.read(cancelLeaveControllerProvider);
    if (state.failureOrNull != null) {
      AppSnackBar.failure(context, state.failureOrNull!);
    }
  }

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final AsyncValue<LeaveRequest> request = ref.watch(
      leaveRequestDetailProvider(leaveRequestId),
    );
    final bool isCancelling = ref
        .watch(cancelLeaveControllerProvider)
        .isSubmitting;

    return AnnotatedRegion<SystemUiOverlayStyle>(
      value: SystemUiOverlayStyle.light,
      child: Scaffold(
        body: AsyncValueView<LeaveRequest>(
          value: request,
          loadingMessages: const <String>[
            'Fetching the request…',
            'Checking its progress…',
            'Almost there…',
          ],
          onRetry: () =>
              ref.invalidate(leaveRequestDetailProvider(leaveRequestId)),
          data: (LeaveRequest data) => ListView(
            padding: EdgeInsets.zero,
            children: <Widget>[
              _Header(status: data.status),
              Transform.translate(
                offset: const Offset(0, -_overlap),
                child: Padding(
                  padding: EdgeInsets.symmetric(horizontal: AppSpacing.lg),
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.stretch,
                    children: <Widget>[
                      _SummaryCard(request: data),
                      SizedBox(height: AppSpacing.md),
                      _ProgressCard(request: data),
                      if (data.hasAttachments) ...<Widget>[
                        SizedBox(height: AppSpacing.md),
                        _AttachmentsCard(request: data),
                      ],
                      if (data.isCancellable) ...<Widget>[
                        SizedBox(height: AppSpacing.xl),
                        AppButton.danger(
                          label: 'Withdraw request',
                          loadingLabel: 'Withdrawing…',
                          icon: Icons.close_rounded,
                          isLoading: isCancelling,
                          onPressed: () => _cancel(context, ref, data),
                        ),
                      ],
                      SizedBox(height: AppSpacing.lg),
                    ],
                  ),
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

/// Navy band: back, the title, and the status as a glass pill.
class _Header extends StatelessWidget {
  const _Header({required this.status});

  final LeaveStatus status;

  @override
  Widget build(BuildContext context) {
    const Color onBrand = AppColors.textOnPrimary;
    final TextTheme textStyles = Theme.of(context).textTheme;

    return ColoredBox(
      color: AppColors.apexNavy,
      child: SafeArea(
        bottom: false,
        child: Padding(
          padding: EdgeInsets.fromLTRB(
            AppSpacing.lg,
            AppSpacing.md,
            AppSpacing.lg,
            AppSpacing.lg + LeaveDetailScreen._overlap,
          ),
          child: Row(
            children: <Widget>[
              if (Navigator.of(context).canPop()) ...<Widget>[
                Material(
                  color: onBrand.withValues(alpha: 0.12),
                  shape: CircleBorder(
                    side: BorderSide(color: onBrand.withValues(alpha: 0.16)),
                  ),
                  clipBehavior: Clip.antiAlias,
                  child: IconButton(
                    onPressed: () => Navigator.of(context).maybePop(),
                    icon: const Icon(Icons.arrow_back_rounded, color: onBrand),
                    iconSize: AppSizes.iconMd,
                    tooltip: MaterialLocalizations.of(context)
                        .backButtonTooltip,
                  ),
                ),
                Gap.wMd,
              ],
              Expanded(
                child: Text(
                  'Leave request',
                  style: textStyles.titleLarge?.copyWith(
                    color: onBrand,
                    fontWeight: FontWeight.w800,
                  ),
                ),
              ),
              _StatusPill(status: status),
            ],
          ),
        ),
      ),
    );
  }
}

/// The status in its own colour on navy glass.
class _StatusPill extends StatelessWidget {
  const _StatusPill({required this.status});

  final LeaveStatus status;

  @override
  Widget build(BuildContext context) {
    final Color tone = _toneColour(context, status);
    final IconData? icon = LeaveStatusChip.iconFor(status);

    return Container(
      padding: EdgeInsets.symmetric(
        horizontal: AppSpacing.md,
        vertical: AppSpacing.xs + 1,
      ),
      decoration: BoxDecoration(
        color: tone.withValues(alpha: 0.18),
        borderRadius: BorderRadius.circular(AppRadius.pill),
        border: Border.all(color: tone.withValues(alpha: 0.35)),
      ),
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: <Widget>[
          if (icon != null) ...<Widget>[
            Icon(icon, size: 14, color: _onNavy(tone)),
            Gap.wXs,
          ],
          Text(
            status.label,
            style: Theme.of(context).textTheme.labelMedium
                ?.copyWith(color: _onNavy(tone), fontWeight: FontWeight.w700),
          ),
        ],
      ),
    );
  }

  /// Lifted toward white so the tone stays legible on navy.
  static Color _onNavy(Color tone) =>
      Color.lerp(tone, AppColors.textOnPrimary, 0.35)!;
}

Color _toneColour(BuildContext context, LeaveStatus status) {
  final AppPalette palette = context.palette;
  return switch (status) {
    LeaveStatus.approved => palette.success,
    LeaveStatus.pending => palette.warning,
    LeaveStatus.rejected => palette.danger,
    LeaveStatus.cancelled || LeaveStatus.unknown => palette.textTertiary,
  };
}

/// `Mon, 3 Nov`.
String _shortDay(DateTime date) => DateFormatter.weekdayDayMonth(date);

/// `Sun, 2 Nov 2026`.
String _shortDayYear(DateTime date) =>
    '${DateFormatter.weekdayDayMonth(date)} ${date.year}';

/// White, softly raised card.
class _Card extends StatelessWidget {
  const _Card({required this.child});

  final Widget child;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: EdgeInsets.all(AppSpacing.lg),
      decoration: BoxDecoration(
        color: AppColors.surface,
        borderRadius: BorderRadius.circular(AppRadius.xl),
        boxShadow: const <BoxShadow>[
          BoxShadow(
            color: AppColors.shadowLift,
            blurRadius: 18,
            offset: Offset(0, 5),
          ),
        ],
      ),
      child: child,
    );
  }
}

/// Type, dates and reason.
class _SummaryCard extends StatelessWidget {
  const _SummaryCard({required this.request});

  final LeaveRequest request;

  @override
  Widget build(BuildContext context) {
    final AppPalette palette = context.palette;
    final TextTheme textStyles = Theme.of(context).textTheme;
    final Color accent = ColorUtils.fromHex(
      request.colorHex,
      fallback: AppColors.summitTeal,
    );

    return _Card(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: <Widget>[
          Row(
            children: <Widget>[
              Container(
                height: 40,
                width: 40,
                decoration: BoxDecoration(
                  color: accent.withValues(alpha: 0.14),
                  borderRadius: BorderRadius.circular(AppRadius.md),
                ),
                child: Icon(
                  Icons.event_note_outlined,
                  size: AppSizes.iconMd,
                  color: accent,
                ),
              ),
              Gap.wMd,
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: <Widget>[
                    Text(
                      request.leaveTypeName,
                      style: textStyles.titleMedium?.copyWith(
                        fontWeight: FontWeight.w800,
                      ),
                    ),
                    Text(
                      request.dayPortion.label,
                      style: textStyles.bodySmall?.copyWith(
                        color: palette.textTertiary,
                      ),
                    ),
                  ],
                ),
              ),
            ],
          ),
          Gap.md,
          Container(
            padding: EdgeInsets.all(AppSpacing.md),
            decoration: BoxDecoration(
              color: AppColors.cloud,
              borderRadius: AppRadius.field,
              border: Border.all(color: palette.border),
            ),
            child: Row(
              children: <Widget>[
                Expanded(
                  child: _DateEnd(
                    caption: request.isSingleDay ? 'ON' : 'FROM',
                    date: request.startDate,
                  ),
                ),
                Container(
                  padding: EdgeInsets.symmetric(
                    horizontal: AppSpacing.md,
                    vertical: AppSpacing.xs,
                  ),
                  decoration: BoxDecoration(
                    color: AppColors.apexNavy,
                    borderRadius: BorderRadius.circular(AppRadius.pill),
                  ),
                  child: Text(
                    DayCountFormatter.format(request.totalDays),
                    style: textStyles.labelMedium?.copyWith(
                      color: AppColors.textOnPrimary,
                      fontWeight: FontWeight.w700,
                    ),
                  ),
                ),
                Expanded(
                  child: request.isSingleDay
                      ? const SizedBox.shrink()
                      : _DateEnd(
                          caption: 'TO',
                          date: request.endDate,
                          alignEnd: true,
                        ),
                ),
              ],
            ),
          ),
          if (request.reason.isNotBlank) ...<Widget>[
            Gap.md,
            Text(
              'Reason',
              style: textStyles.labelSmall?.copyWith(
                color: palette.textTertiary,
              ),
            ),
            Gap.xxs,
            Text(request.reason, style: textStyles.bodyMedium),
          ],
          if (request.contactDuringLeave.isNotNullOrBlank) ...<Widget>[
            Gap.md,
            Row(
              children: <Widget>[
                Icon(
                  Icons.phone_outlined,
                  size: AppSizes.iconSm,
                  color: palette.textTertiary,
                ),
                Gap.wXs,
                Expanded(
                  child: Text(
                    'Reachable on ${request.contactDuringLeave}',
                    style: textStyles.bodySmall?.copyWith(
                      color: palette.textSecondary,
                    ),
                  ),
                ),
              ],
            ),
          ],
        ],
      ),
    );
  }
}

class _DateEnd extends StatelessWidget {
  const _DateEnd({
    required this.caption,
    required this.date,
    this.alignEnd = false,
  });

  final String caption;
  final DateTime date;
  final bool alignEnd;

  @override
  Widget build(BuildContext context) {
    final TextTheme textStyles = Theme.of(context).textTheme;

    return Column(
      crossAxisAlignment: alignEnd
          ? CrossAxisAlignment.end
          : CrossAxisAlignment.start,
      children: <Widget>[
        Text(
          caption,
          style: textStyles.labelSmall?.copyWith(
            fontSize: 9,
            letterSpacing: 0.8,
            fontWeight: FontWeight.w700,
            color: context.palette.textTertiary,
          ),
        ),
        Text(
          _shortDay(date),
          style: textStyles.titleSmall?.copyWith(fontWeight: FontWeight.w800),
        ),
      ],
    );
  }
}

/// Submitted, then the decision — or where the request is waiting.
class _ProgressCard extends StatelessWidget {
  const _ProgressCard({required this.request});

  final LeaveRequest request;

  @override
  Widget build(BuildContext context) {
    final AppPalette palette = context.palette;
    final Color tone = _toneColour(context, request.status);
    final String? approver = request.approverName.isNotNullOrBlank
        ? request.approverName
        : null;

    final (
      String title,
      IconData icon,
      String? meta,
    ) = switch (request.status) {
      LeaveStatus.approved => (
        'Approved',
        Icons.check_rounded,
        _decidedMeta(request, approver),
      ),
      LeaveStatus.rejected => (
        'Rejected',
        Icons.close_rounded,
        _decidedMeta(request, approver),
      ),
      LeaveStatus.pending => (
        'Awaiting approval',
        Icons.schedule_rounded,
        approver == null ? 'With your manager' : 'With $approver',
      ),
      LeaveStatus.cancelled => (
        'Withdrawn',
        Icons.block_rounded,
        request.decidedAt == null ? null : _shortDayYear(request.decidedAt!),
      ),
      LeaveStatus.unknown => ('Status unknown', Icons.help_outline, null),
    };

    return _Card(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: <Widget>[
          Text(
            'Progress',
            style: Theme.of(context).textTheme.titleSmall
                ?.copyWith(fontWeight: FontWeight.w800),
          ),
          Gap.md,
          _Step(
            icon: Icons.send_rounded,
            tone: palette.textTertiary,
            isMuted: true,
            title: 'Submitted',
            meta: '${_shortDayYear(request.appliedAt)} · by you',
            hasLineBelow: true,
          ),
          _Step(
            icon: icon,
            tone: tone,
            isMuted: request.status == LeaveStatus.pending,
            title: title,
            meta: meta,
            remark: request.decisionRemark.isNotNullOrBlank
                ? request.decisionRemark
                : null,
            remarkAuthor: approver,
          ),
        ],
      ),
    );
  }

  static String? _decidedMeta(LeaveRequest request, String? approver) {
    final List<String> parts = <String>[
      if (request.decidedAt != null) _shortDayYear(request.decidedAt!),
      if (approver != null) 'by $approver',
    ];
    return parts.isEmpty ? null : parts.join(' · ');
  }
}

/// One step on the progress line, optionally with the approver's remark.
class _Step extends StatelessWidget {
  const _Step({
    required this.icon,
    required this.tone,
    required this.title,
    this.meta,
    this.isMuted = false,
    this.hasLineBelow = false,
    this.remark,
    this.remarkAuthor,
  });

  final IconData icon;
  final Color tone;
  final String title;
  final String? meta;

  /// A light tint rather than a solid dot: submitted, or still waiting.
  final bool isMuted;

  final bool hasLineBelow;
  final String? remark;
  final String? remarkAuthor;

  @override
  Widget build(BuildContext context) {
    final AppPalette palette = context.palette;
    final TextTheme textStyles = Theme.of(context).textTheme;

    return IntrinsicHeight(
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: <Widget>[
          SizedBox(
            width: 24,
            child: Column(
              children: <Widget>[
                Container(
                  height: 24,
                  width: 24,
                  decoration: BoxDecoration(
                    shape: BoxShape.circle,
                    color: isMuted ? tone.withValues(alpha: 0.16) : tone,
                  ),
                  child: Icon(
                    icon,
                    size: 13,
                    color: isMuted ? tone : AppColors.textOnPrimary,
                  ),
                ),
                if (hasLineBelow)
                  Expanded(
                    child: Container(
                      width: 2,
                      margin: const EdgeInsets.symmetric(vertical: 3),
                      color: palette.border,
                    ),
                  ),
              ],
            ),
          ),
          Gap.wMd,
          Expanded(
            child: Padding(
              padding: EdgeInsets.only(
                bottom: hasLineBelow ? AppSpacing.md : 0,
              ),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: <Widget>[
                  Text(
                    title,
                    style: textStyles.labelLarge?.copyWith(
                      fontWeight: FontWeight.w800,
                    ),
                  ),
                  if (meta != null)
                    Text(
                      meta!,
                      style: textStyles.bodySmall?.copyWith(
                        color: palette.textTertiary,
                      ),
                    ),
                  if (remark != null) ...<Widget>[
                    Gap.sm,
                    _RemarkBubble(
                      text: remark!,
                      author: remarkAuthor,
                      tone: tone,
                    ),
                  ],
                ],
              ),
            ),
          ),
        ],
      ),
    );
  }
}

/// The approver's note, as a speech bubble beside their initials.
class _RemarkBubble extends StatelessWidget {
  const _RemarkBubble({
    required this.text,
    required this.author,
    required this.tone,
  });

  final String text;
  final String? author;
  final Color tone;

  @override
  Widget build(BuildContext context) {
    final TextTheme textStyles = Theme.of(context).textTheme;

    return Row(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: <Widget>[
        if (author != null) ...<Widget>[
          Container(
            height: 28,
            width: 28,
            alignment: Alignment.center,
            decoration: BoxDecoration(
              color: context.palette.surfaceMuted,
              shape: BoxShape.circle,
            ),
            child: Text(
              _initials(author!),
              style: textStyles.labelSmall?.copyWith(
                fontSize: 10,
                fontWeight: FontWeight.w800,
                color: context.palette.textSecondary,
              ),
            ),
          ),
          Gap.wSm,
        ],
        Flexible(
          child: Container(
            padding: EdgeInsets.symmetric(
              horizontal: AppSpacing.md,
              vertical: AppSpacing.sm,
            ),
            decoration: BoxDecoration(
              color: tone.withValues(alpha: 0.10),
              borderRadius: const BorderRadius.only(
                topRight: Radius.circular(12),
                bottomLeft: Radius.circular(12),
                bottomRight: Radius.circular(12),
                topLeft: Radius.circular(4),
              ),
            ),
            child: Text(
              text,
              style: textStyles.bodySmall?.copyWith(
                color: Color.lerp(tone, Colors.black, 0.35),
              ),
            ),
          ),
        ),
      ],
    );
  }

  /// `Ma Myat Gyi` → `MG`: the honorific is skipped where there is one.
  static String _initials(String name) {
    const Set<String> honorifics = <String>{'u', 'daw', 'ma', 'ko', 'mg'};
    final List<String> parts = name
        .trim()
        .split(RegExp(r'\s+'))
        .where((String p) => p.isNotEmpty)
        .toList();
    final List<String> named = parts.length > 2
        ? parts
              .where((String p) => !honorifics.contains(p.toLowerCase()))
              .toList()
        : parts;
    final List<String> use = named.isEmpty ? parts : named;
    if (use.isEmpty) {
      return '?';
    }
    final String first = use.first[0];
    final String last = use.length > 1 ? use.last[0] : '';
    return (first + last).toUpperCase();
  }
}

/// The files submitted with the request.
///
/// Downloading is not offered yet: the contract exposes `downloadUrl` but the
/// endpoint is unconfirmed (`docs/api_contracts.md` §5), and an action that
/// might fail is worse than one that is plainly absent. The rows show what
/// was submitted, which is what the employee needs to confirm.
class _AttachmentsCard extends StatelessWidget {
  const _AttachmentsCard({required this.request});

  final LeaveRequest request;

  @override
  Widget build(BuildContext context) {
    final AppPalette palette = context.palette;
    final TextTheme textStyles = Theme.of(context).textTheme;
    final int count = request.attachments.length;

    return _Card(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: <Widget>[
          Row(
            children: <Widget>[
              Expanded(
                child: Text(
                  'Attachments',
                  style: textStyles.titleSmall?.copyWith(
                    fontWeight: FontWeight.w800,
                  ),
                ),
              ),
              Text(
                count == 1 ? '1 file' : '$count files',
                style: textStyles.labelSmall?.copyWith(
                  color: palette.textTertiary,
                ),
              ),
            ],
          ),
          Gap.md,
          for (int i = 0; i < count; i++) ...<Widget>[
            _AttachmentRow(attachment: request.attachments[i]),
            if (i < count - 1) Gap.sm,
          ],
        ],
      ),
    );
  }
}

class _AttachmentRow extends StatelessWidget {
  const _AttachmentRow({required this.attachment});

  final LeaveAttachment attachment;

  @override
  Widget build(BuildContext context) {
    final AppPalette palette = context.palette;
    final TextTheme textStyles = Theme.of(context).textTheme;
    final bool isPdf = attachment.isPdf;
    final Color tone = isPdf ? palette.danger : palette.info;

    return Container(
      padding: EdgeInsets.all(AppSpacing.sm + 2),
      decoration: BoxDecoration(
        color: AppColors.cloud,
        borderRadius: AppRadius.field,
        border: Border.all(color: palette.border),
      ),
      child: Row(
        children: <Widget>[
          Container(
            height: 36,
            width: 36,
            alignment: Alignment.center,
            decoration: BoxDecoration(
              color: tone.withValues(alpha: 0.12),
              borderRadius: BorderRadius.circular(AppRadius.sm),
            ),
            child: Text(
              isPdf ? 'PDF' : 'IMG',
              style: textStyles.labelSmall?.copyWith(
                fontSize: 9,
                fontWeight: FontWeight.w800,
                color: tone,
              ),
            ),
          ),
          Gap.wMd,
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: <Widget>[
                Text(
                  attachment.fileName,
                  style: textStyles.labelLarge?.copyWith(
                    fontWeight: FontWeight.w700,
                  ),
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                ),
                if (attachment.readableSize.isNotEmpty)
                  Text(
                    attachment.readableSize,
                    style: textStyles.labelSmall?.copyWith(
                      color: palette.textTertiary,
                    ),
                  ),
              ],
            ),
          ),
        ],
      ),
    );
  }
}
