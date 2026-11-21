import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import { AuthResponse, CurrentUser } from '../api-models';

const REFRESH_KEY = 'documind.refreshToken';
const EXPIRY_MARGIN_MS = 30_000; // refresh a little early so requests never go out with a dying token

interface Session {
  accessToken: string;
  expiresAt: number; // epoch ms
}

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);

  private readonly _session = signal<Session | null>(null);
  private readonly _user = signal<CurrentUser | null>(null);

  readonly user = this._user.asReadonly();
  readonly isAuthenticated = computed(() => this._session() !== null);

  private initPromise: Promise<void> | null = null;
  private refreshPromise: Promise<boolean> | null = null;

  /** Restores a session from the stored refresh token. Safe to call many times. */
  init(): Promise<void> {
    this.initPromise ??= (async () => {
      if (localStorage.getItem(REFRESH_KEY)) await this.refresh();
    })();
    return this.initPromise;
  }

  async login(email: string, password: string): Promise<void> {
    const response = await firstValueFrom(this.http.post<AuthResponse>('/api/auth/login', { email, password }));
    await this.establish(response);
  }

  async register(body: { email: string; password: string; displayName: string; workspaceName: string }): Promise<void> {
    const response = await firstValueFrom(this.http.post<AuthResponse>('/api/auth/register', body));
    await this.establish(response);
  }

  async logout(): Promise<void> {
    const token = localStorage.getItem(REFRESH_KEY);
    this.clearSession();
    if (token) {
      try {
        await firstValueFrom(this.http.post('/api/auth/logout', { refreshToken: token }));
      } catch {
        // already signed out locally; the server-side revoke is best effort
      }
    }
    window.location.assign('/login'); // a full reload also wipes every in-memory store
  }

  /** Ends the session after an unrecoverable 401. */
  endSession(): void {
    this.clearSession();
    window.location.assign('/login');
  }

  /** A valid access token, refreshing first if the current one is about to expire. */
  async getAccessToken(): Promise<string | null> {
    const session = this._session();
    if (!session) return null;

    if (session.expiresAt - Date.now() < EXPIRY_MARGIN_MS) {
      if (!(await this.refresh())) return null;
    }
    return this._session()?.accessToken ?? null;
  }

  /** Single-flight: concurrent callers share one refresh request. */
  refresh(): Promise<boolean> {
    this.refreshPromise ??= this.doRefresh().finally(() => (this.refreshPromise = null));
    return this.refreshPromise;
  }

  private doRefresh(): Promise<boolean> {
    // The lock serializes refreshes across browser tabs. The server rotates refresh tokens and treats a
    // replayed one as theft, so two tabs must never present the same token. Inside the lock we always
    // read the newest token from storage.
    return navigator.locks.request('documind-refresh', async () => {
      const stored = localStorage.getItem(REFRESH_KEY);
      if (!stored) {
        this.clearSession();
        return false;
      }

      try {
        const response = await firstValueFrom(
          this.http.post<AuthResponse>('/api/auth/refresh', { refreshToken: stored }),
        );
        await this.establish(response);
        return true;
      } catch (error) {
        // Only an explicit rejection ends the session; a network blip should not sign people out.
        if (error instanceof HttpErrorResponse && error.status === 401) this.clearSession();
        return false;
      }
    });
  }

  private async establish(response: AuthResponse): Promise<void> {
    localStorage.setItem(REFRESH_KEY, response.refreshToken);
    this._session.set({
      accessToken: response.accessToken,
      expiresAt: Date.parse(response.accessTokenExpiresAt),
    });

    try {
      this._user.set(await firstValueFrom(this.http.get<CurrentUser>('/api/auth/me')));
    } catch {
      // the profile is cosmetic; being signed in is what matters
    }
  }

  private clearSession(): void {
    localStorage.removeItem(REFRESH_KEY);
    this._session.set(null);
    this._user.set(null);
  }
}