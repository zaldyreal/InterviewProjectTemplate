import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { Observable, tap } from 'rxjs';

import { environment } from '../../../environments/environment';
import { AdminLoginRequest, AdminLoginResponse } from '../models/mood.models';

interface StoredSession {
  accessToken: string;
  expiresAtUtc: string;
  username: string;
}

@Injectable({ providedIn: 'root' })
export class AuthService {
  /**
   * sessionStorage rather than localStorage: the admin token is cleared when the browser tab closes,
   * which limits the window in which a token left on a shared machine is usable.
   *
   * Storing a JWT in web storage is a known trade-off — it is readable by any script on the page, so
   * an XSS bug exposes it. The more robust alternative is an HttpOnly cookie session for the admin
   * too; that is called out in the README as a next step.
   */
  private static readonly storageKey = 'mood_tracker_admin_session';

  private readonly http = inject(HttpClient);

  private readonly session = signal<StoredSession | null>(this.readStoredSession());

  readonly isAuthenticated = computed(() => {
    const current = this.session();

    return current !== null && new Date(current.expiresAtUtc).getTime() > Date.now();
  });

  readonly username = computed(() => this.session()?.username ?? null);

  login(request: AdminLoginRequest): Observable<AdminLoginResponse> {
    return this.http
      .post<AdminLoginResponse>(`${environment.apiUrl}/api/auth/login`, request)
      .pipe(tap(response => this.storeSession(response)));
  }

  logout(): void {
    this.session.set(null);
    sessionStorage.removeItem(AuthService.storageKey);
  }

  /** Returns the bearer token, or null when there is no valid session. */
  getAccessToken(): string | null {
    return this.isAuthenticated() ? this.session()!.accessToken : null;
  }

  private storeSession(response: AdminLoginResponse): void {
    const stored: StoredSession = {
      accessToken: response.accessToken,
      expiresAtUtc: response.expiresAtUtc,
      username: response.username
    };

    this.session.set(stored);
    sessionStorage.setItem(AuthService.storageKey, JSON.stringify(stored));
  }

  private readStoredSession(): StoredSession | null {
    const raw = sessionStorage.getItem(AuthService.storageKey);

    if (!raw) {
      return null;
    }

    try {
      const parsed = JSON.parse(raw) as StoredSession;

      // Corrupt or partial storage is discarded rather than trusted; otherwise a malformed entry
      // would make every subsequent request fail in a confusing way.
      return parsed.accessToken && parsed.expiresAtUtc ? parsed : null;
    } catch {
      return null;
    }
  }
}
