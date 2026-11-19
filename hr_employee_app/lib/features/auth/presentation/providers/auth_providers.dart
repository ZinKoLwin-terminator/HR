import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:hr_employee_app/app/config/app_config.dart';
import 'package:hr_employee_app/core/providers/core_providers.dart';
import 'package:hr_employee_app/features/auth/data/datasources/auth_local_datasource.dart';
import 'package:hr_employee_app/features/auth/data/datasources/auth_mock_datasource.dart';
import 'package:hr_employee_app/features/auth/data/datasources/auth_remote_datasource.dart';
import 'package:hr_employee_app/features/auth/data/repositories/auth_repository_impl.dart';
import 'package:hr_employee_app/features/auth/domain/repositories/auth_repository.dart';
import 'package:hr_employee_app/features/auth/domain/usecases/auth_usecases.dart';
import 'package:hr_employee_app/features/auth/domain/usecases/login_usecase.dart';

/// Dependency injection for the auth feature.
///
/// The wiring is the same in every feature: data source -> repository ->
/// use cases. Only the data source provider ever branches, and only on
/// whether the build is in mock mode.

/// Chooses between the real API and the fixtures.
///
/// `AppConfig.useMockData` is forced to false in production builds, so the
/// mock cannot reach an employee's device.
final Provider<AuthRemoteDataSource> authRemoteDataSourceProvider =
    Provider<AuthRemoteDataSource>((Ref ref) {
      final AppConfig config = ref.watch(appConfigProvider);
      if (config.useMockData) {
        return const AuthMockDataSource();
      }
      return AuthRemoteDataSourceImpl(ref.watch(apiClientProvider));
    });

final Provider<AuthLocalDataSource> authLocalDataSourceProvider =
    Provider<AuthLocalDataSource>(
      (Ref ref) =>
          AuthLocalDataSourceImpl(ref.watch(secureStorageServiceProvider)),
    );

final Provider<AuthRepository> authRepositoryProvider =
    Provider<AuthRepository>(
      (Ref ref) => AuthRepositoryImpl(
        remote: ref.watch(authRemoteDataSourceProvider),
        local: ref.watch(authLocalDataSourceProvider),
        tokenStorage: ref.watch(tokenStorageProvider),
        logger: ref.watch(appLoggerProvider),
      ),
    );

final Provider<LoginUseCase> loginUseCaseProvider = Provider<LoginUseCase>(
  (Ref ref) => LoginUseCase(ref.watch(authRepositoryProvider)),
);

final Provider<RestoreSessionUseCase> restoreSessionUseCaseProvider =
    Provider<RestoreSessionUseCase>(
      (Ref ref) => RestoreSessionUseCase(ref.watch(authRepositoryProvider)),
    );

final Provider<LogoutUseCase> logoutUseCaseProvider = Provider<LogoutUseCase>(
  (Ref ref) => LogoutUseCase(ref.watch(authRepositoryProvider)),
);

final Provider<RequestPasswordResetUseCase>
requestPasswordResetUseCaseProvider = Provider<RequestPasswordResetUseCase>(
  (Ref ref) => RequestPasswordResetUseCase(ref.watch(authRepositoryProvider)),
);

final Provider<ResetPasswordUseCase> resetPasswordUseCaseProvider =
    Provider<ResetPasswordUseCase>(
      (Ref ref) => ResetPasswordUseCase(ref.watch(authRepositoryProvider)),
    );

final Provider<ChangePasswordUseCase> changePasswordUseCaseProvider =
    Provider<ChangePasswordUseCase>(
      (Ref ref) => ChangePasswordUseCase(ref.watch(authRepositoryProvider)),
    );
