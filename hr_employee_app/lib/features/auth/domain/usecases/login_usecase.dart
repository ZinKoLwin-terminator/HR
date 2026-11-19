import 'package:hr_employee_app/core/errors/failure.dart';
import 'package:hr_employee_app/core/errors/result.dart';
import 'package:hr_employee_app/features/auth/domain/entities/authenticated_user.dart';
import 'package:hr_employee_app/features/auth/domain/entities/login_credentials.dart';
import 'package:hr_employee_app/features/auth/domain/repositories/auth_repository.dart';

/// Signs an employee in.
///
/// The rules that belong to the business, not to a widget or to the server,
/// live here: how an identifier is normalised before it is sent, and the
/// guard that stops an obviously empty submission from costing a round trip.
class LoginUseCase {
  const LoginUseCase(this._repository);

  final AuthRepository _repository;

  Future<Result<AuthenticatedUser>> call(LoginCredentials credentials) {
    final String userName = _normalizeUserName(credentials.userName);

    if (userName.isEmpty || credentials.password.isEmpty) {
      return Future<Result<AuthenticatedUser>>.value(
        const Result<AuthenticatedUser>.failure(
          ValidationFailure('Enter your employee ID and password.'),
        ),
      );
    }

    return _repository.login(credentials.copyWith(userName: userName));
  }

  /// Trims surrounding whitespace, and lower-cases an email so that
  /// `Aye.Chan@Example.com` and `aye.chan@example.com` are one account.
  ///
  /// An employee ID keeps its case, because the server may treat it as
  /// case-sensitive and it is not ours to decide otherwise.
  String _normalizeUserName(String raw) {
    final String trimmed = raw.trim();
    return trimmed.contains('@') ? trimmed.toLowerCase() : trimmed;
  }
}
