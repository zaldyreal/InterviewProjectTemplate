import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';

import { environment } from '../../../environments/environment';
import { AuthService } from './auth.service';

describe('AuthService', () => {
  let service: AuthService;
  let httpMock: HttpTestingController;

  const loginUrl = `${environment.apiUrl}/api/auth/login`;

  const futureExpiry = () => new Date(Date.now() + 60 * 60 * 1000).toISOString();
  const pastExpiry = () => new Date(Date.now() - 60 * 1000).toISOString();

  function createService(): AuthService {
    TestBed.resetTestingModule();

    TestBed.configureTestingModule({
      providers: [AuthService, provideHttpClient(), provideHttpClientTesting()]
    });

    httpMock = TestBed.inject(HttpTestingController);

    return TestBed.inject(AuthService);
  }

  beforeEach(() => {
    sessionStorage.clear();
    service = createService();
  });

  afterEach(() => {
    httpMock.verify();
    sessionStorage.clear();
  });

  it('starts unauthenticated', () => {
    expect(service.isAuthenticated()).toBeFalse();
    expect(service.getAccessToken()).toBeNull();
  });

  it('becomes authenticated after a successful login', () => {
    service.login({ username: 'admin', password: 'good' }).subscribe();

    httpMock.expectOne(loginUrl).flush({
      accessToken: 'token-123',
      expiresAtUtc: futureExpiry(),
      username: 'admin'
    });

    expect(service.isAuthenticated()).toBeTrue();
    expect(service.getAccessToken()).toBe('token-123');
    expect(service.username()).toBe('admin');
  });

  it('restores a session from storage so a page refresh does not sign the admin out', () => {
    service.login({ username: 'admin', password: 'good' }).subscribe();

    httpMock.expectOne(loginUrl).flush({
      accessToken: 'token-123',
      expiresAtUtc: futureExpiry(),
      username: 'admin'
    });

    const restored = createService();

    expect(restored.isAuthenticated()).toBeTrue();
    expect(restored.getAccessToken()).toBe('token-123');
  });

  it('treats an expired stored session as signed out', () => {
    // The token would be rejected by the API anyway; recognising it locally avoids showing the
    // admin page and then failing to populate it.
    sessionStorage.setItem(
      'mood_tracker_admin_session',
      JSON.stringify({
        accessToken: 'stale-token',
        expiresAtUtc: pastExpiry(),
        username: 'admin'
      })
    );

    const restored = createService();

    expect(restored.isAuthenticated()).toBeFalse();
    expect(restored.getAccessToken()).toBeNull();
  });

  it('ignores corrupt session storage instead of throwing', () => {
    sessionStorage.setItem('mood_tracker_admin_session', 'not-json');

    const restored = createService();

    expect(restored.isAuthenticated()).toBeFalse();
  });

  it('clears the session on logout', () => {
    service.login({ username: 'admin', password: 'good' }).subscribe();

    httpMock.expectOne(loginUrl).flush({
      accessToken: 'token-123',
      expiresAtUtc: futureExpiry(),
      username: 'admin'
    });

    service.logout();

    expect(service.isAuthenticated()).toBeFalse();
    expect(sessionStorage.getItem('mood_tracker_admin_session')).toBeNull();
  });

  it('does not authenticate when login fails', () => {
    service.login({ username: 'admin', password: 'wrong' }).subscribe({
      error: () => undefined
    });

    httpMock.expectOne(loginUrl).flush(
      { detail: 'The username or password is incorrect.' },
      { status: 401, statusText: 'Unauthorized' }
    );

    expect(service.isAuthenticated()).toBeFalse();
  });
});
