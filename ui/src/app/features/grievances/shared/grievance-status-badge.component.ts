import { Component, computed, input } from '@angular/core';
import { GRIEVANCE_STATUS_LABELS, GrievanceStatus } from '../../../core/models/grievance.models';
import { StatusBadgeComponent } from '../../../shared/components/status-badge/status-badge.component';

/** A grievance status as the shared status pill, with its display label ("Under Review"). */
@Component({
  selector: 'app-grievance-status-badge',
  imports: [StatusBadgeComponent],
  template: `<app-status-badge [status]="label()" />`,
})
export class GrievanceStatusBadgeComponent {
  readonly status = input.required<GrievanceStatus>();
  protected readonly label = computed(() => GRIEVANCE_STATUS_LABELS[this.status()]);
}

/** The display label of a status (for table value accessors and text). */
export const grievanceStatusLabel = (status: GrievanceStatus): string =>
  GRIEVANCE_STATUS_LABELS[status];
