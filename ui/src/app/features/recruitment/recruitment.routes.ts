import { Routes } from '@angular/router';

export const RECRUITMENT_ROUTES: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'job-openings' },
  {
    path: 'job-openings',
    title: 'Job Openings',
    data: { breadcrumb: 'Job Openings' },
    loadComponent: () =>
      import('./job-openings/job-openings-page.component').then((m) => m.JobOpeningsPageComponent),
  },
  {
    path: 'candidates',
    title: 'Candidates',
    data: { breadcrumb: 'Candidates' },
    loadComponent: () =>
      import('./candidates/candidates-page.component').then((m) => m.CandidatesPageComponent),
  },
  {
    path: 'interviews',
    title: 'Interviews',
    data: { breadcrumb: 'Interviews' },
    loadComponent: () =>
      import('./interviews/interviews-page.component').then((m) => m.InterviewsPageComponent),
  },
  {
    path: 'pipeline',
    title: 'Recruitment Pipeline',
    data: { breadcrumb: 'Pipeline' },
    loadComponent: () =>
      import('./pipeline/recruitment-pipeline.component').then(
        (m) => m.RecruitmentPipelineComponent,
      ),
  },
];
