import { Injectable, inject } from '@angular/core';
import { CURRENT_EMPLOYMENT_STATUSES } from '../../core/models/employee.models';
import { GrievanceActorRole, GrievanceViewerRole } from '../../core/models/grievance.models';
import {
  ADMIN_ROLE_NAME,
  AppUser,
  PERMISSION_MODULES,
  PermissionGrants,
} from '../../core/models/settings.models';
import { CurrentUserService } from '../../core/services/current-user.service';
import { GrievanceCaseRecord } from '../../mock-data/grievance/grievance-records';
import { NotificationMockService } from '../activity/notification-mock.service';
import { EmployeeMockService } from '../employees/employee-mock.service';
import { mockError } from '../mock-response';
import {
  RoleMockService,
  RolePermissionMockService,
  UserMockService,
} from '../settings/settings-mock.services';
import { GrievanceMockStore } from './grievance-mock-store';
import { GrievanceActorRights, isGrievanceOpen } from '../../core/utilities/grievance-workflow';

export type GrievanceModule =
  'grievance' | 'grievance-investigation' | 'grievance-management' | 'grievance-review';
type Action = 'View' | 'Create' | 'Edit' | 'Delete' | 'Approve';

/**
 * Who is acting, as the grievance mock understands it. DEMO ONLY, NO REAL SECURITY: the grants come
 * from the simulated session's role in Settings → Permissions (or from `permissions` when a session
 * carries them), and nothing outside these mock services honours them.
 */
export interface GrievanceMockActor {
  userId: number;
  /** Null for an account without a linked employee (it cannot submit or be assigned). */
  employeeId: number | null;
  name: string;
  /** HR Administrator: the API's AdministratorPolicy (the case access log). */
  isAdmin: boolean;
  can: (module: GrievanceModule, action: Action) => boolean;
}

const everything = (): PermissionGrants =>
  Object.fromEntries(PERMISSION_MODULES.map((m) => [m.key, [...m.actions]]));

/**
 * Actor, tiers, rights and notification recipients for the grievance mock services. Simulation of
 * the API's `Application/Grievances` access rules; it enforces nothing outside the demo mocks.
 */
@Injectable({ providedIn: 'root' })
export class GrievanceMockAccess {
  private readonly user = inject(CurrentUserService);
  private readonly roles = inject(RoleMockService);
  private readonly permissions = inject(RolePermissionMockService);
  private readonly users = inject(UserMockService);
  private readonly employees = inject(EmployeeMockService);
  private readonly notifications = inject(NotificationMockService);
  private readonly store = inject(GrievanceMockStore);

  // ---- Actor ----------------------------------------------------------------------------------

  actor(): GrievanceMockActor {
    const current = this.user.current();
    const isAdmin = current.roles.includes(ADMIN_ROLE_NAME);
    let grants: PermissionGrants;
    if (current.permissions) {
      grants = {};
      for (const key of current.permissions) {
        const [module, action] = key.split(':');
        grants[module] = [...(grants[module] ?? []), action as Action];
      }
    } else if (isAdmin) {
      grants = everything();
    } else {
      grants = {};
      for (const role of this.roles.records().filter((r) => current.roles.includes(r.name))) {
        for (const [module, actions] of Object.entries(this.permissions.grantsNow(role.id))) {
          grants[module] = [...new Set([...(grants[module] ?? []), ...actions])];
        }
      }
    }
    return {
      userId: current.id,
      employeeId: current.employeeId !== null && current.employeeId > 0 ? current.employeeId : null,
      name: current.displayName,
      isAdmin,
      can: (module, action) => (grants[module] ?? []).includes(action),
    };
  }

  /** 403 unless the actor holds at least one of the permissions. */
  require(actor: GrievanceMockActor, ...keys: [GrievanceModule, Action][]): void {
    if (!keys.some(([module, action]) => actor.can(module, action))) {
      throw mockError('Forbidden', 'You do not have permission to do this.');
    }
  }

  // ---- Tiers and rights -----------------------------------------------------------------------

  /** The caller is the subject employee: excluded from every case-team tier of the case. */
  isSubject(record: GrievanceCaseRecord, actor: GrievanceMockActor): boolean {
    return actor.employeeId !== null && record.subjectEmployeeId === actor.employeeId;
  }

  /**
   * The caller is the subject or the submitter (named or anonymous): every case-team tier silently
   * leaves the case out of lists and details, as if it did not exist (404). Aggregate figures still
   * count it (identical for every viewer); report rows, export rows and recent activity leave out
   * subject cases only.
   */
  isConflicted(record: GrievanceCaseRecord, actor: GrievanceMockActor): boolean {
    return this.isSubject(record, actor) || this.store.isSubmitter(record.id, actor.employeeId);
  }

