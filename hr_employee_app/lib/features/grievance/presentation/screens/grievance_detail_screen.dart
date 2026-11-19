import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:hr_employee_app/app/router/route_names.dart';
import 'package:hr_employee_app/app/theme/app_spacing.dart';
import 'package:hr_employee_app/app/theme/app_theme.dart';
import 'package:hr_employee_app/core/errors/failure.dart';
import 'package:hr_employee_app/core/utils/date_formatter.dart';
import 'package:hr_employee_app/core/widgets/app_button.dart';
import 'package:hr_employee_app/core/widgets/app_snackbar.dart';
import 'package:hr_employee_app/core/widgets/async_value_view.dart';
import 'package:hr_employee_app/core/widgets/confirm_dialog.dart';
import 'package:hr_employee_app/features/grievance/domain/entities/grievance.dart';
import 'package:hr_employee_app/features/grievance/domain/entities/grievance_status.dart';
import 'package:hr_employee_app/features/grievance/presentation/providers/grievance_controllers.dart';
import 'package:hr_employee_app/features/grievance/presentation/widgets/grievance_header.dart';
import 'package:hr_employee_app/features/grievance/presentation/widgets/grievance_message_thread.dart';
import 'package:hr_employee_app/features/grievance/presentation/widgets/grievance_status_chip.dart';
import 'package:hr_employee_app/features/grievance/presentation/widgets/grievance_timeline.dart';
import 'package:hr_employee_app/shared/models/submission_state.dart';
import 'package:material_ui/material_ui.dart';

/// One of the employee's own grievances: what they reported, the outcome
/// when HR shares it, the status history with HR's reasons, and the
/// conversation with HR (its box becomes "Respond" while HR waits for an
/// answer). A draft offers Edit and Delete. Opening it clears the unread dot.
class GrievanceDetailScreen extends ConsumerStatefulWidget {
  const GrievanceDetailScreen({required this.grievanceId, super.key});

  final int grievanceId;

  @override
  ConsumerState<GrievanceDetailScreen> createState() =>
      _GrievanceDetailScreenState();
}

