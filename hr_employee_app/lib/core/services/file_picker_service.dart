import 'package:file_picker/file_picker.dart';
import 'package:hr_employee_app/core/errors/failure.dart';
import 'package:hr_employee_app/core/errors/result.dart';
import 'package:hr_employee_app/core/platform/local_file.dart';

/// A file the user chose.
class PickedFile {
  const PickedFile({
    required this.path,
    required this.name,
    required this.sizeBytes,
  });

  final String path;
  final String name;
  final int sizeBytes;

  String get extension =>
      name.contains('.') ? name.split('.').last.toLowerCase() : '';

  /// `148 KB`, for the attachment row.
  String get readableSize {
    if (sizeBytes < 1024) {
      return '$sizeBytes B';
    }
    if (sizeBytes < 1024 * 1024) {
      return '${(sizeBytes / 1024).round()} KB';
    }
    return '${(sizeBytes / (1024 * 1024)).toStringAsFixed(1)} MB';
  }
}

/// Lets the user choose files to upload.
///
/// An interface so the apply-leave and profile-photo flows can be tested
/// without a platform picker.
///
/// Size and type are enforced **here**, before anything is uploaded. On a
/// slow connection, rejecting a 10 MB photo after it has finished uploading
/// is a waste of the user's data and patience, and the limits are already
/// known from `UploadLimits`.
abstract interface class FilePickerService {
  /// Picks one or more documents or images.
  ///
  /// Returns an empty list when the user cancels, which is not a failure.
  Future<Result<List<PickedFile>>> pickFiles({
    required List<String> allowedExtensions,
    required int maxSizeBytes,
    bool allowMultiple = false,
    int? maxFiles,
  });

  /// Picks a single image, for the profile photo.
  Future<Result<PickedFile?>> pickImage({
    required List<String> allowedExtensions,
    required int maxSizeBytes,
  });
}

class FilePickerServiceImpl implements FilePickerService {
  const FilePickerServiceImpl();

  @override
  Future<Result<List<PickedFile>>> pickFiles({
    required List<String> allowedExtensions,
    required int maxSizeBytes,
    bool allowMultiple = false,
    int? maxFiles,
  }) async {
    try {
      final FilePickerResult? result = await FilePicker.platform.pickFiles(
        type: FileType.custom,
        allowedExtensions: allowedExtensions,
        allowMultiple: allowMultiple,
        withData: false,
      );

      if (result == null || result.files.isEmpty) {
        // Cancelling is a normal outcome, not an error.
        return const Result<List<PickedFile>>.success(<PickedFile>[]);
      }

      if (maxFiles != null && result.files.length > maxFiles) {
        return Result<List<PickedFile>>.failure(
          ValidationFailure('Choose at most $maxFiles files.'),
        );
      }

      final List<PickedFile> picked = <PickedFile>[];
      for (final PlatformFile file in result.files) {
        final String? path = file.path;
        if (path == null) {
          continue;
        }

        if (file.size > maxSizeBytes) {
          return Result<List<PickedFile>>.failure(
            ValidationFailure(
              '${file.name} is ${_megabytes(file.size)}. The limit is '
              '${_megabytes(maxSizeBytes)}.',
            ),
          );
        }

        final String extension = file.extension?.toLowerCase() ?? '';
        if (!allowedExtensions.contains(extension)) {
          return Result<List<PickedFile>>.failure(
            ValidationFailure(
              '${file.name} is not a supported type. Use '
              '${allowedExtensions.join(', ')}.',
            ),
          );
        }

        picked.add(
          PickedFile(path: path, name: file.name, sizeBytes: file.size),
        );
      }

      return Result<List<PickedFile>>.success(picked);
    } on Object catch (_) {
      return const Result<List<PickedFile>>.failure(
        UnexpectedFailure('Could not open the file picker.'),
      );
    }
  }

  @override
  Future<Result<PickedFile?>> pickImage({
    required List<String> allowedExtensions,
    required int maxSizeBytes,
  }) async {
    final Result<List<PickedFile>> result = await pickFiles(
      allowedExtensions: allowedExtensions,
      maxSizeBytes: maxSizeBytes,
    );

    return result.map(
      (List<PickedFile> files) => files.isEmpty ? null : files.first,
    );
  }

  static String _megabytes(int bytes) =>
      '${(bytes / (1024 * 1024)).toStringAsFixed(1)} MB';
}

/// In-memory implementation for tests.
///
/// The queued results are handed out one call at a time, so a test can
/// script a sequence of picks including a cancellation (an empty list).
class FakeFilePickerService implements FilePickerService {
  FakeFilePickerService({List<List<PickedFile>>? queued})
    : _queued = queued ?? <List<PickedFile>>[];

  final List<List<PickedFile>> _queued;

  @override
  Future<Result<List<PickedFile>>> pickFiles({
    required List<String> allowedExtensions,
    required int maxSizeBytes,
    bool allowMultiple = false,
    int? maxFiles,
  }) async {
    if (_queued.isEmpty) {
      return const Result<List<PickedFile>>.success(<PickedFile>[]);
    }
    return Result<List<PickedFile>>.success(_queued.removeAt(0));
  }

  @override
  Future<Result<PickedFile?>> pickImage({
    required List<String> allowedExtensions,
    required int maxSizeBytes,
  }) async {
    final Result<List<PickedFile>> result = await pickFiles(
      allowedExtensions: allowedExtensions,
      maxSizeBytes: maxSizeBytes,
    );
    return result.map(
      (List<PickedFile> files) => files.isEmpty ? null : files.first,
    );
  }
}

/// Reads a picked file's size from disk.
///
/// Used where a path arrives without metadata, such as a restored draft.
/// Null when the file is gone — or, in a browser build, whenever a path
/// cannot be inspected at all.
int? fileSizeOrNull(String path) => localFileSizeOrNull(path);
