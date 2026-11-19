import 'package:hr_employee_app/core/exceptions/app_exception.dart';
import 'package:hr_employee_app/shared/models/api_response.dart';

/// One page of a list endpoint.
///
/// See `docs/api_contracts.md` §1.3. Page numbers are 1-based.
///
/// Hand-written for the same reason as [ApiResponse]: it is generic over the
/// item type.
class PaginatedResponse<T> {
  const PaginatedResponse({
    required this.items,
    required this.pageNumber,
    required this.pageSize,
    required this.totalCount,
    required this.totalPages,
    required this.hasNextPage,
    required this.hasPreviousPage,
  });

  factory PaginatedResponse.fromJson(
    Object? json,
    T Function(Map<String, dynamic>) fromItem,
  ) {
    final Map<String, dynamic> map = asJsonObject(json);
    final Object? rawItems = map['items'];
    if (rawItems is! List) {
      throw const ParsingException();
    }

    final int pageNumber = (map['pageNumber'] as num?)?.toInt() ?? 1;
    final int pageSize = (map['pageSize'] as num?)?.toInt() ?? rawItems.length;
    final int totalCount =
        (map['totalCount'] as num?)?.toInt() ?? rawItems.length;
    final int totalPages =
        (map['totalPages'] as num?)?.toInt() ??
        (pageSize == 0 ? 1 : (totalCount / pageSize).ceil());

    return PaginatedResponse<T>(
      items: rawItems
          .map((Object? item) => fromItem(asJsonObject(item)))
          .toList(growable: false),
      pageNumber: pageNumber,
      pageSize: pageSize,
      totalCount: totalCount,
      totalPages: totalPages,
      // Derived when absent so a backend that omits the flags still paginates.
      hasNextPage: map['hasNextPage'] as bool? ?? pageNumber < totalPages,
      hasPreviousPage: map['hasPreviousPage'] as bool? ?? pageNumber > 1,
    );
  }

  /// An empty first page, used as the initial state of a paginated list.
  const PaginatedResponse.empty()
    : items = const <Never>[],
      pageNumber = 0,
      pageSize = 0,
      totalCount = 0,
      totalPages = 0,
      hasNextPage = true,
      hasPreviousPage = false;

  final List<T> items;
  final int pageNumber;
  final int pageSize;
  final int totalCount;
  final int totalPages;
  final bool hasNextPage;
  final bool hasPreviousPage;

  bool get isEmpty => items.isEmpty;

  /// Maps the items while preserving the page metadata.
  PaginatedResponse<R> mapItems<R>(R Function(T item) transform) {
    return PaginatedResponse<R>(
      items: items.map(transform).toList(growable: false),
      pageNumber: pageNumber,
      pageSize: pageSize,
      totalCount: totalCount,
      totalPages: totalPages,
      hasNextPage: hasNextPage,
      hasPreviousPage: hasPreviousPage,
    );
  }
}

/// The query half of a paginated request.
class PageRequest {
  const PageRequest({this.pageNumber = 1, this.pageSize = 20});

  final int pageNumber;
  final int pageSize;

  /// The request for the page after this one.
  PageRequest next() =>
      PageRequest(pageNumber: pageNumber + 1, pageSize: pageSize);

  /// The first page, used when a list is pulled to refresh.
  PageRequest first() => PageRequest(pageSize: pageSize);

  @override
  bool operator ==(Object other) =>
      identical(this, other) ||
      other is PageRequest &&
          pageNumber == other.pageNumber &&
          pageSize == other.pageSize;

  @override
  int get hashCode => Object.hash(pageNumber, pageSize);
}