class _GrievanceDetailScreenState extends ConsumerState<GrievanceDetailScreen> {
  bool _markedSeen = false;

  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (mounted) {
        ref.read(grievanceActionsControllerProvider.notifier).reset();
      }
    });
  }

  void _markSeenOnce(GrievanceDetail grievance) {
    if (_markedSeen || !grievance.hasUnreadUpdate) {
      return;
    }
    _markedSeen = true;
    ref.read(grievanceActionsControllerProvider.notifier).markSeen(grievance);
  }

  Future<void> _delete(GrievanceDetail draft) async {
    final bool confirmed = await ConfirmDialog.show(
      context,
      title: 'Delete this draft?',
      message: '"${draft.subject}" will be removed. Nothing was sent to HR.',
      confirmLabel: 'Delete draft',
      isDestructive: true,
      icon: Icons.delete_outline_rounded,
    );
    if (!confirmed || !mounted) {
      return;
    }
    final bool deleted = await ref
        .read(grievanceActionsControllerProvider.notifier)
        .deleteDraft(draft);
    if (!mounted) {
      return;
    }
    if (deleted) {
      AppSnackBar.success(context, 'Draft deleted.');
      context.canPop() ? context.pop() : context.goNamed(RouteNames.grievances);
      return;
    }
    _showFailure();
  }

  Future<bool> _send(GrievanceDetail grievance, String body) async {
    final GrievanceDetail? updated = await ref
        .read(grievanceActionsControllerProvider.notifier)
        .send(grievance, body);
    if (!mounted) {
      return updated != null;
    }
    if (updated != null) {
      AppSnackBar.success(
        context,
        grievance.isRespondMode
            ? 'Your answer was sent to HR.'
            : 'Message sent.',
      );
      return true;
    }
    _showFailure();
    return false;
  }

  void _showFailure() {
    final Failure? failure = ref
        .read(grievanceActionsControllerProvider)
        .failureOrNull;
    if (failure != null) {
      AppSnackBar.failure(context, failure);
    }
  }

  void _edit(GrievanceDetail draft) {
    context.pushNamed(
      RouteNames.editGrievance,
      pathParameters: <String, String>{RouteParams.grievanceId: '${draft.id}'},
    );
  }

  /// How far the summary card rides up over the header's foot.
  static const double _overlap = 28;

  @override
  Widget build(BuildContext context) {
    final AsyncValue<GrievanceDetail> detail = ref.watch(
      grievanceDetailProvider(widget.grievanceId),
    );
    final SubmissionState actions = ref.watch(
      grievanceActionsControllerProvider,
    );
    final GrievanceDetail? loaded = detail.value;
    if (loaded != null) {
      _markSeenOnce(loaded);
    }

    return GrievanceScaffold(
      title: loaded == null
          ? 'Grievance'
          : (loaded.caseNo ?? 'Draft grievance'),
      subtitle: loaded?.categoryName,
      actions: <Widget>[
        if (loaded != null && loaded.canEditDraft) ...<Widget>[
          GlassIconButton(
            tooltip: 'Edit draft',
            icon: Icons.edit_outlined,
            onPressed: actions.isSubmitting ? null : () => _edit(loaded),
          ),
          GlassIconButton(
            tooltip: 'Delete draft',
            icon: Icons.delete_outline_rounded,
            onPressed: actions.isSubmitting ? null : () => _delete(loaded),
          ),
        ],
      ],
      body: AsyncValueView<GrievanceDetail>(
        value: detail,
        loadingMessages: const <String>[
          'Fetching the grievance…',
          'Loading messages with HR…',
          'Almost there…',
        ],
        onRetry: () =>
            ref.invalidate(grievanceDetailProvider(widget.grievanceId)),
        data: (GrievanceDetail g) {
          final GrievanceMessage? request = g.openInfoRequest;

          return RefreshIndicator(
            onRefresh: () async {
              ref.invalidate(grievanceDetailProvider(widget.grievanceId));
              await ref.read(
                grievanceDetailProvider(widget.grievanceId).future,
              );
            },
            child: ListView(
              physics: const AlwaysScrollableScrollPhysics(),
              padding: EdgeInsets.zero,
              children: <Widget>[
                // The summary card straddles the header's foot.
                Stack(
                  children: <Widget>[
                    const Positioned(
                      top: 0,
                      left: 0,
                      right: 0,
                      height: _overlap,
                      child: ColoredBox(color: AppColors.apexNavy),
                    ),
                    Padding(
                      padding: EdgeInsets.symmetric(horizontal: AppSpacing.lg),
                      child: _SummaryCard(grievance: g),
                    ),
                  ],
                ),
                Padding(
                  padding: EdgeInsets.fromLTRB(
                    AppSpacing.lg,
                    AppSpacing.md,
                    AppSpacing.lg,
                    AppSpacing.xxxl,
                  ),
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.stretch,
                    children: <Widget>[
                      if (request != null) ...<Widget>[
                        _Callout(
                          title: 'HR needs more information',
                          text:
                              '${request.body} Your answer moves this '
                              'forward again.',
                          tone: context.palette.warning,
                        ),
                        Gap.md,
                      ],
                      if (g.rejectionReason != null) ...<Widget>[
                        _Callout(
                          title: 'Why HR did not take it further',
                          text: g.rejectionReason!,
                          tone: context.palette.danger,
                        ),
                        Gap.md,
                      ],
                      if (g.resolutionSummary != null) ...<Widget>[
                        _Callout(
                          title: 'Resolution',
                          text: g.resolutionSummary!,
                          tone: context.palette.success,
                        ),
                        Gap.md,
                      ],
                      if (!g.isDraft) ...<Widget>[
                        _Section(
                          title: 'Messages with HR',
                          trailing: g.messages.length == 1
                              ? '1 message'
                              : '${g.messages.length} messages',
                          child: GrievanceMessageThread(
                            messages: g.messages,
                            canReply: g.canMessage,
                            isRespondMode: g.isRespondMode,
                            isBusy: actions.isSubmitting,
                            onSend: (String body) => _send(g, body),
                          ),
                        ),
                        Gap.md,
                      ],
                      _Section(
                        title: 'What you reported',
                        child: _Facts(grievance: g),
                      ),
                      if (g.attachments.isNotEmpty) ...<Widget>[
                        Gap.md,
                        _Section(
                          title: 'Files',
                          trailing: g.attachments.length == 1
                              ? '1 file'
                              : '${g.attachments.length} files',
                          child: _Attachments(files: g.attachments),
                        ),
                      ],
                      Gap.md,
                      _Section(
                        title: 'History',
                        child: GrievanceTimeline(entries: g.timeline),
                      ),
                      if (g.canEditDraft) ...<Widget>[
                        Gap.xl,
                        AppButton(
                          label: 'Edit draft',
                          icon: Icons.edit_outlined,
                          isEnabled: !actions.isSubmitting,
                          onPressed: () => _edit(g),
                        ),
                        Gap.sm,
                        AppButton.danger(
                          label: 'Delete draft',
                          loadingLabel: 'Deleting…',
                          icon: Icons.delete_outline_rounded,
                          isLoading: actions.isSubmitting,
                          onPressed: () => _delete(g),
                        ),
                      ],
                    ],
                  ),
                ),
              ],
            ),
          );
        },
      ),
    );
  }
}

