import 'package:hr_employee_app/app/theme/app_spacing.dart';
import 'package:hr_employee_app/app/theme/app_theme.dart';
import 'package:hr_employee_app/core/widgets/app_avatar.dart';
import 'package:hr_employee_app/features/profile/domain/entities/employee_profile.dart';
import 'package:material_ui/material_ui.dart';

/// Avatar, name and role, with a camera affordance on the photo.
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
    final TextTheme textStyles = Theme.of(context).textTheme;

    return Container(
      width: double.infinity,
      padding: EdgeInsets.fromLTRB(
        AppSpacing.lg,
        AppSpacing.lg,
        AppSpacing.lg,
        AppSpacing.xxl,
      ),
      // Same gradient band and rounded edge as every other page header.
      decoration: BoxDecoration(
        gradient: LinearGradient(
          begin: Alignment.topCenter,
          end: Alignment.bottomCenter,
          colors: context.palette.brandGradient,
        ),
        borderRadius: BorderRadius.vertical(
          bottom: Radius.circular(AppRadius.xl),
        ),
      ),
      child: Column(
        children: <Widget>[
          Stack(
            children: <Widget>[
              AppAvatar(
                name: profile.fullName,
                imageUrl: profile.profilePhotoUrl,
                size: AppSizes.avatarLg,
                borderColor: Colors.white24,
              ),
              // The spinner replaces the camera button rather than sitting
              // beside it, so there is only ever one affordance.
              Positioned(
                right: 0,
                bottom: 0,
                child: Material(
                  color: Colors.white,
                  shape: const CircleBorder(),
                  child: InkWell(
                    onTap: isUploadingPhoto ? null : onChangePhoto,
                    customBorder: const CircleBorder(),
                    child: Padding(
                      padding: EdgeInsets.all(AppSpacing.xs),
                      child: isUploadingPhoto
                          ? const SizedBox.square(
                              dimension: 18,
                              child: CircularProgressIndicator(strokeWidth: 2),
                            )
                          : const Icon(
                              Icons.photo_camera_outlined,
                              size: 18,
                              color: AppColors.primary,
                            ),
                    ),
                  ),
                ),
              ),
            ],
          ),
          Gap.lg,
          Text(
            profile.fullName,
            style: textStyles.titleLarge?.copyWith(
              color: Colors.white,
              fontWeight: FontWeight.w700,
            ),
            textAlign: TextAlign.center,
          ),
          if (profile.roleLine.isNotEmpty) ...<Widget>[
            Gap.xxs,
            Text(
              profile.roleLine,
              style: textStyles.bodyMedium?.copyWith(color: Colors.white70),
              textAlign: TextAlign.center,
            ),
          ],
          Gap.sm,
          Container(
            padding: EdgeInsets.symmetric(
              horizontal: AppSpacing.md,
              vertical: AppSpacing.xs,
            ),
            decoration: BoxDecoration(
              color: Colors.white.withValues(alpha: 0.16),
              borderRadius: AppRadius.chip,
            ),
            child: Text(
              profile.employeeId,
              style: textStyles.labelSmall?.copyWith(
                color: Colors.white,
                fontWeight: FontWeight.w700,
              ),
            ),
          ),
        ],
      ),
    );
  }
}
