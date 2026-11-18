import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { APP_CONFIG } from '../../core/configuration/app-config';
import { PagedResult, PageRequest } from '../../core/models/common.models';
import { ExportRequest } from '../../core/models/excel.models';
import {
  GRIEVANCE_NOT_DISCLOSED_LABEL,
  GRIEVANCE_OPEN_STATUSES,
} from '../../core/models/grievance.models';
import {
  GRIEVANCE_REPORT_SORT_KEYS,
  GrievanceHrDashboard,
  GrievanceReportFilter,
  GrievanceReportRow,
  GrievanceReviewDashboard,
  GrievanceSummary,
  GrievanceSummaryFilter,
} from '../../core/models/grievance-insight.models';
import { GrievanceInsightDataService } from '../../core/services/data/grievance-data.services';
import { GrievanceCaseRecord } from '../../mock-data/grievance/grievance-records';
import { diffInDays, todayIso } from '../../shared/utilities/date.util';
import { isEmptyFilterValue, paginate, sortItems } from '../../shared/utilities/query.util';
import { ExportRecord } from '../excel/excel-module';
import { mockResponse } from '../mock-response';
import { GrievanceMockAccess, GrievanceMockActor } from './grievance-mock-access';
import { GrievanceMockRedaction, grievanceIsOverdue } from './grievance-mock-redaction';
import { validation } from './grievance-mock-rules';
import { GrievanceMockStore } from './grievance-mock-store';
import {
  averageResolutionDays,
  categoryCounts,
  countIn,
  countedCases,
  departmentCounts,
  monthlyTrend,
  priorityCounts,
  resolutionDays,
  resolutionRatePct,
  statusCounts,
} from './grievance-metrics';

/**
 * Grievance dashboards and reports, computed in the browser with the definitions of
 * `grievance-insight.models.ts` (see `grievance-metrics.ts`). DEMO MIRROR: the API's figures are
 * authoritative, and the "permissions" here are the simulated session's, enforced nowhere else.
 */
@Injectable({ providedIn: 'root' })
export class GrievanceInsightMockService implements GrievanceInsightDataService {
  private readonly latency = inject(APP_CONFIG).mockLatencyMs;
  private readonly access = inject(GrievanceMockAccess);
  private readonly store = inject(GrievanceMockStore);
  private readonly redaction = inject(GrievanceMockRedaction);

  private respond<R>(work: () => R): Observable<R> {
    return mockResponse(work, this.latency);
  }

  /**
   * Every non-Draft case, archived ones included. Aggregates are the same for every viewer: leaving
   * out the caller's own cases would let two HR users compare totals and find a submitter. Only the
   * lists and details leave them out.
   */
  private countedAll(): GrievanceCaseRecord[] {
    return countedCases(this.store.records());
  }

  private categoryName = (id: number): string => this.redaction.categoryName(id);
  private departmentName = (id: number): string => this.redaction.departmentName(id) ?? `#${id}`;

  hrDashboard(): Observable<GrievanceHrDashboard> {
    return this.respond(() => {
      const actor = this.access.actor();
      this.access.require(actor, ['grievance-management', 'View']);
      const counted = this.countedAll();
      const today = todayIso();
      return {
        asOfDate: today,
        total: counted.length,
        new: countIn(counted, ['Submitted']),
        underReview: countIn(counted, ['Acknowledged', 'UnderReview', 'Reopened']),
        underInvestigation: countIn(counted, ['Assigned', 'UnderInvestigation']),
        awaitingEmployeeResponse: countIn(counted, ['AwaitingEmployeeResponse']),
        pendingResolution: countIn(counted, ['ResolutionProposed']),
        resolved: countIn(counted, ['Resolved']),
        closed: countIn(counted, ['Closed']),
        rejected: countIn(counted, ['Rejected']),
        overdue: counted.filter((r) => grievanceIsOverdue(r, today)).length,
        escalated: counted.filter(
          (r) => r.isEscalated && GRIEVANCE_OPEN_STATUSES.includes(r.status),
        ).length,
        resolutionRatePct: resolutionRatePct(counted),
        averageResolutionDays: averageResolutionDays(counted),
        byStatus: statusCounts(counted),
        byPriority: priorityCounts(counted),
        byCategory: categoryCounts(counted, this.categoryName),
        byDepartment: departmentCounts(counted, this.departmentName),
        monthlyTrend: monthlyTrend(counted, today),
        // Case numbers only, and never a case the caller is the subject of (it would name it).
        recentActivity: counted
          .filter((r) => !this.access.isSubject(r, actor))
          .flatMap((r) => r.history.map((h) => ({ record: r, h })))
          .sort((a, b) => b.h.occurredAtUtc.localeCompare(a.h.occurredAtUtc) || b.h.id - a.h.id)
          .slice(0, 10)
          .map(({ record, h }) => ({
            caseId: record.id,
            caseNo: record.caseNo ?? '',
            action: h.action,
            toStatus: h.toStatus,
            occurredAtUtc: h.occurredAtUtc,
          })),
      };
    });
  }

