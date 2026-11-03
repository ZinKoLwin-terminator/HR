import { Component, computed, inject } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import {
  CANDIDATE_SOURCES,
  Candidate,
  candidateName,
} from '../../../core/models/recruitment.models';
import {
  ApplicationDataService,
  CandidateDataService,
} from '../../../core/services/data/recruitment-data.services';
import { CrudPageComponent } from '../../../shared/components/crud/crud-page.component';
import { CrudPageConfig } from '../../../shared/components/crud/crud.models';

const SOURCE_OPTIONS = CANDIDATE_SOURCES.map((s) => ({ value: s, label: s }));

/** Candidate profiles. Applications to job openings are managed on the pipeline board. */
@Component({
  selector: 'app-candidates-page',
  imports: [CrudPageComponent, MatProgressBarModule],
  template: `
    @if (config(); as cfg) {
      <app-crud-page [config]="cfg" />
    } @else {
      <mat-progress-bar mode="indeterminate" aria-label="Loading candidates" />
    }
  `,
})
export class CandidatesPageComponent {
  private readonly candidates = inject(CandidateDataService);
  private readonly applicationsApi = inject(ApplicationDataService);
  private readonly applications = rxResource({
    stream: () => this.applicationsApi.getAll(),
  });

  protected readonly config = computed<CrudPageConfig<Candidate> | null>(() => {
    const apps = this.applications.value();
    if (!apps) {
      return null;
    }
    return {
      title: 'Candidates',
      description:
        'People who applied or were referred. Sample data only — no real personal details.',
      entityLabel: 'Candidate',
      service: this.candidates,
      hasStatus: false,
      searchLabel: 'Search by name, email or employer',
      defaultSort: { active: 'lastName', direction: 'asc' },
      filters: [{ key: 'source', label: 'Source', options: SOURCE_OPTIONS }],
      nameOf: candidateName,
      canDelete: (c) => !apps.some((a) => a.candidateId === c.id),
      columns: [
        { key: 'lastName', header: 'Candidate', sortable: true, value: candidateName },
        { key: 'email', header: 'Email', hideBelow: 'md' },
        { key: 'currentEmployer', header: 'Current employer', hideBelow: 'lg' },
        {
          key: 'experienceYears',
          header: 'Experience',
          align: 'end',
          sortable: true,
          hideBelow: 'sm',
          value: (c) => `${c.experienceYears} yr`,
        },
        { key: 'source', header: 'Source', sortable: true, hideBelow: 'md', nowrap: true },
        {
          key: 'applications',
          header: 'Applications',
          type: 'number',
          align: 'end',
          value: (c) => apps.filter((a) => a.candidateId === c.id).length,
        },
      ],
      newItem: () => ({ source: 'Career site', experienceYears: 0 }),
      fields: [
        { key: 'firstName', label: 'First name', type: 'text', required: true, maxLength: 40 },
        { key: 'lastName', label: 'Last name', type: 'text', required: true, maxLength: 40 },
        { key: 'email', label: 'Email', type: 'email', required: true, maxLength: 100 },
        { key: 'phone', label: 'Phone', type: 'tel', required: true, pattern: 'phone' },
        { key: 'currentEmployer', label: 'Current employer', type: 'text', maxLength: 80 },
        {
          key: 'experienceYears',
          label: 'Years of experience',
          type: 'number',
          required: true,
          min: 0,
          max: 50,
        },
        {
          key: 'source',
          label: 'Source',
          type: 'select',
          required: true,
          options: () => SOURCE_OPTIONS,
        },
        { key: 'resumeName', label: 'Resume (demo: file name only)', type: 'file' },
        { key: 'notes', label: 'Notes', type: 'textarea', maxLength: 300, fullWidth: true },
      ],
    };
  });
}