/// Status and anonymity, the subject, how far along it is, and the dates.
class _SummaryCard extends StatelessWidget {
  const _SummaryCard({required this.grievance});

  final GrievanceDetail grievance;

  @override
  Widget build(BuildContext context) {
    final AppPalette palette = context.palette;
    final TextTheme textStyles = Theme.of(context).textTheme;
    final GrievanceDetail g = grievance;
    final Color tone = grievanceTone(context, g.status);

    final List<(String, String)> facts = <(String, String)>[
      ('Reported', g.isAnonymous ? 'Anonymously' : 'With your name'),
      (
        g.isDraft ? 'Started' : 'Submitted',
        DateFormatter.dayMonthTime(g.submittedAt ?? g.createdAt),
      ),
      if (g.lastUpdateAt != null)
        ('Last update', DateFormatter.dayMonthTime(g.lastUpdateAt!)),
      if (g.targetResolutionDate != null)
        ('Target date', DateFormatter.dayMonthYear(g.targetResolutionDate!)),
      if (g.resolvedAt != null)
        ('Resolved', DateFormatter.dayMonthTime(g.resolvedAt!)),
      if (g.closedAt != null)
        ('Closed', DateFormatter.dayMonthTime(g.closedAt!)),
    ];

    return _Card(
      shadow: AppColors.shadowLift,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: <Widget>[
          Wrap(
            spacing: AppSpacing.sm,
            runSpacing: AppSpacing.xs,
            children: <Widget>[
              GrievancePill(
                label: grievanceShortLabel(g.status),
                color: tone,
                showDot: true,
              ),
              if (g.isAnonymous)
                GrievancePill(label: 'Anonymous', color: palette.textTertiary),
            ],
          ),
          Gap.md,
          Text(
            g.subject,
            style: textStyles.titleMedium?.copyWith(
              fontWeight: FontWeight.w800,
            ),
          ),
          Gap.md,
          if (g.isDraft)
            Text(
              'This is a draft. Nothing has been sent to HR yet.',
              style: textStyles.bodySmall?.copyWith(color: palette.info),
            )
          else
            _Stepper(status: g.status),
          Gap.md,
          const Divider(height: 1, color: AppColors.divider),
          Gap.md,
          LayoutBuilder(
            builder: (BuildContext context, BoxConstraints constraints) {
              final double cell = (constraints.maxWidth - AppSpacing.md) / 2;
              return Wrap(
                spacing: AppSpacing.md,
                runSpacing: AppSpacing.md,
                children: <Widget>[
                  for (final (String label, String value) in facts)
                    SizedBox(
                      width: cell,
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: <Widget>[
                          Text(
                            label,
                            style: textStyles.labelSmall?.copyWith(
                              color: palette.textTertiary,
                            ),
                          ),
                          Text(
                            value,
                            style: textStyles.labelLarge?.copyWith(
                              fontWeight: FontWeight.w800,
                            ),
                          ),
                        ],
                      ),
                    ),
                ],
              );
            },
          ),
        ],
      ),
    );
  }
}

/// Sent → Received → In review → Resolved, as four bars: done in navy, the
/// current one in teal, the rest grey. A refused case ends in red.
class _Stepper extends StatelessWidget {
  const _Stepper({required this.status});

