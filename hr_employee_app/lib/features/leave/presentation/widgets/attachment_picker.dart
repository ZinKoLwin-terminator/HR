import 'package:hr_employee_app/app/theme/app_spacing.dart';
import 'package:hr_employee_app/app/theme/app_theme.dart';
import 'package:hr_employee_app/core/constants/app_constants.dart';
import 'package:hr_employee_app/core/services/file_picker_service.dart';
import 'package:hr_employee_app/core/widgets/dashed_border.dart';
import 'package:material_ui/material_ui.dart';

/// Picks and lists the files attached to a leave request.
///
/// Stateless: the chosen files live in the apply form's state, so the list
/// survives a rebuild and can be submitted with the rest of the form.
class AttachmentPicker extends StatelessWidget {
  const AttachmentPicker({
    required this.files,
    required this.onAdd,
    required this.onRemove,
    super.key,
    this.isRequired = false,
    this.isEnabled = true,
    this.errorText,
  });

  final List<PickedFile> files;
  final VoidCallback onAdd;
  final void Function(PickedFile file) onRemove;

  /// True when the chosen leave type demands a document.
  final bool isRequired;

  final bool isEnabled;
  final String? errorText;

  @override
  Widget build(BuildContext context) {
    final AppPalette palette = context.palette;
    final TextTheme textStyles = Theme.of(context).textTheme;
    final bool canAddMore = files.length < UploadLimits.maxLeaveAttachments;

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: <Widget>[
        Row(
          children: <Widget>[
            Text.rich(
              TextSpan(
                children: <InlineSpan>[
                  const TextSpan(text: 'Attachments'),
                  if (isRequired)
                    TextSpan(
                      text: ' *',
                      style: TextStyle(color: palette.danger),
                    )
                  else
                    TextSpan(
                      text: ' (optional)',
                      style: TextStyle(
                        color: palette.textTertiary,
                        fontWeight: FontWeight.w400,
                      ),
                    ),
                ],
              ),
              style: textStyles.labelMedium,
            ),
            const Spacer(),
            Text(
              '${files.length} / ${UploadLimits.maxLeaveAttachments}',
              style: textStyles.labelSmall?.copyWith(
                color: palette.textTertiary,
              ),
            ),
          ],
        ),
        Gap.xs,

        for (final PickedFile file in files) ...<Widget>[
          _AttachmentRow(
            file: file,
            onRemove: isEnabled ? () => onRemove(file) : null,
          ),
          Gap.sm,
        ],

        if (canAddMore)
          _DropZone(
            label: files.isEmpty ? 'Add attachment' : 'Add another',
            onTap: isEnabled ? onAdd : null,
          ),

        if (errorText != null) ...<Widget>[
          Gap.xs,
          Text(
            errorText!,
            style: textStyles.bodySmall?.copyWith(color: palette.danger),
          ),
        ],
      ],
    );
  }
}

/// The dashed "add a file" slot.
class _DropZone extends StatelessWidget {
  const _DropZone({required this.label, required this.onTap});

  final String label;
  final VoidCallback? onTap;

  @override
  Widget build(BuildContext context) {
    final AppPalette palette = context.palette;
    final TextTheme textStyles = Theme.of(context).textTheme;

    return Semantics(
      button: true,
      enabled: onTap != null,
      label: label,
      child: ExcludeSemantics(
        child: CustomPaint(
          foregroundPainter: DashedBorderPainter(
            color: palette.textTertiary.withValues(alpha: 0.55),
            radius: AppRadius.md,
          ),
          child: Material(
            color: AppColors.cloud,
            borderRadius: AppRadius.field,
            clipBehavior: Clip.antiAlias,
            child: InkWell(
              onTap: onTap,
              child: Padding(
                padding: EdgeInsets.all(AppSpacing.md),
                child: Row(
                  children: <Widget>[
                    Container(
                      height: 36,
                      width: 36,
                      decoration: BoxDecoration(
                        color: AppColors.summitTeal.withValues(alpha: 0.12),
                        borderRadius: BorderRadius.circular(AppRadius.sm + 2),
                      ),
                      child: const Icon(
                        Icons.attach_file_rounded,
                        size: 18,
                        color: AppColors.summitTeal,
                      ),
                    ),
                    Gap.wMd,
                    Expanded(
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: <Widget>[
                          Text(
                            label,
                            style: textStyles.labelLarge?.copyWith(
                              color: palette.textPrimary,
                              fontWeight: FontWeight.w700,
                            ),
                          ),
                          Text(
                            'PDF, JPG or PNG · up to '
                            '${UploadLimits.maxLeaveAttachmentBytes ~/ (1024 * 1024)}'
                            ' MB each',
                            style: textStyles.bodySmall?.copyWith(
                              color: palette.textTertiary,
                            ),
                          ),
                        ],
                      ),
                    ),
                  ],
                ),
              ),
            ),
          ),
        ),
      ),
    );
  }
}

class _AttachmentRow extends StatelessWidget {
  const _AttachmentRow({required this.file, required this.onRemove});

  final PickedFile file;
  final VoidCallback? onRemove;

  @override
  Widget build(BuildContext context) {
    final AppPalette palette = context.palette;
    final bool isPdf = file.extension == 'pdf';

    return Container(
      padding: EdgeInsets.symmetric(
        horizontal: AppSpacing.md,
        vertical: AppSpacing.sm,
      ),
      decoration: BoxDecoration(
        color: AppColors.cloud,
        borderRadius: AppRadius.field,
        border: Border.all(color: palette.border),
      ),
      child: Row(
        children: <Widget>[
          Icon(
            isPdf ? Icons.picture_as_pdf_outlined : Icons.image_outlined,
            size: AppSizes.iconMd,
            color: palette.textSecondary,
          ),
          Gap.wMd,
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: <Widget>[
                Text(
                  file.name,
                  style: Theme.of(context).textTheme.bodyMedium,
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                ),
                Text(
                  file.readableSize,
                  style: Theme.of(context).textTheme.labelSmall
                      ?.copyWith(color: palette.textTertiary),
                ),
              ],
            ),
          ),
          IconButton(
            onPressed: onRemove,
            icon: const Icon(Icons.close_rounded),
            iconSize: AppSizes.iconMd,
            tooltip: 'Remove ${file.name}',
          ),
        ],
      ),
    );
  }
}
