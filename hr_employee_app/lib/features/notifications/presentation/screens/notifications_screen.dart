import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:hr_employee_app/app/config/app_config.dart';
import 'package:hr_employee_app/app/router/route_names.dart';
import 'package:hr_employee_app/app/theme/app_spacing.dart';
import 'package:hr_employee_app/app/theme/app_theme.dart';
import 'package:hr_employee_app/core/providers/core_providers.dart';
import 'package:hr_employee_app/core/widgets/app_empty_view.dart';
import 'package:hr_employee_app/core/widgets/app_snackbar.dart';
import 'package:hr_employee_app/core/widgets/async_value_view.dart';
import 'package:hr_employee_app/core/widgets/mock_data_banner.dart';
import 'package:hr_employee_app/core/widgets/paginated_list_view.dart';
import 'package:hr_employee_app/features/notifications/domain/entities/app_notification.dart';
import 'package:hr_employee_app/features/notifications/presentation/providers/notification_controllers.dart';
import 'package:hr_employee_app/features/notifications/presentation/widgets/notification_links.dart';
import 'package:hr_employee_app/features/notifications/presentation/widgets/notification_tile.dart';
import 'package:hr_employee_app/shared/models/paginated_state.dart';
import 'package:material_ui/material_ui.dart';

/// The kinds of notification the inbox can be narrowed to. Applied to the
/// pages already loaded; "Unread" alone is asked of the server.
enum _Category {
  all('All'),
  unread('Unread'),
  leave('Leave'),
  payslips('Payslips'),
  attendance('Attendance'),
  announcements('Announcements');

  const _Category(this.label);

  final String label;

  bool matches(AppNotification n) => switch (this) {
    _Category.all || _Category.unread => true,
    _Category.leave =>
      n.type == NotificationType.leaveApproval ||
          n.type == NotificationType.leaveRejection,
    _Category.payslips => n.type == NotificationType.payslip,
    _Category.attendance => n.type == NotificationType.attendance,
    _Category.announcements =>
      n.type == NotificationType.announcement ||
          n.type == NotificationType.general,
  };
}

/// The notification inbox: unread first under "New", the rest under
/// "Earlier", each with a link to where it leads.
///
/// Opening an item marks it read; "Mark all read" clears the lot.
class NotificationsScreen extends ConsumerStatefulWidget {
  const NotificationsScreen({super.key});

  @override
  ConsumerState<NotificationsScreen> createState() =>
      _NotificationsScreenState();
}

class _NotificationsScreenState extends ConsumerState<NotificationsScreen> {
  _Category _category = _Category.all;

