import 'dart:io';
import 'dart:typed_data';

import 'package:hr_employee_app/features/payslip/data/datasources/payslip_document_store.dart';
import 'package:open_filex/open_filex.dart';
import 'package:path_provider/path_provider.dart';

/// Storage policy, enforced here because this is the only place that writes:
///
///   * the PDF goes to the **application support directory**, which is
///     app-private on both platforms. Not the documents directory (exposed
///     to the iOS Files app and to Android's external storage), and not the
///     cache directory (the OS may evict it mid-read);
///   * [clear] wipes the directory on sign-out, so a shared device does not
///     leak the previous user's pay.
class IoPayslipDocumentStore implements PayslipDocumentStore {
  const IoPayslipDocumentStore();

  /// Subdirectory the PDFs are written to.
  static const String _directoryName = 'payslips';

  @override
  Future<String> save({
    required String fileName,
    required Uint8List bytes,
  }) async {
    final Directory directory = await _directory();
    final File file = File('${directory.path}/$fileName');
    try {
      await file.writeAsBytes(bytes, flush: true);
    } on FileSystemException catch (error) {
      throw PayslipStoreUnavailable('${error.osError}');
    }
    return file.path;
  }

  @override
  Future<PayslipOpenOutcome> open(String path) async {
    final OpenResult result = await OpenFilex.open(
      path,
      type: 'application/pdf',
    );
    return switch (result.type) {
      ResultType.done => PayslipOpenOutcome.opened,
      ResultType.noAppToOpen => PayslipOpenOutcome.noViewer,
      _ => PayslipOpenOutcome.failed,
    };
  }

  @override
  Future<void> clear() async {
    final Directory directory = await _directory();
    if (directory.existsSync()) {
      await directory.delete(recursive: true);
    }
  }

  /// The app-private directory payslip PDFs are written to, created on first
  /// use.
  Future<Directory> _directory() async {
    final Directory support = await getApplicationSupportDirectory();
    final Directory directory = Directory('${support.path}/$_directoryName');
    if (!directory.existsSync()) {
      await directory.create(recursive: true);
    }
    return directory;
  }
}

PayslipDocumentStore createPayslipDocumentStore() =>
    const IoPayslipDocumentStore();
