import 'package:hr_employee_app/core/errors/failure.dart';
import 'package:hr_employee_app/shared/models/paginated_response.dart';

/// UI state for an infinite-scrolling list.
///
/// Separate from [PaginatedResponse], which is one page from the server. This
/// is the accumulated list plus the bookkeeping the screen needs: whether
/// another page exists, whether one is in flight, and whether the *last page
/// request* failed.
///
/// That last distinction is the point of the type. A failure loading page 5
/// must not discard pages 1-4; it shows a retry row at the end of the list
/// while everything already loaded stays on screen. Folding that into
/// `AsyncValue.error` would replace the whole list with an error page.
///
/// Hand-written rather than generated because it is generic over its item
/// type, like the other two paginated types.
class PaginatedState<T> {
  /// Builds the state for a freshly loaded first page.
  ///
  /// A factory rather than a static method so it can be called as
  /// `PaginatedState<T>.fromFirstPage(...)`; Dart does not allow reaching a
  /// static member through a parameterised type.
  factory PaginatedState.fromFirstPage(
    PaginatedResponse<T> response, {
    required int pageSize,
  }) {
    return PaginatedState<T>(
      items: response.items,
      lastLoadedPage: response.pageNumber,
      pageSize: pageSize,
      totalCount: response.totalCount,
      hasMore: response.hasNextPage,
    );
  }

  const PaginatedState({
    this.items = const <Never>[],
    this.lastLoadedPage = 0,
    this.pageSize = 20,
    this.totalCount = 0,
    this.hasMore = true,
    this.isLoadingMore = false,
    this.loadMoreFailure,
  });

  /// Everything loaded so far, in server order.
  final List<T> items;

  /// 1-based number of the most recently loaded page; 0 before the first.
  final int lastLoadedPage;

  final int pageSize;
  final int totalCount;

  /// Whether the server says there is another page.
  final bool hasMore;

  /// Whether the next page is being fetched.
  final bool isLoadingMore;

  /// Set when the most recent *next-page* request failed.
  final Failure? loadMoreFailure;

  bool get isEmpty => items.isEmpty;

  /// The request for the next page.
  PageRequest get nextPageRequest =>
      PageRequest(pageNumber: lastLoadedPage + 1, pageSize: pageSize);

  /// The request for the first page, used on refresh.
  PageRequest get firstPageRequest => PageRequest(pageSize: pageSize);

  /// Appends a newly loaded page.
  ///
  /// Items already present are skipped: a record inserted server-side between
  /// two page requests shifts the window and can otherwise deliver the same
  /// row twice, which breaks list keys.
  PaginatedState<T> appendPage(
    PaginatedResponse<T> response, {
    required Object Function(T item) identity,
  }) {
    final Set<Object> seen = items.map(identity).toSet();
    final List<T> fresh = response.items
        .where((T item) => seen.add(identity(item)))
        .toList();

    return PaginatedState<T>(
      items: <T>[...items, ...fresh],
      lastLoadedPage: response.pageNumber,
      pageSize: pageSize,
      totalCount: response.totalCount,
      hasMore: response.hasNextPage,
    );
  }

  /// Replaces one item in place, matched by [identity].
  ///
  /// Used after an action changes a single row — cancelling a leave request,
  /// marking a notification read — so the list updates without a refetch.
  PaginatedState<T> replaceItem(
    T updated, {
    required Object Function(T item) identity,
  }) {
    final Object target = identity(updated);
    return copyWith(
      items: items
          .map((T item) => identity(item) == target ? updated : item)
          .toList(),
    );
  }

  PaginatedState<T> copyWith({
    List<T>? items,
    int? lastLoadedPage,
    int? pageSize,
    int? totalCount,
    bool? hasMore,
    bool? isLoadingMore,
    Failure? loadMoreFailure,

    /// Set to clear [loadMoreFailure], which `copyWith` cannot otherwise do
    /// because null means "unchanged".
    bool clearLoadMoreFailure = false,
  }) {
    return PaginatedState<T>(
      items: items ?? this.items,
      lastLoadedPage: lastLoadedPage ?? this.lastLoadedPage,
      pageSize: pageSize ?? this.pageSize,
      totalCount: totalCount ?? this.totalCount,
      hasMore: hasMore ?? this.hasMore,
      isLoadingMore: isLoadingMore ?? this.isLoadingMore,
      loadMoreFailure: clearLoadMoreFailure
          ? null
          : (loadMoreFailure ?? this.loadMoreFailure),
    );
  }
}
