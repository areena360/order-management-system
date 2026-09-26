import { Injectable, signal } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Router } from '@angular/router';
import { Observable, Subject, tap, map, of, catchError, finalize, shareReplay, throwError } from 'rxjs';
import { environment } from '../../environments/environment';

export interface AuthResponse {
  token: string;
  expiresAt: string;
  firstName: string;
  lastName: string;
  email: string;
  role: string;
  isActive: boolean;
}

export interface UserProfile {
  id: number;
  firstName: string;
  lastName: string;
  email: string;
  firstContact: string;
  secondContact?: string;
  homeAddress?: string;
  officeAddress?: string;
  websiteUrl?: string;
  role: string;
  isActive: boolean;
  createdDate: string;
  updatedDate?: string;
}

@Injectable({ providedIn: 'root' })
export class AuthService {

  private readonly apiUrl = `${environment.apiUrl}/auth`;
  private readonly usersUrl = `${environment.apiUrl}/users`;

  private readonly tokenKey = 'oms_token';
  private readonly userKey = 'oms_user';
  private readonly sessionChangedSubject = new Subject<boolean>();
  readonly sessionChanged$ = this.sessionChangedSubject.asObservable();

  isAuthenticated = signal<boolean>(
    !!this.getToken()
  );

  currentRole = signal<string | null>(
    this.getCurrentUser()?.role ?? null
  );

  currentUser = signal<AuthResponse | null>(
    this.getCurrentUser()
  );

  private refreshRequest?: Observable<AuthResponse>;
  private sessionVersion = 0;

  constructor(private http: HttpClient, private router: Router) { }

  getValidToken(): Observable<string | null> {
    const token = this.getToken();
    if (!token) return of(null);
    try {
      const payload = JSON.parse(atob(token.split('.')[1].replace(/-/g, '+').replace(/_/g, '/')));
      if (payload.exp * 1000 > Date.now() + 30000) return of(token);
    } catch { /* Renew malformed or expired cached tokens through the session cookie. */ }
    return this.refreshSession().pipe(map(res => res.token));
  }

  refreshSession(): Observable<AuthResponse> {
    if (!this.refreshRequest) {
      const version = this.sessionVersion;
      this.refreshRequest = this.http.post<AuthResponse>(`${this.apiUrl}/refresh`, {}, { withCredentials: true }).pipe(
        tap(res => {
          if (version !== this.sessionVersion) throw new HttpErrorResponse({ status: 401 });
          this.persistSession(res);
        }),
        catchError(err => {
          if (err.status === 401 && version === this.sessionVersion) this.expireSession();
          return throwError(() => err);
        }),
        finalize(() => this.refreshRequest = undefined),
        shareReplay({ bufferSize: 1, refCount: false })
      );
    }
    return this.refreshRequest;
  }

  expireSession(): void {
    const returnUrl = this.router.url;
    this.clearSession();
    if (!returnUrl.startsWith('/login'))
      void this.router.navigate(['/login'], { queryParams: { returnUrl, sessionExpired: true } });
  }

  register(payload: {
    firstName: string;
    lastName: string;
    email: string;
    firstContact: string;
    secondContact?: string;
    homeAddress?: string;
    officeAddress?: string;
    websiteUrl?: string;
    password: string;
    confirmPassword: string;
  }): Observable<AuthResponse> {

    return this.http
      .post<AuthResponse>(
        `${this.apiUrl}/register`,
        payload
      )
      .pipe(
        tap((res) => this.persistSession(res))
      );
  }

  login(
    email: string,
    password: string,
    rememberMe: boolean
  ): Observable<AuthResponse> {

    return this.http
      .post<AuthResponse>(
        `${this.apiUrl}/login`,
        {
          email,
          password,
          rememberMe
        }
      )
      .pipe(
        tap((res) => this.persistSession(res))
      );
  }

  forgotPassword(
    email: string
  ): Observable<{ message: string }> {

    return this.http.post<{ message: string }>(
      `${this.apiUrl}/forgot-password`,
      { email }
    );
  }

  resetPassword(payload: {
    token: string;
    email: string;
    newPassword: string;
    confirmPassword: string;
  }): Observable<{ message: string }> {

    return this.http.post<{ message: string }>(
      `${this.apiUrl}/reset-password`,
      payload
    );
  }

  changePassword(payload: {
    oldPassword: string;
    newPassword: string;
  }): Observable<{ message: string }> {

    return this.http.post<{ message: string }>(
      `${this.apiUrl}/change-password`,
      payload
    );
  }

  getProfile(): Observable<UserProfile> {

    return this.http.get<UserProfile>(
      `${environment.apiUrl}/profile/me`
    );
  }

  updateProfile(payload: {
    firstName: string;
    lastName: string;
    websiteUrl?: string;
    firstContact: string;
    secondContact?: string;
    homeAddress?: string;
    officeAddress?: string;
  }): Observable<UserProfile> {

    return this.http.put<UserProfile>(
      `${environment.apiUrl}/profile/me`,
      payload
    );
  }

  logout(): void {
    this.http.post(`${this.apiUrl}/logout`, {}, { withCredentials: true }).subscribe({ error: () => {} });
    this.clearSession();
  }

  private clearSession(): void {
    this.sessionVersion++;

    localStorage.removeItem(this.tokenKey);
    localStorage.removeItem(this.userKey);

    this.isAuthenticated.set(false);
    this.currentRole.set(null);
    this.currentUser.set(null);
    this.sessionChangedSubject.next(false);
  }

  getToken(): string | null {

    return localStorage.getItem(this.tokenKey);
  }

  getCurrentUser(): AuthResponse | null {

    const raw = localStorage.getItem(this.userKey);

    try { return raw ? JSON.parse(raw) : null; }
    catch { return null; }
  }

  updateLocalUser(patch: Partial<AuthResponse>): void {

    const current = this.getCurrentUser();
    if (!current) return;

    const updated = { ...current, ...patch };

    localStorage.setItem(
      this.userKey,
      JSON.stringify(updated)
    );

    this.currentUser.set(updated);
  }

  isSuperAdmin(): boolean {

    return this.getCurrentUser()?.role === 'Super Admin';
  }

  isCustomer(): boolean {
    return this.currentRole() === 'Customer';
  }

  private persistSession(res: AuthResponse): void {

    localStorage.setItem(
      this.tokenKey,
      res.token
    );

    localStorage.setItem(
      this.userKey,
      JSON.stringify(res)
    );

    this.isAuthenticated.set(true);
    this.currentRole.set(res.role);
    this.currentUser.set(res);
    this.sessionChangedSubject.next(true);
  }
}
