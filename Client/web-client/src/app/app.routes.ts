import { Routes } from '@angular/router';

import { adminGuard } from './core/guards/admin.guard';

export const routes: Routes = [
  {
    path: '',
    // Lazy-loaded so the admin screens are not downloaded by the everyday user who only submits a
    // mood. Each route is a standalone component, so no feature module is needed.
    loadComponent: () =>
      import('./features/mood/mood-tracker.component').then(m => m.MoodTrackerComponent),
    title: 'How are you feeling today?'
  },
  {
    path: 'admin/login',
    loadComponent: () =>
      import('./features/admin/admin-login.component').then(m => m.AdminLoginComponent),
    title: 'Admin sign in'
  },
  {
    path: 'admin',
    canActivate: [adminGuard],
    loadComponent: () =>
      import('./features/admin/admin-moods.component').then(m => m.AdminMoodsComponent),
    title: 'Mood entries'
  },
  { path: '**', redirectTo: '' }
];
