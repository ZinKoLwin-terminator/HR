import 'package:hr_employee_app/core/errors/error_mapper.dart';
import 'package:hr_employee_app/core/errors/failure.dart';
import 'package:hr_employee_app/core/errors/result.dart';
import 'package:hr_employee_app/core/exceptions/app_exception.dart';
import 'package:hr_employee_app/core/utils/app_logger.dart';

/// The exception-to-[Result] boundary every repository sits on.
///
/// Without it each repository method repeats the same try/catch, and it only
/// takes one that forgets for an exception to escape into a widget. Mixing
/// this in makes the conversion a one-liner and guarantees the boundary is
/// never leaky:
///
/// ```dart
/// Future<Result<Profile>> getProfile() =>
///     guard(() async => (await _remote.fetchProfile()).toEntity());
/// ```
mixin RepositoryGuard {
  /// Implemented by the repository so failures are logged with its own name.
  AppLogger get logger;

  /// Runs [action], returning its value as a success or the mapped [Failure].
  ///
  /// [operation] is a short label used only for the log line, never shown to
  /// the user.
  Future<Result<T>> guard<T>(
    Future<T> Function() action, {
    required String operation,
  }) async {
    try {
      return Result<T>.success(await action());
    } on AppException catch (exception) {
      // Expected failures: logged at warning, without the payload, because
      // the payload may contain personal data.
      logger.warning('$operation failed: ${exception.runtimeType}');
      return Result<T>.failure(ErrorMapper.toFailure(exception));
    } on Object catch (error, stackTrace) {
      // Unexpected: a bug. Logged with the stack trace so it can be fixed,
      // and reported to the user as a generic failure.
      logger.error('$operation threw unexpectedly', error, stackTrace);
      return Result<T>.failure(const UnexpectedFailure());
    }
  }
}
