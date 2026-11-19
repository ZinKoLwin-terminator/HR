import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:hr_employee_app/core/errors/result.dart';
import 'package:hr_employee_app/features/auth/domain/entities/authenticated_user.dart';
import 'package:hr_employee_app/features/auth/presentation/providers/auth_controller.dart';
import 'package:hr_employee_app/features/dashboard/presentation/providers/dashboard_controller.dart';
import 'package:hr_employee_app/features/profile/domain/entities/employee_profile.dart';
import 'package:hr_employee_app/features/profile/presentation/providers/profile_providers.dart';
import 'package:hr_employee_app/shared/models/submission_state.dart';

/// The employee's own record.
class ProfileController extends AsyncNotifier<EmployeeProfile> {
  @override
  Future<EmployeeProfile> build() async {
    final Result<EmployeeProfile> result = await ref.watch(
      getProfileUseCaseProvider,
    )();
    return result.valueOrThrow();
  }

  Future<void> refresh() async {
    ref.invalidateSelf();
    await future;
  }

  /// Applies a profile the server has just returned.
  void apply(EmployeeProfile profile) {
    state = AsyncValue<EmployeeProfile>.data(profile);
  }
}

final AsyncNotifierProvider<ProfileController, EmployeeProfile>
profileControllerProvider =
    AsyncNotifierProvider<ProfileController, EmployeeProfile>(
      ProfileController.new,
    );

/// Saves the self-editable parts of the profile.
///
/// One controller for both the contact fields and the emergency contact:
/// they are edited on the same screen and saved by the same button, so
/// splitting them would mean coordinating two submission states for one user
/// action.
class EditProfileController extends Notifier<SubmissionState> {
  @override
  SubmissionState build() => const SubmissionState.idle();

  /// Saves whichever sections changed.
  ///
  /// The two requests run in sequence rather than together: the second
  /// returns the profile including the first one's changes, so the final
  /// response is the complete record. Running them concurrently would make
  /// the winner's response overwrite the loser's change in the UI.
  Future<bool> save({
    ContactUpdate? contact,
    EmergencyContact? emergencyContact,
  }) async {
    if (state.isSubmitting) {
      return false;
    }
    if (contact == null && emergencyContact == null) {
      // Nothing changed; treat as a no-op success so the screen just closes.
      state = const SubmissionState.success();
      return true;
    }

    state = const SubmissionState.submitting();

    EmployeeProfile? latest;

    if (contact != null) {
      final Result<EmployeeProfile> result = await ref.read(
        updateContactUseCaseProvider,
      )(contact);

      final EmployeeProfile? updated = result.valueOrNull;
      if (updated == null) {
        state = SubmissionState.failure(result.failureOrNull!);
        return false;
      }
      latest = updated;
    }

    if (emergencyContact != null) {
      final Result<EmployeeProfile> result = await ref.read(
        updateEmergencyContactUseCaseProvider,
      )(emergencyContact);

      final EmployeeProfile? updated = result.valueOrNull;
      if (updated == null) {
        // The contact change above may already have been saved. Reflect what
        // did succeed rather than leaving the UI showing stale values.
        if (latest != null) {
          _propagate(latest);
        }
        state = SubmissionState.failure(result.failureOrNull!);
        return false;
      }
      latest = updated;
    }

    if (latest != null) {
      _propagate(latest);
    }
    state = const SubmissionState.success(message: 'Profile updated.');
    return true;
  }

  void reset() => state = const SubmissionState.idle();

  /// Pushes the saved profile to every provider that displays part of it.
  void _propagate(EmployeeProfile profile) {
    ref.read(profileControllerProvider.notifier).apply(profile);

    // The shell's avatar and greeting come from the auth session, so update
    // that too rather than waiting for the next sign-in.
    final AuthenticatedUser? current = ref.read(currentUserProvider);
    if (current != null) {
      ref
          .read(authControllerProvider.notifier)
          .onUserUpdated(
            current.copyWith(
              fullName: profile.fullName,
              email: profile.email,
              department: profile.department,
              designation: profile.designation,
              profilePhotoUrl: profile.profilePhotoUrl,
            ),
          );
    }

    ref.invalidate(dashboardControllerProvider);
  }
}

final NotifierProvider<EditProfileController, SubmissionState>
editProfileControllerProvider =
    NotifierProvider<EditProfileController, SubmissionState>(
      EditProfileController.new,
    );

/// Uploads a new profile photo.
class ProfilePhotoController extends Notifier<SubmissionState> {
  @override
  SubmissionState build() => const SubmissionState.idle();

  Future<bool> upload(String filePath) async {
    if (state.isSubmitting) {
      return false;
    }
    state = const SubmissionState.submitting();

    final Result<EmployeeProfile> result = await ref.read(
      uploadProfilePhotoUseCaseProvider,
    )(filePath);

    return result.fold(
      onSuccess: (EmployeeProfile profile) {
        ref.read(profileControllerProvider.notifier).apply(profile);

        final AuthenticatedUser? current = ref.read(currentUserProvider);
        if (current != null) {
          ref
              .read(authControllerProvider.notifier)
              .onUserUpdated(
                current.copyWith(profilePhotoUrl: profile.profilePhotoUrl),
              );
        }
        ref.invalidate(dashboardControllerProvider);

        state = const SubmissionState.success(
          message: 'Profile photo updated.',
        );
        return true;
      },
      onFailure: (failure) {
        state = SubmissionState.failure(failure);
        return false;
      },
    );
  }

  void reset() => state = const SubmissionState.idle();
}

final NotifierProvider<ProfilePhotoController, SubmissionState>
profilePhotoControllerProvider =
    NotifierProvider<ProfilePhotoController, SubmissionState>(
      ProfilePhotoController.new,
    );
