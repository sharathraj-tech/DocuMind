import { Component, inject } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { Button } from 'primeng/button';

import { AuthService } from '../core/auth/auth.service';

@Component({
  selector: 'app-shell',
  host: {
    class: 'block h-screen overflow-hidden',
  },
  imports: [RouterOutlet, RouterLink, RouterLinkActive, Button],
  template: `
    <div class="flex h-screen overflow-hidden bg-surface-50">
      <aside class="hidden w-60 shrink-0 flex-col border-r border-surface-200/80 bg-surface-0 md:flex">
        <div class="flex h-16 items-center gap-2 px-5">
          <span class="flex h-9 w-9 items-center justify-center rounded-xl bg-primary text-primary-contrast shadow-sm">
            <i class="pi pi-sparkles text-sm"></i>
          </span>
          <div>
            <div class="font-semibold tracking-tight">DocuMind</div>
            <div class="text-xs text-surface-500">Knowledge workspace</div>
          </div>
        </div>

        <nav class="flex flex-col gap-1 px-3" aria-label="Main">
          <a
            routerLink="/chat"
            routerLinkActive="bg-primary-50 text-primary shadow-[inset_3px_0_0_var(--p-primary-color)]"
            class="flex items-center gap-3 rounded-lg px-3 py-2.5 text-sm font-medium text-surface-600 transition hover:bg-surface-100"
          >
            <i class="pi pi-comments text-base"></i> Chat
          </a>
          <a
            routerLink="/documents"
            routerLinkActive="bg-primary-50 text-primary shadow-[inset_3px_0_0_var(--p-primary-color)]"
            class="flex items-center gap-3 rounded-lg px-3 py-2.5 text-sm font-medium text-surface-600 transition hover:bg-surface-100"
          >
            <i class="pi pi-file text-base"></i> Documents
          </a>
        </nav>

        <div class="mt-auto border-t border-surface-200/80 p-3">
          <div class="mb-2 flex items-center gap-3 rounded-lg px-2 py-2">
            <span class="flex h-9 w-9 shrink-0 items-center justify-center rounded-full bg-primary-100 text-sm font-semibold text-primary">
              {{ (auth.user()?.email || 'U').slice(0, 1).toUpperCase() }}
            </span>
            <div class="min-w-0 flex-1">
              <div class="truncate text-sm font-medium">{{ auth.user()?.email }}</div>
              <div class="text-xs capitalize text-surface-500">{{ auth.user()?.role }}</div>
            </div>
          </div>
          <p-button
            label="Sign out"
            icon="pi pi-sign-out"
            severity="secondary"
            [text]="true"
            size="small"
            styleClass="w-full justify-start"
            (onClick)="auth.logout()"
          />
        </div>
      </aside>

      <main class="flex h-full min-h-0 min-w-0 flex-1 flex-col overflow-hidden">
        <router-outlet />
        <nav class="grid h-16 shrink-0 grid-cols-2 border-t border-surface-200/80 bg-surface-0 px-3 md:hidden" aria-label="Mobile main">
          <a
            routerLink="/chat"
            routerLinkActive="text-primary"
            class="flex flex-col items-center justify-center gap-1 text-xs font-medium text-surface-500"
          >
            <i class="pi pi-comments text-lg"></i>
            Chat
          </a>
          <a
            routerLink="/documents"
            routerLinkActive="text-primary"
            class="flex flex-col items-center justify-center gap-1 text-xs font-medium text-surface-500"
          >
            <i class="pi pi-file text-lg"></i>
            Documents
          </a>
        </nav>
      </main>
    </div>
  `,
})
export class Shell {
  protected readonly auth = inject(AuthService);
}