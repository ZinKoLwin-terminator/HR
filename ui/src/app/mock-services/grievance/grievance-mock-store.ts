import { Injectable } from '@angular/core';
import { GrievanceActorRole, GrievanceAuditAction } from '../../core/models/grievance.models';
import {
  MOCK_GRIEVANCE_CASES,
  MOCK_GRIEVANCE_SUBMITTERS,
} from '../../mock-data/grievance/grievance-cases.mock';
import {
  GrievanceAuditRecord,
  GrievanceCaseRecord,
  GrievanceSubmitterLink,
  grievanceDemoPseudonym,
} from '../../mock-data/grievance/grievance-records';
import { onDataReset, persistedSignal } from '../persistence';
import { initialRecords } from '../seed-mode';

/** A minimal valid PDF, served for the sample attachments (their bytes are not part of the seed). */
const SAMPLE_PDF = [
  '%PDF-1.4',
  '1 0 obj<</Type/Catalog/Pages 2 0 R>>endobj',
  '2 0 obj<</Type/Pages/Kids[3 0 R]/Count 1>>endobj',
  '3 0 obj<</Type/Page/Parent 2 0 R/MediaBox[0 0 300 100]>>endobj',
  'trailer<</Root 1 0 R>>',
  '%%EOF',
].join('\n');

/**
 * The state of the grievance mock services: the case records, the submitter links, the access log
 * and the uploaded bytes. BROWSER SIMULATION: nothing here protects anything; it only reproduces the
 * API's behaviour for the demo.
 *
 * The submitter link (`grv.CaseSubmitter`) is private to this class: the services ask questions
 * ("is this the caller's case?", "who receives the notification?") and never copy the link onto a
 * returned object. Anonymous cases are linked by a pseudonym (see `grievanceDemoPseudonym`), never by
 * the employee id.
 */
@Injectable({ providedIn: 'root' })
export class GrievanceMockStore {
  private readonly cases = persistedSignal<GrievanceCaseRecord[]>(
    'grievance_cases',
    () => structuredClone(initialRecords(MOCK_GRIEVANCE_CASES)) as GrievanceCaseRecord[],
  );
  private readonly submitters = persistedSignal<GrievanceSubmitterLink[]>(
    'grievance_submitters',
    () => structuredClone(initialRecords(MOCK_GRIEVANCE_SUBMITTERS)) as GrievanceSubmitterLink[],
  );
  private readonly auditLog = persistedSignal<GrievanceAuditRecord[]>('grievance_audit', () => []);
  /** Uploaded bytes by attachment id. In memory only: a Blob cannot be saved in browser storage. */
  private readonly blobs = new Map<number, Blob>();
  /** Submitter upload bytes per `employeeId:day`. In memory only (a simulation of the API's quota). */
  private readonly uploads = new Map<string, number>();

  /** Live, read-only: for the grievance mock services only. */
  readonly records = this.cases.asReadonly();
  readonly audits = this.auditLog.asReadonly();

  constructor() {
    onDataReset(() => {
      this.blobs.clear();
      this.uploads.clear();
    });
  }

  // ---- Cases ----------------------------------------------------------------------------------

  find(id: number): GrievanceCaseRecord | undefined {
    return this.cases().find((c) => c.id === id);
  }

  /** Adds or replaces a case. */
  save(record: GrievanceCaseRecord): void {
    this.cases.update((list) =>
      list.some((c) => c.id === record.id)
        ? list.map((c) => (c.id === record.id ? record : c))
        : [...list, record],
    );
  }

  /** Removes a draft with its link and files. */
  remove(id: number): void {
    const record = this.find(id);
    for (const attachment of record?.attachments ?? []) {
      this.blobs.delete(attachment.id);
    }
    this.cases.update((list) => list.filter((c) => c.id !== id));
    this.submitters.update((list) => list.filter((l) => l.caseId !== id));
  }

  nextCaseId(): number {
    return this.cases().reduce((max, c) => Math.max(max, c.id), 0) + 1;
  }

  /** The next id of a child row kind, unique across every case (like an IDENTITY column). */
  nextChildId(
    kind:
      | 'assignments'
      | 'notes'
      | 'communications'
      | 'attachments'
      | 'correctiveActions'
      | 'proposals'
      | 'escalations'
      | 'history',
  ): number {
    let max = 0;
    for (const c of this.cases()) {
      for (const row of c[kind] as { id: number }[]) {
        max = Math.max(max, row.id);
      }
    }
    // Escalations and proposals share the seed's counter, so keep them apart from each other.
    if (kind === 'proposals' || kind === 'escalations') {
      for (const c of this.cases()) {
        for (const row of [...c.proposals, ...c.escalations]) {
          max = Math.max(max, row.id);
        }
      }
    }
    return max + 1;
  }

