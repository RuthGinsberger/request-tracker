import { Routes } from '@angular/router';

export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'requests' },
  {
    path: 'requests',
    title: 'Requests',
    loadComponent: () =>
      import('./features/requests/requests-page.component').then((m) => m.RequestsPageComponent),
  },
  { path: '**', redirectTo: 'requests' },
];
