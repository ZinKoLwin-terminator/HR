import {
  GRIEVANCE_ATTACHMENT_ALLOWED_EXTENSIONS,
  GRIEVANCE_ATTACHMENT_MAX_BYTES,
  GRIEVANCE_CASE_TEAM_FILES_MAX_PER_CASE,
  GRIEVANCE_SUBMITTER_FILES_MAX_PER_CASE,
} from '../models/grievance.models';

/**
 * The grievance file checks a screen can make before uploading (name and size only). The server
 * decides by the file's content and keeps the quotas (per case, and the submitter's daily total),
 * so a file that passes here can still be refused.
 */

/** Who uploads: the submitter and the case team have separate per-case quotas. */
export type GrievanceUploader = 'submitter' | 'case-team';

export const GRIEVANCE_FILES_MAX: Readonly<Record<GrievanceUploader, number>> = {
  submitter: GRIEVANCE_SUBMITTER_FILES_MAX_PER_CASE,
  'case-team': GRIEVANCE_CASE_TEAM_FILES_MAX_PER_CASE,
};

export function grievanceFileHint(uploader: GrievanceUploader): string {
  return `PDF, PNG, JPEG, Word (.docx) or Excel (.xlsx), up to 10 MB each, at most ${GRIEVANCE_FILES_MAX[uploader]} files per grievance.`;
}

/**
 * Why the file cannot be added, or null. `existing` counts the files the same uploader side already
 * added to the case (the quotas are separate).
 */
export function grievanceFileRefusal(
  file: { name: string; size: number },
  existing: number,
  uploader: GrievanceUploader = 'submitter',
): string | null {
  const max = GRIEVANCE_FILES_MAX[uploader];
  if (existing >= max) {
    return `At most ${max} files can be added to a grievance.`;
  }
  const dot = file.name.lastIndexOf('.');
  const extension = dot >= 0 ? file.name.slice(dot).toLowerCase() : '';
  if (!GRIEVANCE_ATTACHMENT_ALLOWED_EXTENSIONS.includes(extension)) {
    return `"${file.name}" is not an allowed file type. ${grievanceFileHint(uploader)}`;
  }
  if (file.size === 0) {
    return `"${file.name}" is empty.`;
  }
  if (file.size > GRIEVANCE_ATTACHMENT_MAX_BYTES) {
    return `"${file.name}" is larger than 10 MB.`;
  }
  return null;
}

/** "12 KB", "3.4 MB". */
export function grievanceFileSize(bytes: number): string {
  if (bytes < 1024) {
    return `${bytes} B`;
  }
  if (bytes < 1024 * 1024) {
    return `${Math.round(bytes / 1024)} KB`;
  }
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
}
