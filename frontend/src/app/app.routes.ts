import { Routes } from '@angular/router';

import { authGuard } from './core/auth/auth.guard';

export const routes: Routes = [
  { path: 'login', loadComponent: () => import('./features/auth/login-page').then((m) => m.LoginPage) },
  { path: 'register', loadComponent: () => import('./features/auth/register-page').then((m) => m.RegisterPage) },
  {
    path: '',
    canActivate: [authGuard],
    loadComponent: () => import('./layout/shell').then((m) => m.Shell),
    children: [
      { path: '', pathMatch: 'full', redirectTo: 'chat' },
      { path: 'chat', loadComponent: () => import('./features/chat/chat-page').then((m) => m.ChatPage) },
      { path: 'chat/:id', loadComponent: () => import('./features/chat/chat-page').then((m) => m.ChatPage) },
      {
        path: 'documents',
        loadComponent: () => import('./features/documents/documents-page').then((m) => m.DocumentsPage),
      },
    ],
  },
  { path: '**', redirectTo: '' },
];