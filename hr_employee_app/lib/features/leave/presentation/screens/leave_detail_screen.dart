import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:hr_employee_app/app/theme/app_spacing.dart';
import 'package:hr_employee_app/app/theme/app_theme.dart';
import 'package:hr_employee_app/core/extensions/string_extensions.dart';
import 'package:hr_employee_app/core/utils/color_utils.dart';
import 'package:hr_employee_app/core/utils/date_formatter.dart';
import 'package:hr_employee_app/core/widgets/app_button.dart';
import 'package:hr_employee_app/core/widgets/app_card.dart';
import 'package:hr_employee_app/core/widgets/app_snackbar.dart';
import 'package:hr_employee_app/core/widgets/async_value_view.dart';
import 'package:hr_employee_app/core/widgets/brand_app_bar.dart';
import 'package:hr_employee_app/core/widgets/confirm_dialog.dart';
import 'package:hr_employee_app/features/leave/data/models/leave_enum_mappers.dart';
import 'package:hr_employee_app/features/leave/domain/entities/leave_request.dart';
import 'package:hr_employee_app/features/leave/domain/entities/leave_status.dart';
import 'package:hr_employee_app/features/leave/presentation/providers/leave_controllers.dart';
import 'package:hr_employee_app/features/leave/presentation/widgets/leave_status_chip.dart';
import 'package:hr_employee_app/shared/helpers/currency_formatter.dart';
import 'package:hr_employee_app/shared/models/submission_state.dart';
import 'package:material_ui/material_ui.dart';

/// One leave request in full, with the option to withdraw it.
class LeaveDetailScreen extends ConsumerWidget {
  const LeaveDetailScreen({required this.leaveRequestId, super.key});

  final String leaveRequestId;

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

    return Scaffold(
      appBar: const BrandAppBar(title: Text('Leave request')),
      body: SafeArea(
        child: AsyncValueView<LeaveRequest>(
          value: request,
          onRetry: () =>
              ref.invalidate(leaveRequestDetailProvider(leaveRequestId)),
          data: (LeaveRequest data) => ListView(
            padding: AppSpacing.screenPadding,
            children: <Widget>[
              _SummaryCard(request: data),
              SizedBox(height: AppSpacing.cardGap),
              _DetailsCard(request: data),
              if (data.status.isDecided) ...<Widget>[
                SizedBox(height: AppSpacing.cardGap),
                _DecisionCard(request: data),
              ],
              if (data.hasAttachments) ...<Widget>[
                SizedBox(height: AppSpacing.cardGap),
                _AttachmentsCard(request: data),
              ],
              if (data.isCancellable) ...<Widget>[
                SizedBox(height: AppSpacing.xxl),
                AppButton.danger(
                  label: 'Withdraw request',
                  icon: Icons.close_rounded,
                  isLoading: isCancelling,
                  onPressed: () => _cancel(context, ref, data),
                ),
              ],
            ],
          ),
        ),
      ),
    );
  }
}

class _SummaryCard extends StatelessWidget {
  const _SummaryCard({required this.request});

  final LeaveRequest request;

  @override
  Widget build(BuildContext context) {
    final TextTheme textStyles = Theme.of(context).textTheme;
    final Color accent = ColorUtils.fromHex(
      request.colorHex,
      fallback: Theme.of(context).colorScheme.primary,
    );

    return AppCard(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          Row(
            children: <Widget>[
              Container(
                padding: EdgeInsets.all(AppSpacing.sm),
                decoration: BoxDecoration(
                  color: ColorUtils.surfaceTint(
                    accent,
                    isDark: Theme.of(context).brightness == Brightness.dark,
                  ),
                  borderRadius: AppRadius.field,
                ),
                child: Icon(
                  Icons.event_available_outlined,
                  color: accent,
                  size: AppSizes.iconLg,
                ),
              ),
              Gap.wMd,
              Expanded(
                child: Text(
                  request.leaveTypeName,
                  style: textStyles.titleLarge,
                ),
              ),
              LeaveStatusChip(status: request.status),
            ],
          ),
          Gap.lg,
          Text(
            DateFormatter.dateRange(request.startDate, request.endDate),
            style: textStyles.titleMedium,
          ),
          Gap.xxs,
          Text(
            '${DayCountFormatter.format(request.totalDays)}'
            '${request.dayPortion == LeaveDayPortion.fullDay ? '' : ' (${request.dayPortion.label})'}',
            style: textStyles.bodySmall,
          ),
        ],
      ),
    );
  }
}

class _DetailsCard extends StatelessWidget {
  const _DetailsCard({required this.request});

  final LeaveRequest request;

  @override
  Widget build(BuildContext context) {
    return AppSectionCard(
      title: 'Request details',
      icon: Icons.description_outlined,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          AppInfoRow(label: 'Reason', value: request.reason, isMultiline: true),
          Divider(height: AppSpacing.lg, color: context.palette.border),
          AppInfoRow(
            label: 'Applied on',
            value: DateFormatter.dateTime(request.appliedAt),
          ),
          if (request.contactDuringLeave.isNotNullOrBlank)
            AppInfoRow(
              label: 'Contact during leave',
              value: request.contactDuringLeave!,
              icon: Icons.phone_outlined,
            ),
        ],
      ),
    );
  }
}

class _DecisionCard extends StatelessWidget {
  const _DecisionCard({required this.request});

  final LeaveRequest request;

  @override
  Widget build(BuildContext context) {
    return AppSectionCard(
      title: 'Decision',
      icon: Icons.how_to_reg_outlined,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          AppInfoRow(label: 'Status', value: request.status.label),
          if (request.approverName.isNotNullOrBlank)
            AppInfoRow(label: 'Decided by', value: request.approverName!),
          if (request.decidedAt != null)
            AppInfoRow(
              label: 'Decided on',
              value: DateFormatter.dateTime(request.decidedAt!),
            ),
          if (request.decisionRemark.isNotNullOrBlank) ...<Widget>[
            Divider(height: AppSpacing.lg, color: context.palette.border),
            AppInfoRow(
              label: 'Remark',
              value: request.decisionRemark!,
              isMultiline: true,
            ),
          ],
        ],
      ),
    );
  }
}

/// Lists the attached files.
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

    return AppSectionCard(
      title: 'Attachments',
      icon: Icons.attach_file_rounded,
      child: Column(
        children: <Widget>[
          for (final LeaveAttachment attachment in request.attachments)
            Padding(
              padding: EdgeInsets.only(bottom: AppSpacing.sm),
              child: Row(
                children: <Widget>[
                  Icon(
                    attachment.isPdf
                        ? Icons.picture_as_pdf_outlined
                        : Icons.image_outlined,
                    size: AppSizes.iconMd,
                    color: palette.textSecondary,
                  ),
                  Gap.wMd,
                  Expanded(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: <Widget>[
                        Text(
                          attachment.fileName,
                          style: Theme.of(context).textTheme.bodyMedium,
                          maxLines: 1,
                          overflow: TextOverflow.ellipsis,
                        ),
                        if (attachment.readableSize.isNotEmpty)
                          Text(
                            attachment.readableSize,
                            style: Theme.of(context).textTheme.labelSmall
                                ?.copyWith(color: palette.textTertiary),
                          ),
                      ],
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
