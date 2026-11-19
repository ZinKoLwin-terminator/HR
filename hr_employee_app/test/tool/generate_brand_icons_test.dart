@Tags(<String>['brand-icons'])
library;

import 'dart:io';
import 'dart:typed_data';
import 'dart:ui' as ui;

import 'package:flutter_test/flutter_test.dart';
import 'package:hr_employee_app/core/widgets/app_branding.dart';
import 'package:material_ui/material_ui.dart';

/// Writes every launcher icon and favicon from [ApexMarkPainter].
///
/// Run with:
/// ```
/// flutter test test/tool --run-skipped
/// ```
///
/// Tooling, not an assertion suite: tagged `brand-icons` and skipped by
/// default in `dart_test.yaml`. Rendering from the same painter the app uses
/// is what keeps the home-screen icon and the in-app logo identical, and it
/// needs no image tool on the machine.
///
/// Launcher icons are written as opaque RGB: the App Store rejects an app
/// icon that carries an alpha channel, even a fully opaque one.
void main() {
  test('generate launcher icons and favicon', () async {
    const String android = 'android/app/src/main/res';
    const String ios = 'ios/Runner/Assets.xcassets/AppIcon.appiconset';

    final Map<String, int> androidIcons = <String, int>{
      'mipmap-mdpi': 48,
      'mipmap-hdpi': 72,
      'mipmap-xhdpi': 96,
      'mipmap-xxhdpi': 144,
      'mipmap-xxxhdpi': 192,
    };
    for (final MapEntry<String, int> icon in androidIcons.entries) {
      await _write(
        '$android/${icon.key}/ic_launcher.png',
        ApexMarkStyle.appIcon,
        icon.value,
      );
    }

    // Point size and scales, as listed in the asset catalogue.
    final Map<String, List<int>> iosIcons = <String, List<int>>{
      '20x20': <int>[1, 2, 3],
      '29x29': <int>[1, 2, 3],
      '40x40': <int>[1, 2, 3],
      '60x60': <int>[2, 3],
      '76x76': <int>[1, 2],
      '1024x1024': <int>[1],
    };
    for (final MapEntry<String, List<int>> icon in iosIcons.entries) {
      final int points = int.parse(icon.key.split('x').first);
      for (final int scale in icon.value) {
        await _write(
          '$ios/Icon-App-${icon.key}@${scale}x.png',
          ApexMarkStyle.appIcon,
          points * scale,
        );
      }
    }
    await _write(
      '$ios/Icon-App-83.5x83.5@2x.png',
      ApexMarkStyle.appIcon,
      167,
    );

    await _write('web/icons/Icon-192.png', ApexMarkStyle.appIcon, 192);
    await _write('web/icons/Icon-512.png', ApexMarkStyle.appIcon, 512);
    await _write(
      'web/icons/Icon-maskable-192.png',
      ApexMarkStyle.appIconMaskable,
      192,
    );
    await _write(
      'web/icons/Icon-maskable-512.png',
      ApexMarkStyle.appIconMaskable,
      512,
    );
    // Rounded corners, so this one keeps its alpha.
    await _write(
      'web/favicon.png',
      ApexMarkStyle.favicon,
      32,
      keepAlpha: true,
    );
  });
}

Future<void> _write(
  String path,
  ApexMarkStyle style,
  int size, {
  bool keepAlpha = false,
}) async {
  final ui.PictureRecorder recorder = ui.PictureRecorder();
  ApexMarkPainter(style).paint(
    Canvas(recorder),
    Size(size.toDouble(), size.toDouble()),
  );
  final ui.Image image = await recorder.endRecording().toImage(size, size);
  final ByteData? rgba = await image.toByteData();
  image.dispose();

  File(path).writeAsBytesSync(
    _encodePng(rgba!.buffer.asUint8List(), size, size, keepAlpha: keepAlpha),
  );
}

/// A minimal PNG encoder: one IDAT, filter type 0 on every row.
///
/// Hand-rolled because `toByteData(format: png)` always writes RGBA, and
/// the launcher icons must not have an alpha channel.
Uint8List _encodePng(
  Uint8List rgba,
  int width,
  int height, {
  required bool keepAlpha,
}) {
  final int channels = keepAlpha ? 4 : 3;
  final BytesBuilder raw = BytesBuilder();
  for (int y = 0; y < height; y++) {
    raw.addByte(0);
    for (int x = 0; x < width; x++) {
      final int i = (y * width + x) * 4;
      if (keepAlpha) {
        // toByteData gives premultiplied alpha; PNG wants straight.
        final int a = rgba[i + 3];
        for (int c = 0; c < 3; c++) {
          raw.addByte(a == 0 ? 0 : (rgba[i + c] * 255 / a).round().clamp(0, 255));
        }
        raw.addByte(a);
      } else {
        raw.add(rgba.sublist(i, i + channels));
      }
    }
  }

  final ByteData header = ByteData(13)
    ..setUint32(0, width)
    ..setUint32(4, height)
    ..setUint8(8, 8) // bit depth
    ..setUint8(9, keepAlpha ? 6 : 2) // RGBA : RGB
    ..setUint8(10, 0)
    ..setUint8(11, 0)
    ..setUint8(12, 0);

  return (BytesBuilder()
        ..add(<int>[0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A])
        ..add(_chunk('IHDR', header.buffer.asUint8List()))
        ..add(_chunk('IDAT', ZLibCodec(level: 9).encode(raw.takeBytes())))
        ..add(_chunk('IEND', <int>[])))
      .takeBytes();
}

List<int> _chunk(String type, List<int> data) {
  final List<int> typed = <int>[...type.codeUnits, ...data];
  final ByteData length = ByteData(4)..setUint32(0, data.length);
  final ByteData crc = ByteData(4)..setUint32(0, _crc32(typed));
  return <int>[
    ...length.buffer.asUint8List(),
    ...typed,
    ...crc.buffer.asUint8List(),
  ];
}

int _crc32(List<int> bytes) {
  int crc = 0xFFFFFFFF;
  for (final int byte in bytes) {
    crc ^= byte;
    for (int k = 0; k < 8; k++) {
      crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320 : crc >> 1;
    }
  }
  return crc ^ 0xFFFFFFFF;
}
