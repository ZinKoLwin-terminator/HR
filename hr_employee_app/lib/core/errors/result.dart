import 'package:hr_employee_app/core/errors/failure.dart';

/// The return type of every repository and use case.
///
/// Using an explicit result instead of throwing keeps error handling visible
/// in the signature and forces callers to deal with both branches. A tiny
/// hand-rolled type is used in preference to a functional-programming package
/// so the codebase keeps one obvious way to express "succeeded or failed".
sealed class Result<T> {
  const Result();

  /// Wraps a successful value.
  const factory Result.success(T value) = Success<T>;

  /// Wraps a [Failure].
  const factory Result.failure(Failure failure) = Error<T>;

  bool get isSuccess => this is Success<T>;

  bool get isFailure => this is Error<T>;

  /// The value when successful, otherwise null.
  T? get valueOrNull => switch (this) {
    Success<T>(:final T value) => value,
    Error<T>() => null,
  };

  /// The failure when unsuccessful, otherwise null.
  Failure? get failureOrNull => switch (this) {
    Success<T>() => null,
    Error<T>(:final Failure failure) => failure,
  };

  /// Collapses both branches into a single value.
  R fold<R>({
    required R Function(T value) onSuccess,
    required R Function(Failure failure) onFailure,
  }) {
    return switch (this) {
      Success<T>(:final T value) => onSuccess(value),
      Error<T>(:final Failure failure) => onFailure(failure),
    };
  }

  /// Transforms a successful value, leaving a failure untouched.
  Result<R> map<R>(R Function(T value) transform) {
    return switch (this) {
      Success<T>(:final T value) => Result<R>.success(transform(value)),
      Error<T>(:final Failure failure) => Result<R>.failure(failure),
    };
  }

  /// Chains another result-producing operation onto a successful value.
  Result<R> flatMap<R>(Result<R> Function(T value) transform) {
    return switch (this) {
      Success<T>(:final T value) => transform(value),
      Error<T>(:final Failure failure) => Result<R>.failure(failure),
    };
  }

  /// The value when successful, otherwise [fallback].
  T getOrElse(T fallback) => valueOrNull ?? fallback;
}

/// A successful [Result].
final class Success<T> extends Result<T> {
  const Success(this.value);

  final T value;

  @override
  bool operator ==(Object other) =>
      identical(this, other) || other is Success<T> && value == other.value;

  @override
  int get hashCode => Object.hash(Success<T>, value);

  @override
  String toString() => 'Success<$T>($value)';
}

/// A failed [Result].
///
/// Named `Error` rather than `Failure` so it does not clash with the
/// [Failure] payload it carries.
final class Error<T> extends Result<T> {
  const Error(this.failure);

  final Failure failure;

  @override
  bool operator ==(Object other) =>
      identical(this, other) || other is Error<T> && failure == other.failure;

  @override
  int get hashCode => Object.hash(Error<T>, failure);

  @override
  String toString() => 'Error<$T>($failure)';
}

/// Bridges a [Result] into Riverpod's `AsyncValue`.
extension ResultThrowX<T> on Result<T> {
  /// The value, or the [Failure] **thrown**.
  ///
  /// This is the one place in the app where a failure is deliberately turned
  /// back into a thrown object, and it exists because that is the contract
  /// `AsyncNotifier` works with: whatever `build()` throws becomes
  /// `AsyncValue.error`. Throwing the `Failure` itself — rather than an
  /// exception or a string — is what lets `AsyncValueView` render a typed,
  /// user-ready error.
  ///
  /// Only controllers call this. Repositories and use cases keep returning
  /// `Result`.
  T valueOrThrow() {
    return switch (this) {
      Success<T>(:final T value) => value,
      Error<T>(:final Failure failure) => throw failure,
    };
  }
}

/// Convenience helpers for asynchronous results.
extension FutureResultX<T> on Future<Result<T>> {
  /// Awaits the result and collapses both branches.
  Future<R> foldAsync<R>({
    required R Function(T value) onSuccess,
    required R Function(Failure failure) onFailure,
  }) async {
    final Result<T> result = await this;
    return result.fold(onSuccess: onSuccess, onFailure: onFailure);
  }
}
