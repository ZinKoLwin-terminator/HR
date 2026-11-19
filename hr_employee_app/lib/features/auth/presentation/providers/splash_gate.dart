import 'package:flutter_riverpod/flutter_riverpod.dart';

/// Whether the splash animation has finished.
///
/// The router leaves the splash only when this is open **and** the stored
/// session has been resolved, whichever happens later — so a fast restore
/// does not cut the logo reveal short, and a slow one is not hidden behind a
/// frozen final frame.
///
/// The splash screen opens it when its animation completes, at once when the
/// platform asks for reduced motion, or if the animation fails to load.
class SplashGate extends Notifier<bool> {
  @override
  bool build() => false;

  void open() => state = true;
}

final NotifierProvider<SplashGate, bool> splashGateProvider =
    NotifierProvider<SplashGate, bool>(SplashGate.new);
