/// Reads what the host can tell us about a file path.
///
/// A browser hands out opaque blob handles rather than paths it will let the
/// app stat, so every caller has to cope with "unknown" as well as with a
/// size — see `canInspectLocalFiles`.
library;

export 'local_file_web.dart' if (dart.library.io) 'local_file_io.dart';
