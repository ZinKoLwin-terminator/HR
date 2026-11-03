import { Routes } from '@angular/router';
import { placeholderRoutes } from '../../shared/utilities/placeholder-routes';

export const RECRUITMENT_ROUTES: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'job-openings' },
  ...placeholderRoutes(9, [
    {
      path: 'job-openings',
      label: 'Job Openings',
      description: 'Open requisitions and vacancies.',
    },
    {
      path: 'candidates',
      label: 'Candidates',
      description: 'Candidate profiles and applications.',
    },
    { path: 'interviews', label: 'Interviews', description: 'Interview scheduling and feedback.' },
    {
      path: 'pipeline',
      label: 'Recruitment Pipeline',
      description: 'Applications by hiring stage.',
    },
  ]),
];