  @override
  void initState() {
    super.initState();
    // Arriving here always starts from everything.
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (mounted && ref.read(unreadOnlyFilterProvider)) {
        ref.read(unreadOnlyFilterProvider.notifier).toggle();
      }
    });
  }

  void _select(_Category category) {
    final bool wantUnread = category == _Category.unread;
    if (ref.read(unreadOnlyFilterProvider) != wantUnread) {
      ref.read(unreadOnlyFilterProvider.notifier).toggle();
    }
    setState(() => _category = category);
  }

  Future<void> _markAllAsRead() async {
    final bool succeeded = await ref
        .read(notificationsControllerProvider.notifier)
        .markAllAsRead();

    if (!mounted) {
      return;
    }
    if (succeeded) {
      AppSnackBar.success(context, 'All notifications marked as read.');
    } else {
      AppSnackBar.error(
        context,
        'Could not mark them as read. Please try again.',
      );
    }
  }

  void _open(AppNotification notification) {
    // Marking read is fire-and-forget with an optimistic local update, so
    // navigation is not held up by the request.
    ref.read(notificationsControllerProvider.notifier).markAsRead(notification);
    context.pushNamed(
      RouteNames.notificationDetail,
      pathParameters: <String, String>{
        RouteParams.notificationId: notification.id,
      },
    );
  }

  void _follow(AppNotification notification, NotificationDestination to) {
    ref.read(notificationsControllerProvider.notifier).markAsRead(notification);
    context.pushNamed(to.name, pathParameters: to.params);
  }

  @override
  Widget build(BuildContext context) {
    final AsyncValue<PaginatedState<AppNotification>> notifications = ref.watch(
      notificationsControllerProvider,
    );
    final AppConfig config = ref.watch(appConfigProvider);
    final List<AppNotification> loaded =
        notifications.value?.items ?? const <AppNotification>[];
    final int unread =
        ref.watch(unreadNotificationCountProvider).value ??
        loaded.where((AppNotification n) => n.isUnread).length;

    return AnnotatedRegion<SystemUiOverlayStyle>(
      value: SystemUiOverlayStyle.light,
      child: Scaffold(
        body: Column(
          children: <Widget>[
            if (config.useMockData)
              const SafeArea(
                bottom: false,
                child: MockDataBanner(
                  compact: true,
                  message:
                      'Development build · sample data, not from the HR '
                      'system',
                ),
              ),
            Expanded(
              child: MediaQuery.removePadding(
                context: context,
                removeTop: config.useMockData,
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.stretch,
                  children: <Widget>[
                    _Header(unread: unread, onMarkAllRead: _markAllAsRead),
                    _FilterRow(
                      selected: _category,
                      unread: unread,
                      onSelect: _select,
                    ),
                    Expanded(
                      child: AsyncValueView<PaginatedState<AppNotification>>(
                        value: notifications,
                        loadingMessages: const <String>[
                          'Checking for updates…',
                          'Fetching notifications…',
                          'Almost there…',
                        ],
                        loading: const PaginatedListSkeleton(lines: 3),
                        onRetry: () =>
                            ref.invalidate(notificationsControllerProvider),
                        data: _buildList,
                      ),
                    ),
                  ],
                ),
              ),
            ),
          ],
        ),
      ),
    );
  }

  Widget _buildList(PaginatedState<AppNotification> state) {
    final List<AppNotification> visible = state.items
        .where(_category.matches)
        .toList(growable: false);
    final List<AppNotification> fresh = visible
        .where((AppNotification n) => n.isUnread)
        .toList(growable: false);
    final List<AppNotification> earlier = visible
        .where((AppNotification n) => !n.isUnread)
        .toList(growable: false);

    Future<void> refresh() =>
        ref.read(notificationsControllerProvider.notifier).refresh();

    if (visible.isEmpty) {
      return RefreshIndicator(
        onRefresh: refresh,
        child: CustomScrollView(
          physics: const AlwaysScrollableScrollPhysics(),
          slivers: <Widget>[
            SliverFillRemaining(hasScrollBody: false, child: _empty()),
          ],
        ),
      );
    }

    Widget group(List<AppNotification> items) => _GroupedCard(
      children: <Widget>[
        for (final AppNotification n in items)
          NotificationTile(
            notification: n,
            onTap: () => _open(n),
            onOpenLink: (NotificationDestination to) => _follow(n, to),
          ),
      ],
    );

    return RefreshIndicator(
      onRefresh: refresh,
      child: ListView(
        physics: const AlwaysScrollableScrollPhysics(),
        padding: EdgeInsets.fromLTRB(
          AppSpacing.lg,
          AppSpacing.sm,
          AppSpacing.lg,
          AppSpacing.xxxl,
        ),
        children: <Widget>[
          if (fresh.isNotEmpty) ...<Widget>[
            const _SectionLabel('New'),
            group(fresh),
          ],
          if (earlier.isNotEmpty) ...<Widget>[
            if (fresh.isNotEmpty) Gap.lg,
            const _SectionLabel('Earlier'),
            group(earlier),
          ],
          if (state.hasMore) ...<Widget>[
            Gap.sm,
            Center(
              child: TextButton(
                onPressed: state.isLoadingMore
                    ? null
                    : () => ref
                          .read(notificationsControllerProvider.notifier)
                          .loadMore(),
                child: Text(
                  state.isLoadingMore ? 'Loading…' : 'Load older notifications',
                ),
              ),
            ),
          ],
        ],
      ),
    );
  }

  Widget _empty() {
    return switch (_category) {
      _Category.all => const AppEmptyView(
        title: 'No notifications',
        message:
            'Announcements, approvals and attendance notices will appear '
            'here.',
        icon: Icons.notifications_none_rounded,
      ),
      _Category.unread => AppEmptyView(
        title: "You're all caught up",
        message: 'Nothing new since you last looked.',
        icon: Icons.mark_email_read_outlined,
        actionLabel: 'Show all',
        onAction: () => _select(_Category.all),
      ),
      _ => AppEmptyView(
        title: 'No ${_category.label.toLowerCase()} notifications',
        message: 'Nothing of this kind has arrived yet.',
        icon: Icons.notifications_none_rounded,
        actionLabel: 'Show all',
        onAction: () => _select(_Category.all),
      ),
    };
  }
}

/// Navy band: back, title, how many are unread, and Mark all read.
class _Header extends StatelessWidget {
  const _Header({required this.unread, required this.onMarkAllRead});

  final int unread;
  final VoidCallback onMarkAllRead;

