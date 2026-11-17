import { EVIDENCE_MAX_BYTES } from '../models/kpi.models';

/**
 * Evidence file checks for the DEMO MOCK ONLY (the API does the real checking and storage).
 * The rules mirror `EvidenceFiles` of the API: the type is decided by the content (magic bytes), the
 * extension must agree with it, 1 byte to 10 MB, and the name is cleaned. Office packages are
 * recognised by their entry names, a simplification of the API's ZIP inspection.
 */

export const EVIDENCE_NOT_ALLOWED_MESSAGE =
  'Allowed evidence files are PDF, PNG, JPEG, Word (.docx) and Excel (.xlsx), up to 10 MB.';
export const EVIDENCE_EMPTY_MESSAGE = `The file is empty. ${EVIDENCE_NOT_ALLOWED_MESSAGE}`;
export const EVIDENCE_TOO_LARGE_MESSAGE = `The file is larger than 10 MB. ${EVIDENCE_NOT_ALLOWED_MESSAGE}`;

export type EvidenceKind = 'pdf' | 'png' | 'jpeg' | 'docx' | 'xlsx';

const CONTENT_TYPES: Record<EvidenceKind, string> = {
  pdf: 'application/pdf',
  png: 'image/png',
  jpeg: 'image/jpeg',
  docx: 'application/vnd.openxmlformats-officedocument.wordprocessingml.document',
  xlsx: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
};

const DEFAULT_EXTENSIONS: Record<EvidenceKind, string> = {
  pdf: '.pdf',
  png: '.png',
  jpeg: '.jpg',
  docx: '.docx',
  xlsx: '.xlsx',
};

const MAX_FILE_NAME_LENGTH = 260;

const startsWith = (bytes: Uint8Array, signature: readonly number[]): boolean =>
  bytes.length >= signature.length && signature.every((value, index) => bytes[index] === value);

/** True when the ASCII text appears anywhere in the bytes (zip entry names are stored uncompressed). */
function containsAscii(bytes: Uint8Array, text: string): boolean {
  const needle = Array.from(text, (character) => character.charCodeAt(0));
  const last = bytes.length - needle.length;
  for (let start = 0; start <= last; start++) {
    let index = 0;
    while (index < needle.length && bytes[start + index] === needle[index]) {
      index++;
    }
    if (index === needle.length) {
      return true;
    }
  }
  return false;
}

/** What the content is, by its first bytes (and for a zip, by its entry names), or null. */
export function detectEvidenceKind(bytes: Uint8Array): EvidenceKind | null {
  if (startsWith(bytes, [0x25, 0x50, 0x44, 0x46, 0x2d])) {
    return 'pdf';
  }
  if (startsWith(bytes, [0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a])) {
    return 'png';
  }
  if (startsWith(bytes, [0xff, 0xd8, 0xff])) {
    return 'jpeg';
  }
  if (startsWith(bytes, [0x50, 0x4b, 0x03, 0x04]) && containsAscii(bytes, '[Content_Types].xml')) {
    if (containsAscii(bytes, 'word/document.xml')) {
      return 'docx';
    }
    if (containsAscii(bytes, 'xl/workbook.xml')) {
      return 'xlsx';
    }
  }
  return null;
}

function kindOfExtension(extension: string): EvidenceKind | null {
  switch (extension.toLowerCase()) {
    case '.pdf':
      return 'pdf';
    case '.png':
      return 'png';
    case '.jpg':
    case '.jpeg':
      return 'jpeg';
    case '.docx':
      return 'docx';
    case '.xlsx':
      return 'xlsx';
    default:
      return null;
  }
}

/** Characters dropped from a file name: controls, bidi overrides, line separators and `< > : " | ? * / \`. */
const UNSAFE_CHARACTERS = '<>:"|?*/\\';
/** Line and paragraph separators, direction marks and the byte order mark. */
const INVISIBLE_CODES = [0x2028, 0x2029, 0x200e, 0x200f, 0xfeff];

function isDropped(character: string): boolean {
  const code = character.codePointAt(0) ?? 0;
  return (
    /\p{Cc}/u.test(character) ||
    UNSAFE_CHARACTERS.includes(character) ||
    INVISIBLE_CODES.includes(code) ||
    (code >= 0x202a && code <= 0x202e) ||
    (code >= 0x2066 && code <= 0x2069)
  );
}

/** Keeps the last path segment, drops unsafe characters and shortens to 260 characters (extension kept). */
export function sanitizeEvidenceFileName(clientFileName: string | null | undefined): string {
  if (!clientFileName || clientFileName.trim() === '') {
    return '';
  }
  const lastSeparator = Math.max(clientFileName.lastIndexOf('/'), clientFileName.lastIndexOf('\\'));
  const name = Array.from(
    lastSeparator >= 0 ? clientFileName.slice(lastSeparator + 1) : clientFileName,
  )
    .filter((character) => !isDropped(character))
    .join('')
    .trim();
  if (name === '' || /^\.+$/.test(name)) {
    return '';
  }
  if (name.length <= MAX_FILE_NAME_LENGTH) {
    return name;
  }
  const dot = name.lastIndexOf('.');
  const extension = dot > 0 && name.length - dot <= 16 ? name.slice(dot) : '';
  return name.slice(0, MAX_FILE_NAME_LENGTH - extension.length).trimEnd() + extension;
}

function extensionOf(name: string): string {
  const dot = name.lastIndexOf('.');
  return dot >= 0 ? name.slice(dot) : '';
}

export type EvidenceInspection =
  { ok: true; fileName: string; contentType: string } | { ok: false; message: string };

/** Checks size, content type and extension of an upload, and returns the cleaned name and detected type. */
export function inspectEvidence(
  clientFileName: string | null | undefined,
  bytes: Uint8Array,
): EvidenceInspection {
  if (bytes.length === 0) {
    return { ok: false, message: EVIDENCE_EMPTY_MESSAGE };
  }
  if (bytes.length > EVIDENCE_MAX_BYTES) {
    return { ok: false, message: EVIDENCE_TOO_LARGE_MESSAGE };
  }
  const kind = detectEvidenceKind(bytes);
  if (kind === null) {
    return { ok: false, message: EVIDENCE_NOT_ALLOWED_MESSAGE };
  }
  const name = sanitizeEvidenceFileName(clientFileName);
  if (name === '') {
    return {
      ok: true,
      fileName: `evidence${DEFAULT_EXTENSIONS[kind]}`,
      contentType: CONTENT_TYPES[kind],
    };
  }
  const extension = extensionOf(name);
  if (kindOfExtension(extension) !== kind) {
    return { ok: false, message: EVIDENCE_NOT_ALLOWED_MESSAGE };
  }
  const stem = name.slice(0, name.length - extension.length);
  return {
    ok: true,
    fileName: stem === '' ? `evidence${extension}` : name,
    contentType: CONTENT_TYPES[kind],
  };
}

/** Reads a File into bytes (works where `Blob.arrayBuffer` is missing, such as older test DOMs). */
export function readFileBytes(file: Blob): Promise<Uint8Array> {
  if (typeof file.arrayBuffer === 'function') {
    return file.arrayBuffer().then((buffer) => new Uint8Array(buffer));
  }
  return new Promise((resolve, reject) => {
    const reader = new FileReader();
    reader.onload = () => resolve(new Uint8Array(reader.result as ArrayBuffer));
    reader.onerror = () => reject(reader.error);
    reader.readAsArrayBuffer(file);
  });
}
