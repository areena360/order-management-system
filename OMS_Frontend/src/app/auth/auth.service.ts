import { Injectable, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, tap } from 'rxjs';
import { environment } from '../../environments/environment';

export interface AuthResponse {
  token: string;
  expiresAt: string;
  firstName: string;
  lastName: string;
  email: string;
  role: string;
}

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly apiUrl = `${environment.apiUrl}/auth`;
  private readonly tokenKey = 'oms_token';
  private readonly userKey = 'oms_user';

  isAuthenticated = signal<boolean>(!!this.getToken());
  currentRole = signal<string | null>(this.getCurrentUser()?.role ?? null);

  constructor(private http: HttpClient) {}

  register(payload: {
  firstName: string;
  lastName: string;
  email: string;
  firstContact: string;
  secondContact?: string;
  homeAddress?: string;
  officeAddress?: string;
  password: string;
  confirmPassword: string;
}): Observable<{ message: string }> {
  return this.http.post<{ message: string }>(`${this.apiUrl}/register`, payload);
  // no persistSession() — user isn't logged in yet
}

  login(email: string, password: string, rememberMe: boolean): Observable<AuthResponse> {
    return this.http
      .post<AuthResponse>(`${this.apiUrl}/login`, { email, password, rememberMe })
      .pipe(tap((res) => this.persistSession(res)));
  }

  forgotPassword(email: string): Observable<{ message: string }> {
    return this.http.post<{ message: string }>(`${this.apiUrl}/forgot-password`, { email });
  }

  resetPassword(payload: {
    token: string;
    email: string;
    newPassword: string;
    confirmPassword: string;
  }): Observable<{ message: string }> {
    return this.http.post<{ message: string }>(`${this.apiUrl}/reset-password`, payload);
  }

  logout(): void {
    localStorage.removeItem(this.tokenKey);
    localStorage.removeItem(this.userKey);
    this.isAuthenticated.set(false);
    this.currentRole.set(null);
  }

  getToken(): string | null {
    return localStorage.getItem(this.tokenKey);
  }

  getCurrentUser(): AuthResponse | null {
    const raw = localStorage.getItem(this.userKey);
    return raw ? JSON.parse(raw) : null;
  }

  isSuperAdmin(): boolean {
    return this.getCurrentUser()?.role === 'Super Admin';
  }

  private persistSession(res: AuthResponse): void {
    localStorage.setItem(this.tokenKey, res.token);
    localStorage.setItem(this.userKey, JSON.stringify(res));
    this.isAuthenticated.set(true);
    this.currentRole.set(res.role);
  }
}