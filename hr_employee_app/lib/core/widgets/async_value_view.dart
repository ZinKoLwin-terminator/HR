import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:hr_employee_app/core/errors/failure.dart';
import 'package:hr_employee_app/core/widgets/app_error_view.dart';
import 'package:hr_employee_app/core/widgets/app_loading_indicator.dart';
import 'package:material_ui/material_ui.dart';

/// Renders the three states of an [AsyncValue] consistently.
///
/// Without this, every screen writes its own `.when(...)` and they drift
/// apart. With it, loading, error and data look the same everywhere and a
/// screen only supplies the part that is actually specific to it: the data.
///
/// Two behaviours worth knowing:
///   * **Refresh keeps the old data on screen.** While a refresh is in flight
///     `AsyncValue` still carries the previous value; showing a spinner over
///     the top would make a pull-to-refresh blank the page. [data] is called
///     with the previous value instead, and [showRefreshOverlay] decides
///     whether a subtle indicator is layered on top.
///   * **Errors keep stale data when there is some.** A failed refresh shows
///     the data that is still valid plus a transient message, rather than
///     replacing a working screen with a full-page error.
class AsyncValueView<T> extends StatelessWidget {
  const AsyncValueView({
    required this.value,
    required this.data,
    super.key,
    this.onRetry,
    this.loading,
    this.error,
    this.showRefreshOverlay = true,
    this.keepDataOnError = true,
  });

  final AsyncValue<T> value;

  /// Builds the populated state.
  final Widget Function(T value) data;

  /// Invoked by the error state's retry button. Omit to hide the button.
  final VoidCallback? onRetry;

  /// Overrides the default spinner, typically with a skeleton layout.
  final Widget? loading;

  /// Overrides the default error view.
  final Widget Function(Failure failure)? error;

  /// Layers a thin progress bar over stale data while it refreshes.
  final bool showRefreshOverlay;

  /// Keeps showing the last good value when a refresh fails.
  final bool keepDataOnError;

  @override
  Widget build(BuildContext context) {
    // `AsyncValue.value` keeps the previous value during a refresh or after a
    // failed reload, which is exactly what this widget relies on.
    final T? previous = value.value;
    final bool hasPrevious = value.hasValue;

    // Error, with nothing good left to show.
    if (value.hasError && (!hasPrevious || !keepDataOnError)) {
      final Failure failure = asFailure(value.error);
      return error?.call(failure) ??
          AppErrorView(failure: failure, onRetry: onRetry);
    }

    // Loading for the first time.
    if (value.isLoading && !hasPrevious) {
      return loading ?? const AppLoadingIndicator();
    }

    // From here on there is a value to render, possibly a stale one.
    final Widget content = data(previous as T);

    if (value.isLoading && showRefreshOverlay) {
      return Stack(
        children: <Widget>[
          content,
          const Positioned(
            top: 0,
            left: 0,
            right: 0,
            child: LinearProgressIndicator(minHeight: 2),
          ),
        ],
      );
    }

    return content;
  }
}

/// A [SliverList]-friendly variant, for screens built from slivers.
class SliverAsyncValueView<T> extends StatelessWidget {
  const SliverAsyncValueView({
    required this.value,
    required this.data,
    super.key,
    this.onRetry,
    this.loading,
  });

  final AsyncValue<T> value;
  final Widget Function(T value) data;
  final VoidCallback? onRetry;
  final Widget? loading;

  @override
  Widget build(BuildContext context) {
    if (value.hasError && !value.hasValue) {
      return SliverFillRemaining(
        hasScrollBody: false,
        child: AppErrorView(failure: asFailure(value.error), onRetry: onRetry),
      );
    }
    if (value.isLoading && !value.hasValue) {
      return SliverFillRemaining(
        hasScrollBody: false,
        child: loading ?? const AppLoadingIndicator(),
      );
    }
    return data(value.value as T);
  }
}

/// Narrows an arbitrary thrown object to a [Failure].
///
/// Controllers throw `Failure`, so the cast nearly always succeeds. Anything
/// else is a programming error that has escaped the repository layer; it is
/// reported as a generic failure rather than crashing the screen.
Failure asFailure(Object? error) {
  if (error is Failure) {
    return error;
  }
  return const UnexpectedFailure();
}