  activeLead(record: GrievanceCaseRecord): number | null {
    return (
      record.assignments.find((a) => a.role === 'Lead' && a.unassignedAtUtc === null)
        ?.investigatorEmployeeId ?? null
    );
  }

  private activelyAssigned(record: GrievanceCaseRecord, employeeId: number | null): boolean {
    return (
      employeeId !== null &&
      record.assignments.some(
        (a) => a.investigatorEmployeeId === employeeId && a.unassignedAtUtc === null,
      )
    );
  }

  /**
   * The case-team tiers of the caller on a case (never a Draft, never on a case the caller submitted
   * or is the subject of):
   *  - Management: `grievance-management:View` (archived cases included; lists hide them);
   *  - Investigator: `grievance-investigation:View` and an active assignment, not archived;
   *  - Reviewer: `grievance-review:View`, not archived, and the case escalated and open, or with a
   *    pending proposal, or with a proposal the caller decided.
   */
  tiers(record: GrievanceCaseRecord, actor: GrievanceMockActor): GrievanceViewerRole[] {
    if (record.status === 'Draft' || this.isConflicted(record, actor)) {
      return [];
    }
    const tiers: GrievanceViewerRole[] = [];
    if (actor.can('grievance-management', 'View')) {
      tiers.push('Management');
    }
    const archived = record.archivedAtUtc !== null;
    if (
      !archived &&
      actor.can('grievance-investigation', 'View') &&
      this.activelyAssigned(record, actor.employeeId)
    ) {
      tiers.push('Investigator');
    }
    if (!archived && actor.can('grievance-review', 'View') && this.inReviewScope(record, actor)) {
      tiers.push('Reviewer');
    }
    return tiers;
  }

  /** The review queue: escalated and open, or a pending proposal. */
  inReviewQueue(record: GrievanceCaseRecord): boolean {
    return (
      (record.isEscalated && isGrievanceOpen(record.status)) ||
      record.proposals.some((p) => p.decision === 'Pending')
    );
  }

  private inReviewScope(record: GrievanceCaseRecord, actor: GrievanceMockActor): boolean {
    return (
      this.inReviewQueue(record) || record.proposals.some((p) => p.decidedByUserId === actor.userId)
    );
  }

  /** A case visible to the case team of the caller, else NotFound (outside the scope: 404). */
  visibleCase(id: number, actor: GrievanceMockActor): GrievanceCaseRecord {
    const record = this.store.find(id);
    if (!record || this.tiers(record, actor).length === 0) {
      throw mockError('NotFound', `Grievance case #${id} was not found.`);
    }
    return record;
  }

  /** The caller's rights on one case, for the workflow. */
  rights(record: GrievanceCaseRecord, actor: GrievanceMockActor): GrievanceActorRights {
    const tiers = this.tiers(record, actor);
    const management = tiers.includes('Management');
    const investigatorEdit =
      tiers.includes('Investigator') && actor.can('grievance-investigation', 'Edit');
    const pending = record.proposals.find((p) => p.decision === 'Pending');
    return {
      owner:
        actor.can('grievance', 'Create') && this.store.isSubmitter(record.id, actor.employeeId),
      managementEdit: management && actor.can('grievance-management', 'Edit'),
      managementDelete: management && actor.can('grievance-management', 'Delete'),
      investigatorEdit,
      leadInvestigator: investigatorEdit && this.activeLead(record) === actor.employeeId,
      reviewApprove: !this.isConflicted(record, actor) && actor.can('grievance-review', 'Approve'),
      // Deciding a proposal: the reviewer tier (review View and Approve), never an active
      // investigator of the case.
      reviewDecide:
        tiers.includes('Reviewer') &&
        actor.can('grievance-review', 'Approve') &&
        !this.activelyAssigned(record, actor.employeeId),
      isProposer: pending !== undefined && pending.proposedByUserId === actor.userId,
    };
  }

  /** The role an actor acts in on a case (history, messages, uploads, access log). */
  actingRole(rights: GrievanceActorRights, prefer: 'team' | 'review' = 'team'): GrievanceActorRole {
    if (prefer === 'review' && rights.reviewApprove) {
      return 'Reviewer';
    }
    if (rights.managementEdit || rights.managementDelete) {
      return 'Management';
    }
    if (rights.investigatorEdit) {
      return 'Investigator';
    }
    return rights.reviewApprove ? 'Reviewer' : 'Management';
  }

  // ---- People ---------------------------------------------------------------------------------

  /** True when the employee is still employed (assignable). */
  isCurrentEmployee(employeeId: number): boolean {
    const employee = this.employees.findById(employeeId);
    return (
      employee !== undefined && CURRENT_EMPLOYMENT_STATUSES.includes(employee.employmentStatus)
    );
  }

