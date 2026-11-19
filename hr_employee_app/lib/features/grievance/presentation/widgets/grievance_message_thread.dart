import 'package:hr_employee_app/app/theme/app_spacing.dart';
import 'package:hr_employee_app/app/theme/app_theme.dart';
import 'package:hr_employee_app/core/utils/date_formatter.dart';
import 'package:hr_employee_app/core/widgets/app_button.dart';
import 'package:hr_employee_app/features/grievance/data/models/grievance_enum_mappers.dart';
import 'package:hr_employee_app/features/grievance/domain/entities/grievance.dart';
import 'package:hr_employee_app/features/grievance/domain/entities/grievance_rules.dart';
import 'package:hr_employee_app/features/grievance/domain/entities/grievance_status.dart';
import 'package:material_ui/material_ui.dart';

/// The conversation with HR, and the box to write the next message.
///
/// While HR waits for the employee ([isRespondMode]) the box asks for the
/// answer to HR's request and its button reads "Respond": sending it moves
/// the grievance forward again.
class GrievanceMessageThread extends StatefulWidget {
  const GrievanceMessageThread({
    required this.messages,
    required this.canReply,
    required this.isRespondMode,
    required this.isBusy,
    required this.onSend,
    super.key,
  });

  final List<GrievanceMessage> messages;
  final bool canReply;
  final bool isRespondMode;
  final bool isBusy;

  /// Returns true when the message was sent, so the box can be cleared.
  final Future<bool> Function(String body) onSend;

  @override
  State<GrievanceMessageThread> createState() => _GrievanceMessageThreadState();
}

class _GrievanceMessageThreadState extends State<GrievanceMessageThread> {
  final TextEditingController _controller = TextEditingController();

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  Future<void> _send() async {
    final String text = _controller.text.trim();
    if (text.isEmpty || widget.isBusy) {
      return;
    }
    FocusScope.of(context).unfocus();
    final bool sent = await widget.onSend(text);
    if (sent && mounted) {
      _controller.clear();
    }
  }

  @override
  Widget build(BuildContext context) {
    final AppPalette palette = context.palette;
    final TextTheme textStyles = Theme.of(context).textTheme;
    final List<GrievanceMessage> ordered =
        List<GrievanceMessage>.of(widget.messages)..sort(
          (GrievanceMessage a, GrievanceMessage b) =>
              a.createdAt.compareTo(b.createdAt),
        );

    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: <Widget>[
        if (ordered.isEmpty)
          Text(
            'No messages yet.',
            style: textStyles.bodySmall?.copyWith(color: palette.textSecondary),
          )
        else
          for (final GrievanceMessage m in ordered) _Bubble(message: m),
        if (widget.canReply) ...<Widget>[
          Gap.md,
          ValueListenableBuilder<TextEditingValue>(
            valueListenable: _controller,
            builder: (BuildContext context, TextEditingValue value, _) {
              final int length = value.text.characters.length;
              return Column(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: <Widget>[
                  Row(
                    children: <Widget>[
                      Expanded(
                        child: Text(
                          widget.isRespondMode
                              ? 'Your answer to HR'
                              : 'Message to HR',
                          style: textStyles.labelMedium?.copyWith(
                            fontWeight: FontWeight.w700,
                          ),
                        ),
                      ),
                      Text(
                        '$length / ${GrievanceRules.messageMaxLength}',
                        style: textStyles.labelSmall?.copyWith(
                          color: palette.textTertiary,
                        ),
                      ),
                    ],
                  ),
                  Gap.xs,
                  TextField(
                    key: const ValueKey<String>('grievance-reply-field'),
                    controller: _controller,
                    enabled: !widget.isBusy,
                    minLines: 2,
                    maxLines: 5,
                    maxLength: GrievanceRules.messageMaxLength,
                    style: textStyles.bodyLarge,
                    decoration: const InputDecoration(
                      hintText: 'Write to HR',
                      counterText: '',
                    ),
                  ),
                  Gap.sm,
                  AppButton(
                    label: widget.isRespondMode ? 'Respond' : 'Send',
                    trailingIcon: Icons.send_rounded,
                    isLoading: widget.isBusy,
                    isEnabled: value.text.trim().isNotEmpty,
                    onPressed: _send,
                  ),
                ],
              );
            },
          ),
        ],
      ],
    );
  }
}

class _Bubble extends StatelessWidget {
  const _Bubble({required this.message});

  final GrievanceMessage message;

  @override
  Widget build(BuildContext context) {
    final AppPalette palette = context.palette;
    final TextTheme textStyles = Theme.of(context).textTheme;
    final bool mine = message.isMine;
    final Color fill = mine
        ? Color.alphaBlend(
            AppColors.summitTeal.withValues(alpha: 0.12),
            AppColors.surface,
          )
        : palette.surfaceMuted;
    const Radius round = Radius.circular(14);
    const Radius tail = Radius.circular(4);

    return Padding(
      padding: EdgeInsets.only(bottom: AppSpacing.md),
      child: Column(
        crossAxisAlignment: mine
            ? CrossAxisAlignment.end
            : CrossAxisAlignment.start,
        children: <Widget>[
          Container(
            constraints: BoxConstraints(
              maxWidth: MediaQuery.sizeOf(context).width * 0.72,
            ),
            padding: EdgeInsets.symmetric(
              horizontal: AppSpacing.md,
              vertical: AppSpacing.sm + 2,
            ),
            decoration: BoxDecoration(
              color: fill,
              borderRadius: BorderRadius.only(
                topLeft: mine ? round : tail,
                topRight: mine ? tail : round,
                bottomLeft: round,
                bottomRight: round,
              ),
            ),
            child: Text(message.body, style: textStyles.bodyMedium),
          ),
          Gap.xxs,
          Text(
            <String>[
              message.actor,
              if (message.kind != GrievanceMessageKind.message)
                message.kind.label,
              DateFormatter.dayMonthTime(message.createdAt),
            ].join(' · '),
            style: textStyles.labelSmall?.copyWith(color: palette.textTertiary),
          ),
        ],
      ),
    );
  }
}
