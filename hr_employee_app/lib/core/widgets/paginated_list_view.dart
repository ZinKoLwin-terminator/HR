import 'package:hr_employee_app/app/theme/app_spacing.dart';
import 'package:hr_employee_app/core/constants/app_constants.dart';
import 'package:hr_employee_app/core/errors/failure.dart';
import 'package:hr_employee_app/core/widgets/app_button.dart';
import 'package:hr_employee_app/core/widgets/app_empty_view.dart';
import 'package:hr_employee_app/core/widgets/app_error_view.dart';
import 'package:hr_employee_app/core/widgets/app_loading_indicator.dart';
import 'package:material_ui/material_ui.dart';

/// An infinite-scrolling, pull-to-refresh list.
///
/// Four screens need exactly this behaviour — attendance history, leave
/// requests, payslips and notifications — so it lives here once.
///
/// Loading the next page is driven by a [NotificationListener] on scroll
/// extent rather than by a sentinel item at the end of the list. That way the
/// request starts while the user is still a screen away from the bottom,
/// which is what makes the scroll feel continuous.
class PaginatedListView<T> extends StatefulWidget {
  const PaginatedListView({
    required this.items,
    required this.itemBuilder,
    required this.onRefresh,
    required this.onLoadMore,
    required this.hasMore,
    super.key,
    this.isLoadingMore = false,
    this.loadMoreFailure,
    this.emptyTitle = 'Nothing here yet',
    this.emptyMessage,
    this.emptyIcon = Icons.inbox_outlined,
    this.emptyActionLabel,
    this.emptyActionIcon,
    this.onEmptyAction,
    this.separator,
    this.padding,
    this.header,
  });

  final List<T> items;
  final Widget Function(BuildContext context, T item, int index) itemBuilder;

  /// Reloads from page 1.
  final Future<void> Function() onRefresh;

  /// Requests the next page. Must be safe to call more than once; the widget
  /// guards against repeats but the controller should too.
  final void Function() onLoadMore;

  final bool hasMore;
  final bool isLoadingMore;

  /// Set when the *next page* failed. The already-loaded items stay on screen
  /// and a retry affordance appears at the end of the list.
  final Failure? loadMoreFailure;

  final String emptyTitle;
  final String? emptyMessage;
  final IconData emptyIcon;

  /// The action that would fill the empty list, such as "Apply leave".
  final String? emptyActionLabel;
  final IconData? emptyActionIcon;
  final VoidCallback? onEmptyAction;

  final Widget? separator;
  final EdgeInsetsGeometry? padding;

  /// Rendered above the first item and scrolls with the list.
  final Widget? header;

  @override
  State<PaginatedListView<T>> createState() => _PaginatedListViewState<T>();
}

class _PaginatedListViewState<T> extends State<PaginatedListView<T>> {
  bool _hasRequestedForThisExtent = false;

  bool get _canLoadMore =>
      widget.hasMore && !widget.isLoadingMore && widget.loadMoreFailure == null;

  void _onScroll(ScrollMetrics metrics) {
    final double remaining = metrics.maxScrollExtent - metrics.pixels;

    if (remaining > AppConstants.paginationTriggerExtent) {
      // Scrolled back up; arm the trigger again.
      _hasRequestedForThisExtent = false;
      return;
    }
    if (_hasRequestedForThisExtent || !_canLoadMore) {
      return;
    }
    _hasRequestedForThisExtent = true;
    widget.onLoadMore();
  }

  @override
  void didUpdateWidget(covariant PaginatedListView<T> oldWidget) {
    super.didUpdateWidget(oldWidget);
    // A new page arrived, so the next trigger is allowed.
    if (oldWidget.items.length != widget.items.length) {
      _hasRequestedForThisExtent = false;
    }
  }