  private grantsOfUser(user: AppUser): (module: GrievanceModule, action: Action) => boolean {
    const role = this.roles.findById(user.roleId);
    const grants =
      role?.name === ADMIN_ROLE_NAME ? everything() : this.permissions.grantsNow(user.roleId);
    return (module, action) => (grants[module] ?? []).includes(action);
  }

  /** Active users whose role holds the permission (and who are linked to a current employee). */
  usersWith(module: GrievanceModule, action: Action): AppUser[] {
    return this.users
      .records()
      .filter(
        (u) =>
          u.status === 'Active' &&
          this.isCurrentEmployee(u.employeeId) &&
          this.grantsOfUser(u)(module, action),
      );
  }

  /** The active user of an employee, if any. */
  userOfEmployee(employeeId: number): AppUser | undefined {
    return this.users.records().find((u) => u.employeeId === employeeId && u.status === 'Active');
  }

  // ---- Notifications (the API's GrievanceNotifications; in-app only) -------------------------

  /**
   * Sends one notification per recipient user, never to the subject employee of the case. A
   * case-team step leaves out the acting user; a step of the submitter does not (leaving them out of
   * "a new grievance was submitted" would show who submitted). The text is the case number and the
   * event only, never the category, the subject or a name: title "Grievance GRV-…", message
   * "Grievance GRV-…: <event>." Simulation of `INotificationWriter`.
   */
  private send(
    record: GrievanceCaseRecord,
    happened: string,
    link: string,
    userIds: readonly number[],
    byCaseTeam = true,
  ): void {
    const actorId = this.user.current().id;
    const subjectUser =
      record.subjectEmployeeId === null ? undefined : this.userOfEmployee(record.subjectEmployeeId);
    const recipients = userIds.filter(
      (id) => (!byCaseTeam || id !== actorId) && id !== subjectUser?.id,
    );
    const caseNo = record.caseNo ?? '';
    this.notifications.add(
      {
        title: `Grievance ${caseNo}`,
        message: `Grievance ${caseNo}: ${happened}.`,
        icon: 'gavel',
        link,
      },
      recipients,
    );
  }

  /**
   * The submitter of a NAMED case, about something they may see (a visible history step, a message
   * from HR, a shared file). Nothing for an anonymous case: they see an unread badge instead.
   */
  notifySubmitter(record: GrievanceCaseRecord): void {
    if (record.isAnonymous) {
      return;
    }
    const employeeId = this.store.namedSubmitterId(record.id);
    const user = employeeId === null ? undefined : this.userOfEmployee(employeeId);
    if (user) {
      this.send(record, 'there is an update on your grievance', `/grievances/my/${record.id}`, [
        user.id,
      ]);
    }
  }

  /**
   * Every user with `grievance-management:View`, about a step of the submitter: "a new grievance
   * was submitted" or "the employee responded".
   */
  notifyManagement(record: GrievanceCaseRecord, happened: string): void {
    this.send(
      record,
      happened,
      `/grievances/cases/${record.id}`,
      this.usersWith('grievance-management', 'View').map((u) => u.id),
      false,
    );
  }

  /**
   * Every user with `grievance-review:Approve`: "a resolution was proposed" or "it was escalated as
   * urgent" (an immediate escalation only).
   */
  notifyReviewers(record: GrievanceCaseRecord, happened: string): void {
    this.send(
      record,
      happened,
      `/grievances/reviews/${record.id}`,
      this.usersWith('grievance-review', 'Approve').map((u) => u.id),
    );
  }

  /**
   * Every active investigator of the case (not the subject employee), about a step of the
   * submitter: "the employee responded". Nobody is left out for being the actor (the submitter's
   * step), as the API's `ToActiveInvestigatorsAsync`.
   */
  notifyActiveInvestigators(record: GrievanceCaseRecord, happened: string): void {
    const employees = new Set(
      record.assignments
        .filter((a) => a.unassignedAtUtc === null)
        .map((a) => a.investigatorEmployeeId)
        .filter((id) => id !== record.subjectEmployeeId),
    );
    const users = [...employees]
      .map((id) => this.userOfEmployee(id)?.id)
      .filter((id): id is number => id !== undefined);
    if (users.length > 0) {
      this.send(record, happened, `/grievances/investigations/${record.id}`, users, false);
    }
  }

  /** The investigator just assigned (their active user), unless they assigned themselves. */
  notifyInvestigator(record: GrievanceCaseRecord, employeeId: number): void {
    if (employeeId === record.subjectEmployeeId || employeeId === this.user.current().employeeId) {
      return;
    }
    const user = this.userOfEmployee(employeeId);
    if (user) {
      this.send(
        record,
        'you were assigned to investigate it',
        `/grievances/investigations/${record.id}`,
        [user.id],
      );
    }
  }
}
