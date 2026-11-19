import 'dart:io';

/// A device build can read a path straight from the file system.
const bool canInspectLocalFiles = true;

/// Size of the file at [path] in bytes, or null when it does not exist or
/// cannot be read.
int? localFileSizeOrNull(String path) {
  try {
    final File file = File(path);
    return file.existsSync() ? file.lengthSync() : null;
  } on Object catch (_) {
    return null;
  }
}
