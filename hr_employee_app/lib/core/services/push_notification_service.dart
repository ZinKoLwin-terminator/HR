import 'dart:async';

import 'package:hr_employee_app/core/utils/app_logger.dart';

/// A push message, reduced to what the app acts on.
class PushMessage {
  const PushMessage({
    required this.title,
    required this.body,
    this.notificationId,
    this.deepLink,
    this.data = const <String, String>{},
  });

  /// Builds a message from a provider's raw payload.
  ///
  /// Defensive by design: a push payload is whatever the server sent, and a
  /// malformed one must not crash the app on a cold start.
  factory PushMessage.fromData(Map<String, dynamic> payload) {
    final Map<String, String> data = <String, String>{
      for (final MapEntry<String, dynamic> entry in payload.entries)
        entry.key: '${entry.value}',
    };

    return PushMessage(
      title: data['title'] ?? '',
      body: data['body'] ?? '',
      notificationId: data['notificationId'],
      deepLink: data['deepLink'],
      data: data,
    );
  }

  final String title;
  final String body;

  /// Id of the matching row in the inbox, so a tap can open the detail
  /// screen and mark it read.
  final String? notificationId;

  /// In-app destination, e.g. `hrapp://leave/lr-5501`.
  final String? deepLink;

  final Map<String, String> data;
}

/// Delivers push notifications.
///
/// ---------------------------------------------------------------------------
/// NO PROVIDER IS WIRED UP. The app ships [NoopPushNotificationService],
/// which does nothing.
/// ---------------------------------------------------------------------------
///
/// The spec calls for a *push-ready architecture*, and this interface is it.
/// No push SDK is included because the provider has not been chosen
/// (`docs/api_contracts.md` §9, question 6) and adding Firebase — with its
/// Gradle plugin, platform config files and app-size cost — before that
/// decision would be committing the project to an answer it has not made.
///
/// To wire one up later:
///
///   1. add the provider's package (for example `firebase_messaging`);
///   2. implement this interface against it;
///   3. swap the implementation in `pushNotificationServiceProvider`;
///   4. call [requestPermission] and [token] after sign-in, and pass the
///      token to `RegisterPushDeviceUseCase`.
///
/// Nothing above the service layer changes, because nothing above it knows
/// which provider is in use.
abstract interface class PushNotificationService {
  /// Prepares the provider. Safe to call more than once.
  Future<void> initialise();

  /// Asks the user for notification permission.
  ///
  /// Returns false when it is refused; the app keeps working, it simply does
  /// not receive pushes.
  Future<bool> requestPermission();

  /// The device's push token, or null when unavailable or not permitted.
  ///
  /// Passed to `POST /devices/register` so the server can address this
  /// device.
  Future<String?> token();

  /// Fires when the token is rotated by the provider, which happens on
  /// reinstall and occasionally at the provider's discretion. The new value
  /// must be re-registered with the server.
  Stream<String> get onTokenRefresh;

  /// Messages received while the app is in the foreground.
  Stream<PushMessage> get onMessage;

  /// Fires when the user taps a notification, including the one that
  /// launched the app from cold.
  Stream<PushMessage> get onMessageOpened;

  /// Clears the token on sign-out, so the device stops receiving messages
  /// addressed to the user who just left.
  Future<void> clearToken();

  Future<void> dispose();
}

/// The shipped implementation: it does nothing, and says so.
///
/// Chosen over leaving the interface unimplemented so the rest of the app can
/// depend on the service today. Every method is a no-op that logs at debug
/// level, and [token] returns null — which `RegisterPushDeviceUseCase`
/// already treats as "nothing to register".
class NoopPushNotificationService implements PushNotificationService {
  NoopPushNotificationService(this._logger);

  final AppLogger _logger;

  final StreamController<String> _tokenController =
      StreamController<String>.broadcast();
  final StreamController<PushMessage> _messageController =
      StreamController<PushMessage>.broadcast();
  final StreamController<PushMessage> _openedController =
      StreamController<PushMessage>.broadcast();

  @override
  Future<void> initialise() async {
    _logger.debug(
      'Push notifications are not configured; using the no-op service',
    );
  }

  @override
  Future<bool> requestPermission() async => false;

  @override
  Future<String?> token() async => null;

  @override
  Stream<String> get onTokenRefresh => _tokenController.stream;

  @override
  Stream<PushMessage> get onMessage => _messageController.stream;

  @override
  Stream<PushMessage> get onMessageOpened => _openedController.stream;

  @override
  Future<void> clearToken() async {}

  @override
  Future<void> dispose() async {
    await _tokenController.close();
    await _messageController.close();
    await _openedController.close();
  }
}
