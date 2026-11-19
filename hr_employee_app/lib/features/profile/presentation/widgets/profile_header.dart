import 'dart:math' as math;

import 'package:hr_employee_app/app/theme/app_spacing.dart';
import 'package:hr_employee_app/app/theme/app_theme.dart';
import 'package:hr_employee_app/core/extensions/string_extensions.dart';
import 'package:hr_employee_app/core/utils/date_formatter.dart';
import 'package:hr_employee_app/features/profile/domain/entities/employee_profile.dart';
import 'package:material_ui/material_ui.dart';

/// The identity card at the top of the profile: photo, name, role and
/// place of work, then employee ID, joining date and length of service.
///
/// It sits on the navy header and rides over its foot; a teal strip along
/// its top edge ties it to the brand.
class ProfileHeader extends StatelessWidget {
  const ProfileHeader({
    required this.profile,
    required this.onChangePhoto,
    super.key,
    this.isUploadingPhoto = false,
  });

  final EmployeeProfile profile;
  final VoidCallback onChangePhoto;
  final bool isUploadingPhoto;

  @override
  Widget build(BuildContext context) {
    final AppPalette palette = context.palette;
    final TextTheme textStyles = Theme.of(context).textTheme;
    final String place = <String?>[
      profile.department,
      profile.workLocation,
    ].whereType<String>().where((String s) => s.isNotBlank).join(' · ');
    final DateTime? joined = profile.dateOfJoining;

    return Container(
      clipBehavior: Clip.antiAlias,
      decoration: BoxDecoration(
        color: AppColors.surface,
        borderRadius: BorderRadius.circular(AppRadius.xl),
        boxShadow: const <BoxShadow>[
          BoxShadow(
            color: AppColors.shadowLift,
            blurRadius: 24,
            offset: Offset(0, 8),
          ),
        ],
      ),
      child: CustomPaint(
        painter: _Watermark(color: palette.border),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: <Widget>[
            Container(height: 4, color: AppColors.summitTeal),
            Padding(
              padding: EdgeInsets.fromLTRB(
                AppSpacing.lg,
                AppSpacing.lg,
                AppSpacing.lg,
                AppSpacing.md,
              ),
              child: Row(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: <Widget>[
                  _Photo(
                    profile: profile,
                    isUploading: isUploadingPhoto,
                    onChange: onChangePhoto,
                  ),
                  Gap.wLg,
                  Expanded(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: <Widget>[
                        Text(
                          profile.fullName,
                          style: textStyles.titleLarge?.copyWith(
                            fontWeight: FontWeight.w800,
                          ),
                        ),
                        if (profile.designation.isNotNullOrBlank)
                          Text(
                            profile.designation!,
                            style: textStyles.bodySmall?.copyWith(
                              color: palette.textSecondary,
                            ),
                          ),
                        if (place.isNotEmpty)
                          Text(
                            place,
                            style: textStyles.bodySmall?.copyWith(
                              color: palette.textSecondary,
                            ),
                          ),
                      ],
                    ),
                  ),
                ],
              ),
            ),
            Divider(
              height: 1,
              indent: AppSpacing.lg,
              endIndent: AppSpacing.lg,
              color: AppColors.divider,
            ),
            Padding(
              padding: EdgeInsets.all(AppSpacing.lg),
              child: Row(
                children: <Widget>[
                  Expanded(
                    child: _Stat(
                      label: 'Employee ID',
                      value: profile.employeeId,
                    ),
                  ),
                  Expanded(
                    child: _Stat(
                      label: 'Joined',
                      value: joined == null
                          ? '—'
                          : DateFormatter.dayMonthYear(joined),
                    ),
                  ),
                  Expanded(
                    child: _Stat(
                      label: 'Service',
                      value: joined == null ? '—' : _serviceLength(joined),
                    ),
                  ),
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }

  /// `5y 9m` from the joining date to today.
  static String _serviceLength(DateTime joined) {
    final DateTime now = DateTime.now();
    int months = (now.year - joined.year) * 12 + now.month - joined.month;
    if (now.day < joined.day) {
      months -= 1;
    }
    months = math.max(months, 0);
    final int years = months ~/ 12;
    final int rest = months % 12;
    if (years == 0) {
      return '${rest}m';
    }
    return rest == 0 ? '${years}y' : '${years}y ${rest}m';
  }
}

/// A rounded-square photo, or initials on navy, with a teal camera badge.
class _Photo extends StatelessWidget {
  const _Photo({
    required this.profile,
    required this.isUploading,
    required this.onChange,
  });

  final EmployeeProfile profile;
  final bool isUploading;
  final VoidCallback onChange;

  static const double _size = 72;

  @override
  Widget build(BuildContext context) {
    final String? url = profile.profilePhotoUrl;
    final BorderRadius radius = BorderRadius.circular(AppRadius.lg);

    final Widget initials = Center(
      child: Text(
        _initials(profile.fullName),
        style: Theme.of(context).textTheme.headlineSmall?.copyWith(
          color: AppColors.textOnPrimary,
          fontWeight: FontWeight.w700,
        ),
      ),
    );

    return SizedBox.square(
      dimension: _size + 6,
      child: Stack(
        children: <Widget>[
          Container(
            height: _size,
            width: _size,
            clipBehavior: Clip.antiAlias,
            decoration: BoxDecoration(
              color: AppColors.apexNavy,
              borderRadius: radius,
            ),
            child: url.isNotNullOrBlank
                ? Image.network(
                    url!,
                    fit: BoxFit.cover,
                    errorBuilder: (_, _, _) => initials,
                  )
                : initials,
          ),
          // The spinner replaces the camera button rather than sitting
          // beside it, so there is only ever one affordance.
          Positioned(
            right: 0,
            bottom: 0,
            child: Material(
              color: AppColors.summitTeal,
              shape: const CircleBorder(
                side: BorderSide(color: AppColors.surface, width: 2),
              ),
              clipBehavior: Clip.antiAlias,
              child: InkWell(
                onTap: isUploading ? null : onChange,
                child: Padding(
                  padding: const EdgeInsets.all(5),
                  child: isUploading
                      ? const SizedBox.square(
                          dimension: 14,
                          child: CircularProgressIndicator(
                            strokeWidth: 2,
                            color: AppColors.textOnPrimary,
                          ),
                        )
                      : Semantics(
                          button: true,
                          label: 'Change profile photo',
                          child: const Icon(
                            Icons.photo_camera_outlined,
                            size: 14,
                            color: AppColors.textOnPrimary,
                          ),
                        ),
                ),
              ),
            ),
          ),
        ],
      ),
    );
  }

  static String _initials(String name) {
    final List<String> parts = name
        .trim()
        .split(RegExp(r'\s+'))
        .where((String p) => p.isNotEmpty)
        .toList();
    if (parts.isEmpty) {
      return '?';
    }
    final String first = parts.first[0];
    final String last = parts.length > 1 ? parts.last[0] : '';
    return (first + last).toUpperCase();
  }
}

class _Stat extends StatelessWidget {
  const _Stat({required this.label, required this.value});

  final String label;
  final String value;

  @override
  Widget build(BuildContext context) {
    final TextTheme textStyles = Theme.of(context).textTheme;

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: <Widget>[
        Text(
          label.toUpperCase(),
          style: textStyles.labelSmall?.copyWith(
            fontSize: 9,
            letterSpacing: 0.8,
            fontWeight: FontWeight.w700,
            color: context.palette.textTertiary,
          ),
        ),
        Gap.xxs,
        Text(
          value,
          style: textStyles.labelLarge?.copyWith(fontWeight: FontWeight.w800),
          maxLines: 1,
          overflow: TextOverflow.ellipsis,
        ),
      ],
    );
  }
}

/// A faint hexagon outline in the lower right, behind the stats.
class _Watermark extends CustomPainter {
  const _Watermark({required this.color});

  final Color color;

  @override
  void paint(Canvas canvas, Size size) {
    final Paint paint = Paint()
      ..style = PaintingStyle.stroke
      ..strokeWidth = 1.2
      ..color = color.withValues(alpha: 0.8);
    final Offset centre = Offset(size.width * 0.88, size.height * 0.62);
    for (final double radius in <double>[48, 78]) {
      final Path path = Path();
      for (int i = 0; i < 6; i++) {
        final double angle = math.pi / 3 * i - math.pi / 2;
        final Offset p =
            centre + Offset(math.cos(angle), math.sin(angle)) * radius;
        if (i == 0) {
          path.moveTo(p.dx, p.dy);
        } else {
          path.lineTo(p.dx, p.dy);
        }
      }
      canvas.drawPath(path..close(), paint);
    }
  }

  @override
  bool shouldRepaint(_Watermark oldDelegate) => oldDelegate.color != color;
}
