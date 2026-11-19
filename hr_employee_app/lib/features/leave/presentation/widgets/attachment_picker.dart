import 'package:hr_employee_app/app/theme/app_spacing.dart';
import 'package:hr_employee_app/app/theme/app_theme.dart';
import 'package:hr_employee_app/core/constants/app_constants.dart';
import 'package:hr_employee_app/core/services/file_picker_service.dart';
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
                    ),
                ],
              ),
              style: textStyles.labelMedium,
            ),
            const Spacer(),
            Text(
              '${files.length}/${UploadLimits.maxLeaveAttachments}',
              style: textStyles.labelSmall?.copyWith(
                color: palette.textTertiary,
              ),
            ),
          ],
        ),
        Gap.xs,
        Text(
          'Up to ${UploadLimits.maxLeaveAttachments} files, '
          '${UploadLimits.maxLeaveAttachmentBytes ~/ (1024 * 1024)} MB each '
          '(${UploadLimits.leaveAttachmentExtensions.join(', ')}).',
          style: textStyles.bodySmall?.copyWith(color: palette.textTertiary),
        ),
        Gap.sm,

        for (final PickedFile file in files) ...<Widget>[
          _AttachmentRow(
            file: file,
            onRemove: isEnabled ? () => onRemove(file) : null,
          ),
          Gap.sm,
        ],

        if (canAddMore)
          OutlinedButton.icon(
            onPressed: isEnabled ? onAdd : null,
            icon: const Icon(Icons.attach_file_rounded, size: 18),
            label: Text(files.isEmpty ? 'Add attachment' : 'Add another'),
            style: OutlinedButton.styleFrom(
              minimumSize: const Size(double.infinity, AppSizes.minTouchTarget),
            ),
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
        color: palette.surfaceMuted,
        borderRadius: AppRadius.field,
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