  @override
  Widget build(BuildContext context) {
    if (widget.items.isEmpty) {
      // Still scrollable, so pull-to-refresh works on an empty list.
      return RefreshIndicator(
        onRefresh: widget.onRefresh,
        child: CustomScrollView(
          slivers: <Widget>[
            if (widget.header != null) SliverToBoxAdapter(child: widget.header),
            SliverFillRemaining(
              hasScrollBody: false,
              child: AppEmptyView(
                title: widget.emptyTitle,
                message: widget.emptyMessage,
                icon: widget.emptyIcon,
                actionLabel: widget.emptyActionLabel,
                actionIcon: widget.emptyActionIcon,
                onAction: widget.onEmptyAction,
              ),
            ),
          ],
        ),
      );
    }

    final int footerCount =
        (widget.isLoadingMore || widget.loadMoreFailure != null) ? 1 : 0;

    return RefreshIndicator(
      onRefresh: widget.onRefresh,
      child: NotificationListener<ScrollNotification>(
        onNotification: (ScrollNotification notification) {
          // Ignore notifications from nested scrollables such as a horizontal
          // carousel inside a row.
          if (notification.depth == 0) {
            _onScroll(notification.metrics);
          }
          return false;
        },
        child: ListView.separated(
          padding: widget.padding ?? AppSpacing.screenPadding,
          physics: const AlwaysScrollableScrollPhysics(),
          itemCount:
              widget.items.length +
              footerCount +
              (widget.header != null ? 1 : 0),
          separatorBuilder: (BuildContext context, int index) =>
              widget.separator ?? SizedBox(height: AppSpacing.cardGap),
          itemBuilder: (BuildContext context, int index) {
            int cursor = index;

            if (widget.header != null) {
              if (cursor == 0) {
                return widget.header!;
              }
              cursor -= 1;
            }

            if (cursor < widget.items.length) {
              return widget.itemBuilder(context, widget.items[cursor], cursor);
            }

            return _ListFooter(
              isLoading: widget.isLoadingMore,
              failure: widget.loadMoreFailure,
              onRetry: widget.onLoadMore,
            );
          },
        ),
      ),
    );
  }
}

/// The spinner or retry prompt shown under the last loaded item.
class _ListFooter extends StatelessWidget {
  const _ListFooter({
    required this.isLoading,
    required this.failure,
    required this.onRetry,
  });

  final bool isLoading;
  final Failure? failure;
  final VoidCallback onRetry;

  @override
  Widget build(BuildContext context) {
    if (failure != null) {
      return Padding(
        padding: EdgeInsets.symmetric(vertical: AppSpacing.lg),
        child: Column(
          children: <Widget>[
            Text(
              failure!.message,
              style: Theme.of(context).textTheme.bodySmall,
              textAlign: TextAlign.center,
            ),
            Gap.sm,
            AppButton.text(
              label: 'Retry',
              icon: Icons.refresh_rounded,
              onPressed: onRetry,
            ),
          ],
        ),
      );
    }

    if (isLoading) {
      return Padding(
        padding: EdgeInsets.symmetric(vertical: AppSpacing.xl),
        child: const AppLoadingIndicator(size: 22),
      );
    }

    return const SizedBox.shrink();
  }
}

/// The first-load skeleton for a paginated screen.
class PaginatedListSkeleton extends StatelessWidget {
  const PaginatedListSkeleton({super.key, this.itemCount = 5, this.lines = 3});

  final int itemCount;
  final int lines;

  @override
  Widget build(BuildContext context) {
    return ListView.separated(
      padding: AppSpacing.screenPadding,
      itemCount: itemCount,
      physics: const NeverScrollableScrollPhysics(),
      separatorBuilder: (BuildContext context, int index) =>
          SizedBox(height: AppSpacing.cardGap),
      itemBuilder: (BuildContext context, int index) =>
          AppSkeletonCard(lines: lines),
    );
  }
}

/// Full-screen error for a paginated screen whose *first* page failed.
class PaginatedListError extends StatelessWidget {
  const PaginatedListError({
    required this.failure,
    required this.onRetry,
    super.key,
  });

  final Failure failure;
  final VoidCallback onRetry;

  @override
  Widget build(BuildContext context) =>
      AppErrorView(failure: failure, onRetry: onRetry);
}
