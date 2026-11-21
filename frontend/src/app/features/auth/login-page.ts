import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { Button, ButtonDirective } from 'primeng/button';
import { InputText } from 'primeng/inputtext';
import { Password, PasswordDirective } from 'primeng/password';

import { AuthService } from '../../core/auth/auth.service';
import { problemMessage, safeReturnUrl } from '../../core/utils';

@Component({
  selector: 'app-login-page',
  imports: [ReactiveFormsModule, RouterLink, InputText, ButtonDirective, PasswordDirective],
  template: `
    <div class="relative flex min-h-screen items-center justify-center overflow-hidden p-4 sm:p-6">
      <div class="pointer-events-none absolute -left-32 -top-32 h-80 w-80 rounded-full bg-primary-100/60 blur-3xl"></div>
      <div class="pointer-events-none absolute -bottom-40 -right-32 h-96 w-96 rounded-full bg-surface-200/70 blur-3xl"></div>
      <form
        [formGroup]="form"
        (ngSubmit)="submit()"
        class="relative w-full max-w-md space-y-5 rounded-2xl border border-surface-200/80 bg-surface-0 p-6 shadow-[0_20px_60px_-24px_rgba(15,23,42,.25)] sm:p-8"
      >
        <div class="mb-2 flex items-center gap-3">
          <span class="flex h-11 w-11 items-center justify-center rounded-xl bg-primary text-primary-contrast shadow-sm">
            <i class="pi pi-sparkles"></i>
          </span>
          <div>
            <div class="text-xs font-semibold uppercase tracking-[.18em] text-primary">DocuMind</div>
            <h1 class="mt-0.5 text-2xl font-semibold tracking-tight">Welcome back</h1>
          </div>
        </div>
        <p class="!mt-2 text-sm text-surface-500">Sign in to continue to your knowledge workspace.</p>

        @if (error(); as message) {
          <div class="rounded-lg bg-red-50 px-3 py-2 text-sm text-red-700" role="alert">{{ message }}</div>
        }

        <div class="space-y-1">
          <label for="email" class="text-sm font-medium">Email</label>
          <input pInputText id="email" type="email" formControlName="email" autocomplete="username" class="w-full" />
        </div>

        <div class="space-y-1">
          <label for="password" class="text-sm font-medium">Password</label>
          <input
            pPassword
            inputId="password"
            formControlName="password"
            autocomplete="current-password"
            [feedback]="false"
            [fluid]="true"
          >
        </div>

        <button pButton type="submit" [loading]="busy()" [fluid]="true" class="w-full">Sign in</button>

        <p class="text-center text-sm text-surface-500">
          New here? <a routerLink="/register" class="text-primary">Create a workspace</a>
        </p>
      </form>
    </div>
  `,
})
export class LoginPage {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly form = inject(FormBuilder).nonNullable.group({
    email: ['', [Validators.required, Validators.email]],
    password: ['', Validators.required],
  });

  constructor() {
    // Already signed in (for example a reload on /login)? Go straight in.
    void this.auth.init().then(() => {
      if (this.auth.isAuthenticated()) void this.router.navigateByUrl(this.returnUrl());
    });
  }

  protected async submit(): Promise<void> {
    

    this.busy.set(true);
    this.error.set(null);
    try {
      const { email, password } = this.form.getRawValue();
      await this.auth.login(email, password);
      await this.router.navigateByUrl(this.returnUrl());
    } catch (error) {
      this.error.set(problemMessage(error, 'Sign-in failed.'));
    } finally {
      this.busy.set(false);
    }
  }

  private returnUrl(): string {
    return safeReturnUrl(this.route.snapshot.queryParamMap.get('returnUrl'));
  }
}