  final GrievanceStatus status;

  static const List<String> _labels = <String>[
    'Sent',
    'Received',
    'In review',
    'Resolved',
  ];

  /// The step the case has reached, 0-based.
  static int _reached(GrievanceStatus status) => switch (status) {
    GrievanceStatus.draft || GrievanceStatus.unknown => -1,
    GrievanceStatus.submitted => 0,
    GrievanceStatus.acknowledged => 1,
    GrievanceStatus.resolved || GrievanceStatus.closed => 3,
    GrievanceStatus.rejected => 3,
    _ => 2,
  };

  @override
  Widget build(BuildContext context) {
    final AppPalette palette = context.palette;
    final TextTheme textStyles = Theme.of(context).textTheme;
    final int reached = _reached(status);
    final bool isRefused = status == GrievanceStatus.rejected;
    final bool isDone =
        status == GrievanceStatus.resolved || status == GrievanceStatus.closed;

    Color barColour(int i) {
      if (i > reached) {
        return palette.border;
      }
      if (i == reached) {
        if (isRefused) {
          return palette.danger;
        }
        return isDone ? palette.success : AppColors.summitTeal;
      }
      return AppColors.apexNavy;
    }

    return Row(
      children: <Widget>[
        for (int i = 0; i < _labels.length; i++) ...<Widget>[
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: <Widget>[
                Container(
                  height: 5,
                  decoration: BoxDecoration(
                    color: barColour(i),
                    borderRadius: BorderRadius.circular(AppRadius.pill),
                  ),
                ),
                Gap.xs,
                Text(
                  i == 3 && isRefused ? 'Not taken further' : _labels[i],
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                  style: textStyles.labelSmall?.copyWith(
                    fontSize: 10,
                    fontWeight: i <= reached
                        ? FontWeight.w700
                        : FontWeight.w500,
                    color: i <= reached
                        ? palette.textPrimary
                        : palette.textTertiary,
                  ),
                ),
              ],
            ),
          ),
          if (i < _labels.length - 1) const SizedBox(width: 4),
        ],
      ],
    );
  }
}

/// A tinted note: HR asking for more, the outcome, or a refusal.
class _Callout extends StatelessWidget {
  const _Callout({required this.title, required this.text, required this.tone});

  final String title;
  final String text;
  final Color tone;

  @override
  Widget build(BuildContext context) {
    final TextTheme textStyles = Theme.of(context).textTheme;
    final Color ink = Color.lerp(tone, Colors.black, 0.35)!;

    return Container(
      padding: EdgeInsets.all(AppSpacing.md),
      decoration: BoxDecoration(
        color: Color.alphaBlend(
          tone.withValues(alpha: 0.10),
          AppColors.surface,
        ),
        borderRadius: BorderRadius.circular(AppRadius.lg),
        border: Border.all(color: tone.withValues(alpha: 0.30)),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          Row(
            children: <Widget>[
              Container(
                height: 7,
                width: 7,
                decoration: BoxDecoration(color: tone, shape: BoxShape.circle),
              ),
              Gap.wSm,
              Expanded(
                child: Text(
                  title,
                  style: textStyles.labelLarge?.copyWith(
                    color: ink,
                    fontWeight: FontWeight.w800,
                  ),
                ),
              ),
            ],
          ),
          Gap.xs,
          Text(text, style: textStyles.bodyMedium?.copyWith(color: ink)),
        ],
      ),
    );
  }
}

/// Category, place and people as a bordered table, then the description.
class _Facts extends StatelessWidget {
  const _Facts({required this.grievance});

  final GrievanceDetail grievance;

