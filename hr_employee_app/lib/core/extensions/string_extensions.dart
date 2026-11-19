/// Small string helpers used for display.
extension StringX on String {
  /// `aye chan moe` -> `Aye Chan Moe`.
  String get titleCase {
    if (isEmpty) {
      return this;
    }
    return split(RegExp(r'\s+'))
        .where((String word) => word.isNotEmpty)
        .map(
          (String word) => word.length == 1
              ? word.toUpperCase()
              : '${word[0].toUpperCase()}${word.substring(1).toLowerCase()}',
        )
        .join(' ');
  }

  /// Up to two initials for an avatar fallback: `Aye Chan Moe` -> `AC`.
  String get initials {
    final List<String> words = trim()
        .split(RegExp(r'\s+'))
        .where((String word) => word.isNotEmpty)
        .toList();
    if (words.isEmpty) {
      return '?';
    }
    if (words.length == 1) {
      final String word = words.first;
      return (word.length == 1 ? word : word.substring(0, 2)).toUpperCase();
    }
    return '${words.first[0]}${words.last[0]}'.toUpperCase();
  }

  /// Truncates with an ellipsis, never mid-surrogate.
  String truncate(int maxLength) {
    if (length <= maxLength) {
      return this;
    }
    return '${substring(0, maxLength).trimRight()}...';
  }

  /// `PascalCase` or `camelCase` -> `Pascal case`.
  ///
  /// Used to render API enum values such as `OnLeave` as `On leave` without
  /// maintaining a parallel label table for every enum.
  String get humanized {
    if (isEmpty) {
      return this;
    }
    final String spaced = replaceAllMapped(
      RegExp(r'(?<=[a-z0-9])(?=[A-Z])'),
      (Match _) => ' ',
    );
    return spaced[0].toUpperCase() + spaced.substring(1).toLowerCase();
  }

  bool get isBlank => trim().isEmpty;

  bool get isNotBlank => trim().isNotEmpty;
}

/// Helpers for optional strings coming back from the API.
extension NullableStringX on String? {
  bool get isNullOrBlank => this == null || this!.trim().isEmpty;

  /// Present as well as the negative form so a condition can read positively
  /// at the call site instead of as a double negative.
  bool get isNotNullOrBlank => !isNullOrBlank;

  /// The value, or [fallback] when it is absent or blank.
  ///
  /// Defaults to an em dash, which is how the UI renders "not provided".
  String orDash([String fallback = '—']) {
    final String? value = this;
    return (value == null || value.trim().isEmpty) ? fallback : value;
  }
}
