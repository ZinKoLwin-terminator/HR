import 'package:cached_network_image/cached_network_image.dart';
import 'package:hr_employee_app/app/theme/app_colors.dart';
import 'package:hr_employee_app/app/theme/app_spacing.dart';
import 'package:hr_employee_app/core/extensions/string_extensions.dart';
import 'package:material_ui/material_ui.dart';

/// A circular profile photo with an initials fallback.
///
/// Remote images go through `cached_network_image` so a photo is fetched once
/// per device rather than on every screen that shows it. When there is no
/// photo, or it fails to load, initials are drawn instead — never a broken
/// image icon.
class AppAvatar extends StatelessWidget {
  const AppAvatar({
    required this.name,
    super.key,
    this.imageUrl,
    this.size,
    this.borderColor,
    this.onTap,
  });

  /// Used for the initials fallback and the accessibility label.
  final String name;

  final String? imageUrl;
  final double? size;
  final Color? borderColor;
  final VoidCallback? onTap;

  @override
  Widget build(BuildContext context) {
    final double dimension = size ?? AppSizes.avatarMd;
    final bool hasImage = imageUrl != null && imageUrl!.isNotBlank;

    final Widget avatar = Container(
      height: dimension,
      width: dimension,
      decoration: BoxDecoration(
        shape: BoxShape.circle,
        color: AppColors.primarySurface,
        border: borderColor == null
            ? null
            : Border.all(color: borderColor!, width: 2),
      ),
      clipBehavior: Clip.antiAlias,
      child: hasImage
          ? CachedNetworkImage(
              imageUrl: imageUrl!,
              fit: BoxFit.cover,
              width: dimension,
              height: dimension,
              placeholder: (BuildContext context, String url) =>
                  _InitialsAvatar(name: name, dimension: dimension),
              errorWidget: (BuildContext context, String url, Object error) =>
                  _InitialsAvatar(name: name, dimension: dimension),
            )
          : _InitialsAvatar(name: name, dimension: dimension),
    );

    final Widget labelled = Semantics(
      label: 'Profile photo of $name',
      image: true,
      child: ExcludeSemantics(child: avatar),
    );

    if (onTap == null) {
      return labelled;
    }
    return InkWell(
      onTap: onTap,
      customBorder: const CircleBorder(),
      child: labelled,
    );
  }
}

class _InitialsAvatar extends StatelessWidget {
  const _InitialsAvatar({required this.name, required this.dimension});

  final String name;
  final double dimension;

  @override
  Widget build(BuildContext context) {
    return ColoredBox(
      color: AppColors.primarySurface,
      child: Center(
        child: Text(
          name.initials,
          style: TextStyle(
            // Scales with the avatar so one widget serves every size.
            fontSize: dimension * 0.38,
            fontWeight: FontWeight.w700,
            color: AppColors.primary,
            height: 1,
          ),
        ),
      ),
    );
  }
}
