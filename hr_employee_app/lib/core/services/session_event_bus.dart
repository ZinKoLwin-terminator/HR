import 'dart:async';

/// Events that affect the whole signed-in session.
enum SessionEvent {
  /// The refresh token was rejected or has lapsed. Every listener should drop
  /// cached user data; the router sends the user back to the login screen.
  expired,

  /// The user signed out deliberately.
  signedOut,
}

/// A one-way channel from the networking layer up to the auth feature.
///
/// The Dio interceptor discovers an unrecoverable 401 deep inside `core`, but
/// `core` must not depend on `features/auth`. Publishing an event here lets
/// the auth controller react without introducing that dependency, and keeps
/// the interceptor free of navigation concerns.
class SessionEventBus {
  final StreamController<SessionEvent> _controller =
      StreamController<SessionEvent>.broadcast();

  Stream<SessionEvent> get stream => _controller.stream;

  void emit(SessionEvent event) {
    if (!_controller.isClosed) {
      _controller.add(event);
    }
  }

  Future<void> dispose() => _controller.close();
}