  /** The next number of the case-number sequence (`grv.CaseNoSeq`): never reused. */
  nextCaseSequence(): number {
    let max = 0;
    for (const c of this.cases()) {
      const match = c.caseNo?.match(/-(\d+)$/);
      if (match) {
        max = Math.max(max, Number(match[1]));
      }
    }
    return max + 1;
  }

  // ---- Submitter link (private: answers only) -------------------------------------------------

  private link(caseId: number): GrievanceSubmitterLink | undefined {
    return this.submitters().find((l) => l.caseId === caseId);
  }

  private matches(link: GrievanceSubmitterLink, employeeId: number): boolean {
    return (
      link.employeeId === employeeId ||
      (link.pseudonym !== null && link.pseudonym === grievanceDemoPseudonym(employeeId))
    );
  }

  /** True when the employee submitted the case (named, or anonymous through the pseudonym). */
  isSubmitter(caseId: number, employeeId: number | null): boolean {
    if (employeeId === null) {
      return false;
    }
    const link = this.link(caseId);
    return link !== undefined && this.matches(link, employeeId);
  }

  /** The ids of the employee's own cases (named and anonymous), drafts included. */
  ownCaseIds(employeeId: number | null): Set<number> {
    if (employeeId === null) {
      return new Set();
    }
    return new Set(
      this.submitters()
        .filter((l) => this.matches(l, employeeId))
        .map((l) => l.caseId),
    );
  }

  /**
   * The submitter's employee id of a NAMED case (for the case-team detail and for notifications);
   * always null for an anonymous case.
   */
  namedSubmitterId(caseId: number): number | null {
    return this.link(caseId)?.employeeId ?? null;
  }

  /** Links a new or edited draft to its submitter: the employee id, or only the pseudonym. */
  linkSubmitter(caseId: number, employeeId: number, anonymous: boolean): void {
    const seenAtUtc = this.link(caseId)?.seenAtUtc ?? null;
    const link: GrievanceSubmitterLink = {
      caseId,
      employeeId: anonymous ? null : employeeId,
      pseudonym: anonymous ? grievanceDemoPseudonym(employeeId) : null,
      seenAtUtc,
    };
    this.submitters.update((list) => [...list.filter((l) => l.caseId !== caseId), link]);
  }

  seenAt(caseId: number): string | null {
    return this.link(caseId)?.seenAtUtc ?? null;
  }

  markSeen(caseId: number, atUtc: string): void {
    this.submitters.update((list) =>
      list.map((l) => (l.caseId === caseId ? { ...l, seenAtUtc: atUtc } : l)),
    );
  }

  // ---- Access log -----------------------------------------------------------------------------

  audit(
    caseId: number | null,
    action: GrievanceAuditAction,
    actorUserId: number | null,
    actorRole: GrievanceActorRole,
    detail: string | null = null,
  ): void {
    this.auditLog.update((list) => [
      ...list,
      {
        id: list.reduce((max, a) => Math.max(max, a.id), 0) + 1,
        caseId,
        action,
        actorUserId,
        actorRole,
        occurredAtUtc: new Date().toISOString(),
        detail,
      },
    ]);
  }

  // ---- File bytes -----------------------------------------------------------------------------

  /** Bytes an employee uploaded to their grievances on a day (the daily quota). In memory only. */
  uploadedBytesOn(employeeId: number, day: string): number {
    return this.uploads.get(`${employeeId}:${day}`) ?? 0;
  }

  recordUpload(employeeId: number, day: string, bytes: number): void {
    const key = `${employeeId}:${day}`;
    this.uploads.set(key, (this.uploads.get(key) ?? 0) + bytes);
  }

  putBlob(attachmentId: number, blob: Blob): void {
    this.blobs.set(attachmentId, blob);
  }

  /**
   * The uploaded bytes; for a PDF whose bytes are not in memory (the samples, or an upload before a
   * reload) a small sample PDF; otherwise null.
   */
  blobOf(attachmentId: number, contentType: string): Blob | null {
    const blob = this.blobs.get(attachmentId);
    if (blob) {
      return blob;
    }
    return contentType === 'application/pdf'
      ? new Blob([SAMPLE_PDF], { type: 'application/pdf' })
      : null;
  }
}