  @override
  Widget build(BuildContext context) {
    final AppPalette palette = context.palette;
    final TextTheme textStyles = Theme.of(context).textTheme;
    final GrievanceDetail g = grievance;

    final List<(String, String)> rows = <(String, String)>[
      ('Category', g.categoryName),
      if (g.incidentDate != null)
        ('Incident date', DateFormatter.dayMonthYear(g.incidentDate!)),
      if (g.location != null) ('Location', g.location!),
      if (g.involvedPersonText != null)
        ('Person involved', g.involvedPersonText!),
      if (g.involvedDepartmentName != null)
        ('Department involved', g.involvedDepartmentName!),
    ];

    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: <Widget>[
        Container(
          decoration: BoxDecoration(
            borderRadius: AppRadius.field,
            border: Border.all(color: palette.border),
          ),
          child: Column(
            children: <Widget>[
              for (int i = 0; i < rows.length; i++) ...<Widget>[
                Padding(
                  padding: EdgeInsets.symmetric(
                    horizontal: AppSpacing.md,
                    vertical: AppSpacing.sm + 2,
                  ),
                  child: Row(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: <Widget>[
                      Text(
                        rows[i].$1,
                        style: textStyles.bodySmall?.copyWith(
                          color: palette.textSecondary,
                        ),
                      ),
                      Gap.wMd,
                      Expanded(
                        child: Text(
                          rows[i].$2,
                          textAlign: TextAlign.end,
                          style: textStyles.labelLarge?.copyWith(
                            fontWeight: FontWeight.w700,
                          ),
                        ),
                      ),
                    ],
                  ),
                ),
                if (i < rows.length - 1)
                  const Divider(height: 1, color: AppColors.divider),
              ],
            ],
          ),
        ),
        Gap.md,
        Text(
          'Description',
          style: textStyles.labelSmall?.copyWith(color: palette.textTertiary),
        ),
        Gap.xxs,
        Text(
          g.description.isEmpty ? '—' : g.description,
          style: textStyles.bodyMedium,
        ),
      ],
    );
  }
}

/// White, softly raised card.
class _Card extends StatelessWidget {
  const _Card({required this.child, this.shadow = AppColors.shadow});

  final Widget child;
  final Color shadow;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: EdgeInsets.all(AppSpacing.lg),
      decoration: BoxDecoration(
        color: AppColors.surface,
        borderRadius: BorderRadius.circular(AppRadius.xl),
        boxShadow: <BoxShadow>[
          BoxShadow(color: shadow, blurRadius: 16, offset: const Offset(0, 4)),
        ],
      ),
      child: child,
    );
  }
}

class _Section extends StatelessWidget {
  const _Section({required this.title, required this.child, this.trailing});

  final String title;
  final String? trailing;
  final Widget child;

  @override
  Widget build(BuildContext context) {
    final TextTheme textStyles = Theme.of(context).textTheme;

    return _Card(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: <Widget>[
          Row(
            children: <Widget>[
              Expanded(
                child: Text(
                  title,
                  style: textStyles.titleSmall?.copyWith(
                    fontWeight: FontWeight.w800,
                  ),
                ),
              ),
              if (trailing != null)
                Text(
                  trailing!,
                  style: textStyles.labelSmall?.copyWith(
                    color: context.palette.textTertiary,
                  ),
                ),
            ],
          ),
          Gap.md,
          child,
        ],
      ),
    );
  }
}

/// The files shared on the case. Listed only: adding and downloading files
/// is done on the web app for now.
class _Attachments extends StatelessWidget {
  const _Attachments({required this.files});

  final List<GrievanceAttachment> files;

  @override
  Widget build(BuildContext context) {
    final AppPalette palette = context.palette;
    final TextTheme textStyles = Theme.of(context).textTheme;

    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: <Widget>[
        for (final GrievanceAttachment file in files)
          Padding(
            padding: EdgeInsets.only(bottom: AppSpacing.xs),
            child: Row(
              children: <Widget>[
                Icon(
                  Icons.insert_drive_file_outlined,
                  size: AppSizes.iconMd,
                  color: palette.textTertiary,
                ),
                Gap.wSm,
                Expanded(
                  child: Text(
                    file.fileName,
                    style: textStyles.bodyMedium,
                    maxLines: 1,
                    overflow: TextOverflow.ellipsis,
                  ),
                ),
                Text(
                  file.uploadedBy,
                  style: textStyles.bodySmall?.copyWith(
                    color: palette.textTertiary,
                  ),
                ),
              ],
            ),
          ),
        Text(
          'Open the grievance on the web app to add or download files.',
          style: textStyles.bodySmall?.copyWith(color: palette.textSecondary),
        ),
      ],
    );
  }
}
