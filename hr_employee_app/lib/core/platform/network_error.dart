/// Recognises the host-level "cannot reach the server" error.
///
/// Dio reports some connection failures as `DioExceptionType.unknown` with
/// the platform error attached, and the platform error type differs per
/// host — `SocketException` on a device, nothing of the kind in a browser.
/// The check lives behind a conditional import so `dart:io` is never named in
/// code that also compiles for the web.
library;

export 'network_error_web.dart' if (dart.library.io) 'network_error_io.dart';
