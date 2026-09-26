import { TestBed } from '@angular/core/testing';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { Router } from '@angular/router';
import { AuthService } from './auth.service';
import { jwtInterceptor } from '../interceptors/jwt.interceptor';
import { environment } from '../../environments/environment';

describe('Session renewal', () => {
  let http: HttpClient;
  let mock: HttpTestingController;
  let auth: AuthService;
  let router: { url: string; navigate: jasmine.Spy };
  const token = (seconds: number) => `e30.${btoa(JSON.stringify({ exp: Date.now() / 1000 + seconds }))}.sig`;
  const response = () => ({ token: token(3600), expiresAt: '', firstName: 'Test', lastName: '', email: '', role: 'Admin', isActive: true });
  beforeEach(() => {
    localStorage.clear();
    router = { url: '/dashboard/orders', navigate: jasmine.createSpy().and.resolveTo(true) };
    TestBed.configureTestingModule({ providers: [
      provideHttpClient(withInterceptors([jwtInterceptor])), provideHttpClientTesting(),
      { provide: Router, useValue: router }
    ] });
    http = TestBed.inject(HttpClient);
    mock = TestBed.inject(HttpTestingController);
    auth = TestBed.inject(AuthService);
  });
  afterEach(() => { mock.verify(); localStorage.clear(); });
  it('shares one renewal for concurrent expired-token requests', () => {
    localStorage.setItem('oms_token', token(-60));
    http.get(`${environment.apiUrl}/orders`).subscribe();
    http.get(`${environment.apiUrl}/profile/me`).subscribe();
    const refresh = mock.expectOne(`${environment.apiUrl}/auth/refresh`);
    expect(refresh.request.withCredentials).toBeTrue();
    const renewed = response();
    refresh.flush(renewed);
    for (const path of ['orders', 'profile/me']) {
      const request = mock.expectOne(`${environment.apiUrl}/${path}`);
      expect(request.request.headers.get('Authorization')).toBe(`Bearer ${renewed.token}`);
      request.flush({});
    }
  });
  it('retries a server-rejected token only once and clears an invalid session', () => {
    localStorage.setItem('oms_token', token(600));
    http.get(`${environment.apiUrl}/orders`).subscribe({ error: () => {} });
    mock.expectOne(`${environment.apiUrl}/orders`).flush({}, { status: 401, statusText: 'Unauthorized' });
    mock.expectOne(`${environment.apiUrl}/auth/refresh`).flush(response());
    mock.expectOne(`${environment.apiUrl}/orders`).flush({}, { status: 401, statusText: 'Unauthorized' });
    expect(auth.getToken()).toBeNull();
    expect(router.navigate).toHaveBeenCalled();
  });
  it('redirects when the refresh cookie expires without sending the protected request', () => {
    localStorage.setItem('oms_token', token(-60));
    http.get(`${environment.apiUrl}/orders`).subscribe({ error: () => {} });
    mock.expectOne(`${environment.apiUrl}/auth/refresh`).flush({}, { status: 401, statusText: 'Unauthorized' });
    mock.expectNone(`${environment.apiUrl}/orders`);
    expect(auth.getToken()).toBeNull();
    expect(router.navigate).toHaveBeenCalled();
  });
  it('does not clear the session on a temporary refresh failure', () => {
    const expired = token(-60);
    localStorage.setItem('oms_token', expired);
    auth.getValidToken().subscribe({ error: () => {} });
    mock.expectOne(`${environment.apiUrl}/auth/refresh`).flush({}, { status: 503, statusText: 'Unavailable' });
    expect(auth.getToken()).toBe(expired);
    expect(router.navigate).not.toHaveBeenCalled();
  });
  it('does not send credentials to unrelated URLs', () => {
    localStorage.setItem('oms_token', token(600));
    http.get('https://example.test/api').subscribe();
    const request = mock.expectOne('https://example.test/api');
    expect(request.request.headers.has('Authorization')).toBeFalse();
    request.flush({});
  });
});
