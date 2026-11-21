import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { Button } from 'primeng/button';
import { InputText } from 'primeng/inputtext';
import { Password } from 'primeng/password';

import { AuthService } from '../../core/auth/auth.service';
import { problemMessage } from '../../core/utils';

@Component({
  selector: 'app-register-page',
  imports: [ReactiveFormsModule, RouterLink, InputText, Password, Button],
  template: `
    <div class="relative flex min-h-screen items-center justify-center overflow-hidden p-4 sm:p-6">
      <div class="pointer-events-none absolute -left-32 -top-32 h-80 w-80 rounded-full bg-primary-100/60 blur-3xl"></div>
      <div class="pointer-events-none absolute -bottom-40 -right-32 h-96 w-96 rounded-full bg-surface-200/70 blur-3xl"></div>
      <form
        [formGroup]="form"
        (ngSubmit)="submit()"
        class="relative w-full max-w-md space-y-4 rounded-2xl border border-surface-200/80 bg-surface-0 p-6 shadow-[0_20px_60px_-24px_rgba(15,23,42,.25)] sm:p-8"
      >
        <div class="mb-2 flex items-center gap-3">
          <span class="flex h-11 w-11 items-center justify-center rounded-xl bg-primary text-primary-contrast shadow-sm">
            <i class="pi pi-sparkles"></i>
          </span>
          <div>
            <div class="text-xs font-semibold uppercase tracking-[.18em] text-primary">DocuMind</div>
            <h1 class="mt-0.5 text-2xl font-semibold tracking-tight">Create your workspace</h1>
          </div>
        </div>

        @if (error(); as message) {
          <div class="rounded-lg bg-red-50 px-3 py-2 text-sm text-red-700" role="alert">{{ message }}</div>
        }

        <div class="space-y-1">
          <label for="workspaceName" class="text-sm font-medium">Workspace name</label>
          <input pInputText id="workspaceName" formControlName="workspaceName" class="w-full" />
        </div>

        <div class="space-y-1">
          <label for="displayName" class="text-sm font-medium">Your name</label>
          <input pInputText id="displayName" formControlName="displayName" autocomplete="name" class="w-full" />
        </div>

        <div class="space-y-1">
          <label for="email" class="text-sm font-medium">Email</label>
          <input pInputText id="email" type="email" formControlName="email" autocomplete="username" class="w-full" />
        </div>

        <div class="space-y-1">
          <label for="password" class="text-sm font-medium">Password</label>
          <p-password
            inputId="password"
            formControlName="password"
            autocomplete="new-password"
            [toggleMask]="true"
            [fluid]="true"
          />
          <p class="text-xs text-surface-500">At least 10 characters, with upper and lower case letters and a digit.</p>
        </div>

        <p-button type="submit" label="Create workspace" [loading]="busy()" [disabled]="form.invalid" [fluid]="true" />

        <p class="text-center text-sm text-surface-500">
          Already have an account? <a routerLink="/login" class="text-primary">Sign in</a>
        </p>
      </form>
    </div>
  `,
})
export class RegisterPage {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly form = inject(FormBuilder).nonNullable.group({
    workspaceName: ['', Validators.required],
    displayName: [''],
    email: ['', [Validators.required, Validators.email]],
    password: ['', [Validators.required, Validators.minLength(10)]],
  });

  protected async submit(): Promise<void> {
    if (this.form.invalid || this.busy()) return;

    this.busy.set(true);
    this.error.set(null);
    try {
      await this.auth.register(this.form.getRawValue());
      await this.router.navigateByUrl('/');
    } catch (error) {
      this.error.set(problemMessage(error, 'Registration failed.'));
    } finally {
      this.busy.set(false);
    }
  }
}