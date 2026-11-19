/// Whether attendance must carry a position.
///
/// ---------------------------------------------------------------------------
/// This is tenant configuration that belongs on the **server**. There is no
/// endpoint for it yet (see question 3 in `docs/api_contracts.md` §9), so it
/// is supplied as a build-time value for now. The extension point is
/// deliberate: when the server exposes tenant settings, change
/// `attendanceLocationPolicyProvider` to read from them and nothing else in
/// the feature has to move.
/// ---------------------------------------------------------------------------
enum AttendanceLocationPolicy {
  /// Location is not collected at all. No permission is requested.
  disabled,

  /// Location is collected when available. A refusal or a failed fix does not
  /// block the punch — it is submitted without a position and the server
  /// decides what to do.
  optional,

  /// A position is mandatory. Without one the punch is not submitted, and the
  /// user is told why rather than being shown a success they did not get.
  required;

  bool get collectsLocation => this != AttendanceLocationPolicy.disabled;

  bool get isMandatory => this == AttendanceLocationPolicy.required;

  static AttendanceLocationPolicy fromName(String value) {
    return AttendanceLocationPolicy.values.firstWhere(
      (AttendanceLocationPolicy policy) => policy.name == value.toLowerCase(),
      orElse: () => AttendanceLocationPolicy.optional,
    );
  }
}