  @override
  Widget build(BuildContext context) {
    const Color onBrand = AppColors.textOnPrimary;
    final TextTheme textStyles = Theme.of(context).textTheme;
    final bool canMark = unread > 0;

    return ColoredBox(
      color: AppColors.apexNavy,
      child: SafeArea(
        bottom: false,
        child: Padding(
          padding: EdgeInsets.fromLTRB(
            AppSpacing.lg,
            AppSpacing.sm,
            AppSpacing.lg,
            AppSpacing.md,
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
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  mainAxisSize: MainAxisSize.min,
                  children: <Widget>[
                    Text(
                      'Notifications',
                      style: textStyles.titleLarge?.copyWith(
                        color: onBrand,
                        fontWeight: FontWeight.w800,
                      ),
                    ),
                    Text(
                      canMark ? '$unread unread' : 'All caught up',
                      style: textStyles.bodySmall?.copyWith(
                        color: onBrand.withValues(alpha: 0.72),
                      ),
                    ),
                  ],
                ),
              ),
              Opacity(
                opacity: canMark ? 1 : 0.45,
                child: Material(
                  color: onBrand.withValues(alpha: 0.12),
                  shape: StadiumBorder(
                    side: BorderSide(color: onBrand.withValues(alpha: 0.16)),
                  ),
                  clipBehavior: Clip.antiAlias,
                  child: InkWell(
                    onTap: canMark ? onMarkAllRead : null,
                    child: Padding(
                      padding: EdgeInsets.symmetric(
                        horizontal: AppSpacing.md,
                        vertical: AppSpacing.sm,
                      ),
                      child: Row(
                        mainAxisSize: MainAxisSize.min,
                        children: <Widget>[
                          const Icon(
                            Icons.done_all_rounded,
                            size: 16,
                            color: onBrand,
                          ),
                          Gap.wXs,
                          Text(
                            'Mark all read',
                            style: textStyles.labelMedium?.copyWith(
                              color: onBrand,
                              fontWeight: FontWeight.w700,
                            ),
                          ),
                        ],
                      ),
                    ),
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

/// All / Unread (with its count) / and the kinds of notification.
class _FilterRow extends StatelessWidget {
  const _FilterRow({
    required this.selected,
    required this.unread,
    required this.onSelect,
  });

  final _Category selected;
  final int unread;
  final ValueChanged<_Category> onSelect;

  @override
  Widget build(BuildContext context) {
    final AppPalette palette = context.palette;
    final TextTheme textStyles = Theme.of(context).textTheme;

    return SizedBox(
      height: 56,
      child: ListView.separated(
        scrollDirection: Axis.horizontal,
        padding: EdgeInsets.symmetric(
          horizontal: AppSpacing.lg,
          vertical: AppSpacing.sm + 2,
        ),
        itemCount: _Category.values.length,
        separatorBuilder: (BuildContext context, int index) => Gap.wSm,
        itemBuilder: (BuildContext context, int index) {
          final _Category option = _Category.values[index];
          final bool isSelected = option == selected;

          return Semantics(
            button: true,
            selected: isSelected,
            child: Material(
              color: isSelected ? AppColors.apexNavy : AppColors.surface,
              shape: StadiumBorder(
                side: BorderSide(
                  color: isSelected ? AppColors.apexNavy : palette.border,
                ),
              ),
              clipBehavior: Clip.antiAlias,
              child: InkWell(
                onTap: () => onSelect(option),
                child: Padding(
                  padding: EdgeInsets.symmetric(horizontal: AppSpacing.md),
                  child: Row(
                    mainAxisSize: MainAxisSize.min,
                    children: <Widget>[
                      Text(
                        option.label,
                        style: textStyles.labelMedium?.copyWith(
                          color: isSelected
                              ? AppColors.textOnPrimary
                              : palette.textPrimary,
                          fontWeight: FontWeight.w700,
                        ),
                      ),
                      if (option == _Category.unread && unread > 0) ...<Widget>[
                        const SizedBox(width: 6),
                        Container(
                          padding: const EdgeInsets.symmetric(
                            horizontal: 6,
                            vertical: 1,
                          ),
                          decoration: BoxDecoration(
                            color: isSelected
                                ? AppColors.textOnPrimary.withValues(alpha: 0.2)
                                : AppColors.summitTeal.withValues(alpha: 0.14),
                            borderRadius: BorderRadius.circular(AppRadius.pill),
                          ),
                          child: Text(
                            '$unread',
                            style: textStyles.labelSmall?.copyWith(
                              fontSize: 10,
                              fontWeight: FontWeight.w800,
                              color: isSelected
                                  ? AppColors.textOnPrimary
                                  : Color.lerp(
                                      AppColors.summitTeal,
                                      Colors.black,
                                      0.2,
                                    ),
                            ),
                          ),
                        ),
                      ],
                    ],
                  ),
                ),
              ),
            ),
          );
        },
      ),
    );
  }
}

class _SectionLabel extends StatelessWidget {
  const _SectionLabel(this.text);

  final String text;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: EdgeInsets.only(left: AppSpacing.xs, bottom: AppSpacing.sm),
      child: Text(
        text.toUpperCase(),
        style: Theme.of(context).textTheme.labelSmall?.copyWith(
          letterSpacing: 1,
          fontWeight: FontWeight.w800,
          color: context.palette.textTertiary,
        ),
      ),
    );
  }
}

/// One white card holding several rows, divided by hairlines.
class _GroupedCard extends StatelessWidget {
  const _GroupedCard({required this.children});

  final List<Widget> children;

  @override
  Widget build(BuildContext context) {
    final BorderRadius radius = BorderRadius.circular(AppRadius.xl);

    return DecoratedBox(
      decoration: BoxDecoration(
        borderRadius: radius,
        boxShadow: const <BoxShadow>[
          BoxShadow(
            color: AppColors.shadow,
            blurRadius: 16,
            offset: Offset(0, 4),
          ),
        ],
      ),
      child: ClipRRect(
        borderRadius: radius,
        child: Column(
          children: <Widget>[
            for (int i = 0; i < children.length; i++) ...<Widget>[
              children[i],
              if (i < children.length - 1)
                const Divider(height: 1, color: AppColors.divider),
            ],
          ],
        ),
      ),
    );
  }
}
