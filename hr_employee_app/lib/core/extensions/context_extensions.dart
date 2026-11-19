import 'package:material_ui/material_ui.dart';

/// Shorthands for values read from [BuildContext] on almost every screen.
///
/// The semantic palette lives on its own extension in `app_theme.dart`.
extension BuildContextX on BuildContext {
  ThemeData get theme => Theme.of(this);

  TextTheme get textStyles => Theme.of(this).textTheme;

  ColorScheme get colors => Theme.of(this).colorScheme;

  MediaQueryData get media => MediaQuery.of(this);

  Size get screenSize => MediaQuery.sizeOf(this);

  double get screenWidth => MediaQuery.sizeOf(this).width;

  double get screenHeight => MediaQuery.sizeOf(this).height;

  /// Height of the on-screen keyboard; zero when it is closed.
  double get keyboardInset => MediaQuery.viewInsetsOf(this).bottom;

  bool get isKeyboardOpen => keyboardInset > 0;

  EdgeInsets get safeArea => MediaQuery.paddingOf(this);

  bool get isDarkMode => Theme.of(this).brightness == Brightness.dark;

  /// True on the narrowest phones, where a two-column grid is too cramped.
  bool get isCompactWidth => MediaQuery.sizeOf(this).width < 360;

  /// True on tablets and foldables, where content should be constrained
  /// rather than stretched edge to edge.
  bool get isWideLayout => MediaQuery.sizeOf(this).width >= 600;

  /// Dismisses the keyboard without stealing focus from a specific field.
  void dismissKeyboard() => FocusScope.of(this).unfocus();
}