  reviewDashboard(): Observable<GrievanceReviewDashboard> {
    return this.respond(() => {
      const actor = this.access.actor();
      this.access.require(actor, ['grievance-review', 'View']);
      const today = todayIso();
      const visible = countedCases(this.store.records()).filter((r) => r.archivedAtUtc === null);
      const queue = visible.filter((r) => this.access.inReviewQueue(r));
      const pending = queue.flatMap((r) => r.proposals.filter((p) => p.decision === 'Pending'));
      const oldest = pending.map((p) => p.proposedAtUtc).sort()[0];
      const decided = this.store
        .records()
        .flatMap((r) => r.proposals)
        .filter((p) => p.decidedByUserId === actor.userId);
      return {
        asOfDate: today,
        pendingProposals: pending.length,
        escalated: queue.filter((r) => r.isEscalated && GRIEVANCE_OPEN_STATUSES.includes(r.status))
          .length,
        immediateEscalations: queue.filter(
          (r) =>
            GRIEVANCE_OPEN_STATUSES.includes(r.status) &&
            r.escalations.some((e) => e.isImmediate && e.clearedAtUtc === null),
        ).length,
        overdue: queue.filter((r) => grievanceIsOverdue(r, today)).length,
        oldestPendingDays: oldest === undefined ? null : diffInDays(oldest.slice(0, 10), today),
        decidedByMe: {
          approved: decided.filter((p) => p.decision === 'Approved').length,
          rejected: decided.filter((p) => p.decision === 'Rejected').length,
        },
        byPriority: priorityCounts(queue),
        byCategory: categoryCounts(queue, this.categoryName),
      };
    });
  }

  casesReport(
    request: PageRequest<GrievanceReportFilter>,
  ): Observable<PagedResult<GrievanceReportRow>> {
    return this.respond(() => {
      const actor = this.access.actor();
      this.access.require(actor, ['grievance-management', 'View']);
      if (request.sort?.active && !GRIEVANCE_REPORT_SORT_KEYS.includes(request.sort.active)) {
        validation(`Sorting by "${request.sort.active}" is not supported.`, 'sort');
      }
      const today = todayIso();
      const rows = this.reportRows(request.filters ?? {}, today, actor);
      const sorted = request.sort?.active
        ? sortItems(rows, request.sort)
        : rows.sort((a, b) => b.submittedOn.localeCompare(a.submittedOn) || b.caseId - a.caseId);
      return paginate(sorted, request.pageIndex, request.pageSize);
    });
  }

  /**
   * The `grievance-cases` Excel export (export only, `grievance-management:View`): the report rows
   * with the same filters (scope `filtered`), all cases (`all`) or the chosen case ids
   * (`selected`), newest submission first. Each export is written to the access log. The columns
   * never include the subject, the description, notes or the submitter.
   */
  exportRows(request: ExportRequest, limit: number): ExportRecord[] {
    const actor = this.access.actor();
    this.access.require(actor, ['grievance-management', 'View']);
    const today = todayIso();
    let rows: GrievanceReportRow[];
    if (request.scope === 'selected') {
      const ids = new Set(request.selectedIds ?? []);
      rows = this.reportRows({}, today, actor).filter((r) => ids.has(r.caseId));
    } else {
      const filtered = request.scope === 'filtered';
      const filters = (filtered ? (request.filters ?? {}) : {}) as Partial<GrievanceReportFilter>;
      // The Filtered scope also takes the case-number search (never the subject), as the API does.
      const search = filtered ? (request.search ?? '').trim().toLowerCase() : '';
      rows = this.reportRows(filters, today, actor).filter(
        (r) => !search || r.caseNo.toLowerCase().includes(search),
      );
    }
    // Newest submission first, then the case number (the API's order).
    const exported = rows
      .sort(
        (a, b) =>
          b.submittedOn.localeCompare(a.submittedOn) ||
          (a.caseNo < b.caseNo ? -1 : a.caseNo > b.caseNo ? 1 : 0),
      )
      .slice(0, limit + 1);
    // One row per export, with no case (it covers many), as the API writes it.
    this.store.audit(
      null,
      'Export',
      actor.userId,
      'Management',
      'module:grievance-cases;rows:' + Math.min(exported.length, limit),
    );
    // The API's row: the status and priority codes, nulls left empty, real booleans.
    return exported.map((r) => ({
      caseNo: r.caseNo,
      category: r.categoryName,
      status: r.status,
      priority: r.priority,
      department: r.departmentName,
      submittedOn: r.submittedOn,
      targetResolutionDate: r.targetResolutionDate,
      resolvedOn: r.resolvedOn,
      resolutionDays: r.resolutionDays,
      isOverdue: r.isOverdue,
      leadInvestigator: r.leadInvestigatorName,
      isEscalated: r.isEscalated,
    }));
  }

