import 'dart:typed_data';

import 'package:hr_employee_app/features/payslip/data/datasources/payslip_document_store.dart';

/// The browser build keeps no payslip on disk.
///
/// A web page has no app-private directory to write to and no installed
/// viewer to hand a path to, and the one thing it could do — push the bytes
/// at the user as a download — would put an unencrypted payslip in the
/// Downloads folder of a possibly shared computer. The web build therefore
/// declines, and the repository says so in as many words. The browser target
/// exists for UI review (see `web/mockup.html`), not for distributing pay
/// documents.
class WebPayslipDocumentStore implements PayslipDocumentStore {
  const WebPayslipDocumentStore();

  @override
  Future<String> save({required String fileName, required Uint8List bytes}) {
    throw const PayslipStoreUnavailable('no file system in the browser');
  }

  @override
  Future<PayslipOpenOutcome> open(String path) async =>
      PayslipOpenOutcome.unsupported;

  @override
  Future<void> clear() async {
    // Nothing was ever written.
  }
}

PayslipDocumentStore createPayslipDocumentStore() =>
    const WebPayslipDocumentStore();
