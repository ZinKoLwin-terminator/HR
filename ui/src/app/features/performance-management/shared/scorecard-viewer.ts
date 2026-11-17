import { Signal, computed, inject } from '@angular/core';
import { CurrentUserService } from '../../../core/services/current-user.service';
import { PermissionService } from '../../../core/services/permission.service';
import { ScorecardViewer } from './scorecard-rules';

/**
 * The signed-in user as the KPI screens see them: their employee, their name (the review history
 * names actors by display name) and the grants that decide which buttons to show. HR tier is
 * `kpi:Approve` with `employees:Edit`, as on the server. UX only: the server enforces.
 * Call in an injection context (a field initializer of a component).
 */
export function injectScorecardViewer(): Signal<ScorecardViewer> {
  const user = inject(CurrentUserService);
  const permissions = inject(PermissionService);
  return computed(() => {
    const current = user.current();
    const canApprove = permissions.can('kpi:Approve');
    return {
      employeeId: current.employeeId !== null && current.employeeId > 0 ? current.employeeId : null,
      displayName: current.displayName,
      canCreate: permissions.can('kpi:Create'),
      canApprove,
      hrTier: canApprove && permissions.can('employees:Edit'),
    };
  });
}
