import 'dart:typed_data';

import 'package:hr_employee_app/features/payslip/data/datasources/payslip_document_store_web.dart'
    if (dart.library.io) 'package:hr_employee_app/features/payslip/data/datasources/payslip_document_store_io.dart'
    as impl;

/// What came of trying to open a saved payslip.
///
/// An enum rather than a thrown error so the wording of each case stays in
/// the repository, next to every other message the feature produces.
enum PayslipOpenOutcome {
  opened,

  /// Nothing on the device can display a PDF.
  noViewer,

  /// The host has no way to open a local document at all — a browser build.
  unsupported,

  failed,
}

/// Where a downloaded payslip PDF is kept, and how it is opened again.
///
/// Split out of the repository because it is the one part of the feature that
/// cannot be written once for every host: a device writes to app-private
/// storage and hands the file to a viewer app, and a browser has neither.
abstract interface class PayslipDocumentStore {
  /// Writes [bytes] and returns the handle [open] will take.
  ///
  /// Throws a [PayslipStoreUnavailable] when the host will not accept the
  /// write; the repository turns that into the user-facing failure.
  Future<String> save({required String fileName, required Uint8List bytes});

  Future<PayslipOpenOutcome> open(String path);

  /// Deletes every saved payslip. Best effort — it must not throw.
  Future<void> clear();
}

/// Raised when the host cannot store the document.
///
/// Carries [detail] for the log rather than for the user: the repository
/// supplies the sentence the user reads.
class PayslipStoreUnavailable implements Exception {
  const PayslipStoreUnavailable(this.detail);

  final String detail;

  @override
  String toString() => 'PayslipStoreUnavailable: $detail';
}

/// The store for the host this build targets.
PayslipDocumentStore createPayslipDocumentStore() =>
    impl.createPayslipDocumentStore();
