import {
  EVIDENCE_EMPTY_MESSAGE,
  EVIDENCE_NOT_ALLOWED_MESSAGE,
  EVIDENCE_TOO_LARGE_MESSAGE,
  detectEvidenceKind,
  inspectEvidence,
  readFileBytes,
  sanitizeEvidenceFileName,
} from './kpi-evidence';

const ascii = (text: string): number[] => Array.from(text, (c) => c.charCodeAt(0));
const PDF = new Uint8Array([...ascii('%PDF-1.4'), 10]);
const PNG = new Uint8Array([0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a, 0]);
const JPEG = new Uint8Array([0xff, 0xd8, 0xff, 0xe0, 0]);
const zip = (...entries: string[]) =>
  new Uint8Array([0x50, 0x4b, 3, 4, ...ascii(entries.join(' '))]);

describe('KPI evidence checks (demo mock)', () => {
  describe('detectEvidenceKind', () => {
    it('recognises each allowed type by its content', () => {
      expect(detectEvidenceKind(PDF)).toBe('pdf');
      expect(detectEvidenceKind(PNG)).toBe('png');
      expect(detectEvidenceKind(JPEG)).toBe('jpeg');
      expect(detectEvidenceKind(zip('[Content_Types].xml', 'word/document.xml'))).toBe('docx');
      expect(detectEvidenceKind(zip('[Content_Types].xml', 'xl/workbook.xml'))).toBe('xlsx');
    });

    it('refuses everything else', () => {
      expect(detectEvidenceKind(new Uint8Array(ascii('<html>')))).toBeNull();
      expect(detectEvidenceKind(new Uint8Array(ascii('<svg xmlns="x"></svg>')))).toBeNull();
      expect(detectEvidenceKind(new Uint8Array([0x4d, 0x5a, 0x90, 0]))).toBeNull(); // an executable
      expect(detectEvidenceKind(zip('other/file.xml'))).toBeNull(); // some other zip
      expect(detectEvidenceKind(zip('word/document.xml'))).toBeNull(); // no content-types part
      expect(detectEvidenceKind(new Uint8Array([0x25, 0x50]))).toBeNull(); // too short
    });
  });

  describe('sanitizeEvidenceFileName', () => {
    it.each([
      ['report.pdf', 'report.pdf'],
      ['C:\\Users\\x\\report.pdf', 'report.pdf'],
      ['../../etc/passwd.pdf', 'passwd.pdf'],
      ['  spaced name .pdf  ', 'spaced name .pdf'],
      ['a<b>c:d"e|f?g*h.pdf', 'abcdefgh.pdf'],
      ['tab\there.pdf', 'tabhere.pdf'],
      ['evil\u202Egnp.pdf', 'evilgnp.pdf'],
      ['', ''],
      ['   ', ''],
      ['....', ''],
      ['folder/', ''],
    ])('cleans %j to %j', (input, expected) => {
      expect(sanitizeEvidenceFileName(input)).toBe(expected);
    });

    it('shortens a long name to 260 characters and keeps the extension', () => {
      const cleaned = sanitizeEvidenceFileName(`${'a'.repeat(400)}.pdf`);
      expect(cleaned).toHaveLength(260);
      expect(cleaned.endsWith('.pdf')).toBe(true);
    });
  });

  describe('inspectEvidence', () => {
    it('accepts a file whose extension agrees with its content, in any case', () => {
      expect(inspectEvidence('Proof.PDF', PDF)).toEqual({
        ok: true,
        fileName: 'Proof.PDF',
        contentType: 'application/pdf',
      });
      expect(inspectEvidence('p.jpg', JPEG)).toMatchObject({ ok: true, contentType: 'image/jpeg' });
      expect(inspectEvidence('p.jpeg', JPEG)).toMatchObject({ ok: true });
      expect(inspectEvidence('p.png', PNG)).toMatchObject({ contentType: 'image/png' });
    });

    it('ignores what the client says about the type: only the content decides', () => {
      expect(inspectEvidence('p.png', PDF)).toEqual({
        ok: false,
        message: EVIDENCE_NOT_ALLOWED_MESSAGE,
      });
      expect(inspectEvidence('p.exe', PDF)).toMatchObject({ ok: false });
    });

    it('names a file with nothing left after cleaning by its detected type', () => {
      expect(inspectEvidence('???', PNG)).toMatchObject({ ok: true, fileName: 'evidence.png' });
      expect(inspectEvidence(null, JPEG)).toMatchObject({ fileName: 'evidence.jpg' });
      expect(inspectEvidence('.pdf', PDF)).toMatchObject({ fileName: 'evidence.pdf' });
    });

    it('refuses an empty file, a file over 10 MB and an unknown type', () => {
      expect(inspectEvidence('a.pdf', new Uint8Array(0))).toEqual({
        ok: false,
        message: EVIDENCE_EMPTY_MESSAGE,
      });
      const big = new Uint8Array(10 * 1024 * 1024 + 1);
      big.set(PDF);
      expect(inspectEvidence('a.pdf', big)).toEqual({
        ok: false,
        message: EVIDENCE_TOO_LARGE_MESSAGE,
      });
      const exactly = new Uint8Array(10 * 1024 * 1024);
      exactly.set(PDF);
      expect(inspectEvidence('a.pdf', exactly)).toMatchObject({ ok: true });
      expect(inspectEvidence('a.pdf', new Uint8Array(ascii('hello')))).toEqual({
        ok: false,
        message: EVIDENCE_NOT_ALLOWED_MESSAGE,
      });
    });
  });

  it('reads a file into bytes', async () => {
    const bytes = await readFileBytes(new File([PDF], 'a.pdf'));
    expect(Array.from(bytes)).toEqual(Array.from(PDF));
  });
});
