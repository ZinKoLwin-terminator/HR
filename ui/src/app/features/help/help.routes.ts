import { Routes } from '@angular/router';

export const HELP_ROUTES: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'modules' },
  {
    path: 'modules',
    title: 'Module Guide',
    data: { breadcrumb: 'Module Guide' },
    loadComponent: () => import('./module-guide.component').then((m) => m.ModuleGuideComponent),
  },
  {
    path: 'workflows',
    title: 'Workflows',
    data: { breadcrumb: 'Workflows' },
    loadComponent: () => import('./workflow-guide.component').then((m) => m.WorkflowGuideComponent),
  },
];