  /**
   * The report rows matching the filters (every non-Draft case, archived ones included), except the
   * cases the caller is the subject employee of. Self-submitted cases stay (leaving them out would
   * show what the caller submitted); the aggregates are the same for everyone.
   */
  private reportRows(
    f: Partial<GrievanceReportFilter>,
    today: string,
    actor: GrievanceMockActor,
  ): GrievanceReportRow[] {
    const set = (key: keyof GrievanceReportFilter): boolean => !isEmptyFilterValue(f[key]);
    return this.countedAll()
      .filter((r) => !this.access.isSubject(r, actor))
      .filter((r) => {
        const submitted = (r.submittedAtUtc ?? '').slice(0, 10);
        const days = resolutionDays(r);
        return (
          (!set('submittedFrom') || submitted >= f.submittedFrom!) &&
          (!set('submittedTo') || submitted <= f.submittedTo!) &&
          (!set('departmentId') || (!r.isAnonymous && r.departmentId === f.departmentId)) &&
          (!set('categoryId') || r.categoryId === f.categoryId) &&
          (!set('status') || r.status === f.status) &&
          (!set('priority') || r.priority === f.priority) &&
          (!set('investigatorEmployeeId') ||
            r.assignments.some(
              (a) =>
                a.unassignedAtUtc === null && a.investigatorEmployeeId === f.investigatorEmployeeId,
            )) &&
          (!set('minResolutionDays') || (days !== null && days >= f.minResolutionDays!)) &&
          (!set('maxResolutionDays') || (days !== null && days <= f.maxResolutionDays!)) &&
          (!set('overdue') || grievanceIsOverdue(r, today) === f.overdue)
        );
      })
      .map((r) => this.row(r, today));
  }

  private row(r: GrievanceCaseRecord, today: string): GrievanceReportRow {
    const team = this.redaction.teamCase(r, today);
    const submittedOn = (r.submittedAtUtc ?? r.createdAtUtc).slice(0, 10);
    const resolvedOn =
      r.status === 'Resolved' || r.status === 'Closed'
        ? (r.resolvedAtUtc?.slice(0, 10) ?? null)
        : null;
    return {
      caseId: r.id,
      caseNo: team.caseNo,
      categoryName: team.categoryName,
      status: r.status,
      priority: r.priority,
      departmentId: team.departmentId,
      departmentName: r.isAnonymous ? GRIEVANCE_NOT_DISCLOSED_LABEL : team.departmentName,
      submittedOn,
      targetResolutionDate: r.targetResolutionDate,
      resolvedOn,
      resolutionDays: resolutionDays(r),
      ageDays: diffInDays(submittedOn, resolvedOn ?? today),
      isOverdue: team.isOverdue,
      leadInvestigatorName: team.leadInvestigatorName,
      isEscalated: r.isEscalated,
    };
  }

  summary(filters?: Partial<GrievanceSummaryFilter>): Observable<GrievanceSummary> {
    return this.respond(() => {
      const actor = this.access.actor();
      this.access.require(actor, ['grievance-management', 'View'], ['grievance-review', 'View']);
      const f = filters ?? {};
      const set = (key: keyof GrievanceSummaryFilter): boolean => !isEmptyFilterValue(f[key]);
      const today = todayIso();
      const counted = this.countedAll().filter((r) => {
        const submitted = (r.submittedAtUtc ?? '').slice(0, 10);
        return (
          (!set('submittedFrom') || submitted >= f.submittedFrom!) &&
          (!set('submittedTo') || submitted <= f.submittedTo!) &&
          (!set('categoryId') || r.categoryId === f.categoryId) &&
          (!set('priority') || r.priority === f.priority)
        );
      });
      return {
        asOfDate: today,
        total: counted.length,
        open: countIn(counted, GRIEVANCE_OPEN_STATUSES),
        resolved: countIn(counted, ['Resolved']),
        closed: countIn(counted, ['Closed']),
        rejected: countIn(counted, ['Rejected']),
        overdue: counted.filter((r) => grievanceIsOverdue(r, today)).length,
        escalated: counted.filter(
          (r) => r.isEscalated && GRIEVANCE_OPEN_STATUSES.includes(r.status),
        ).length,
        resolutionRatePct: resolutionRatePct(counted),
        averageResolutionDays: averageResolutionDays(counted),
        byStatus: statusCounts(counted),
        byPriority: priorityCounts(counted),
        byCategory: categoryCounts(counted, this.categoryName),
        byDepartment: departmentCounts(counted, this.departmentName),
      };
    });
  }
}
