import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:hr_employee_app/core/constants/app_constants.dart';
import 'package:hr_employee_app/core/errors/result.dart';
import 'package:hr_employee_app/shared/models/paginated_response.dart';
import 'package:hr_employee_app/shared/models/paginated_state.dart';

/// Base controller for an infinite-scrolling list.
///
/// Four screens page through server data — attendance history, leave
/// requests, payslips and notifications — and the logic is identical every
/// time: load page 1, append page N, keep the loaded items when page N fails,
/// never fire two requests at once. Writing that four times is four chances
/// to get the concurrency guard wrong, so it lives here.
///
/// A subclass supplies two things:
///   * [fetchPage] — how to get one page;
///   * [identityOf] — a stable id per item, used to drop duplicates when the
///     server's window shifts between requests.
abstract class PaginatedListNotifier<T>
    extends AsyncNotifier<PaginatedState<T>> {
  /// Items per request. Override for a naturally different page size, such as
  /// twelve months of payslips.
  int get pageSize => AppConstants.defaultPageSize;

  /// Fetches one page.
  Future<Result<PaginatedResponse<T>>> fetchPage(PageRequest page);

  /// A stable identity for [item], used for de-duplication and in-place
  /// replacement.
  Object identityOf(T item);

  @override
  Future<PaginatedState<T>> build() async {
    final Result<PaginatedResponse<T>> result = await fetchPage(
      PageRequest(pageSize: pageSize),
    );
    // A first-page failure is thrown so the screen can show a full-page error;
    // there is nothing loaded yet to preserve.
    final PaginatedResponse<T> response = result.valueOrThrow();
    return PaginatedState<T>.fromFirstPage(response, pageSize: pageSize);
  }

  /// Loads the next page, if there is one.
  ///
  /// Safe to call repeatedly: the guard below means a second call while a
  /// request is in flight is ignored, which matters because scroll listeners
  /// fire often.
  Future<void> loadMore() async {
    final PaginatedState<T>? current = state.value;
    if (current == null ||
        !current.hasMore ||
        current.isLoadingMore ||
        current.loadMoreFailure != null) {
      return;
    }

    state = AsyncValue<PaginatedState<T>>.data(
      current.copyWith(isLoadingMore: true, clearLoadMoreFailure: true),
    );

    final Result<PaginatedResponse<T>> result = await fetchPage(
      current.nextPageRequest,
    );

    // The provider may have been disposed, or refreshed from page 1, while the
    // request was in flight.
    final PaginatedState<T>? latest = state.value;
    if (latest == null) {
      return;
    }

    state = AsyncValue<PaginatedState<T>>.data(
      result.fold(
        onSuccess: (PaginatedResponse<T> response) => latest
            .appendPage(response, identity: identityOf)
            .copyWith(isLoadingMore: false),
        // The already-loaded items stay; only a retry row is added.
        onFailure: (failure) =>
            latest.copyWith(isLoadingMore: false, loadMoreFailure: failure),
      ),
    );
  }

  /// Retries a failed next-page request.
  Future<void> retryLoadMore() async {
    final PaginatedState<T>? current = state.value;
    if (current == null) {
      return;
    }
    state = AsyncValue<PaginatedState<T>>.data(
      current.copyWith(clearLoadMoreFailure: true),
    );
    await loadMore();
  }

  /// Reloads from page 1, discarding what is loaded.
  Future<void> refresh() async {
    ref.invalidateSelf();
    await future;
  }

  /// Replaces one item in place, for an action that changed a single row.
  void replaceItem(T updated) {
    final PaginatedState<T>? current = state.value;
    if (current == null) {
      return;
    }
    state = AsyncValue<PaginatedState<T>>.data(
      current.replaceItem(updated, identity: identityOf),
    );
  }

  /// Applies [transform] to every item, for a bulk action such as
  /// "mark all as read".
  void updateAllItems(T Function(T item) transform) {
    final PaginatedState<T>? current = state.value;
    if (current == null) {
      return;
    }
    state = AsyncValue<PaginatedState<T>>.data(
      current.copyWith(items: current.items.map(transform).toList()),
    );
  }

  /// Inserts an item at the top, for something the user just created.
  void prependItem(T item) {
    final PaginatedState<T>? current = state.value;
    if (current == null) {
      return;
    }
    state = AsyncValue<PaginatedState<T>>.data(
      current.copyWith(
        items: <T>[item, ...current.items],
        totalCount: current.totalCount + 1,
      ),
    );
  }
}
