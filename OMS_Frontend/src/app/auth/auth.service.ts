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

  isAuthenticated = signal<boolean>(
    !!this.getToken()
  );

  currentRole = signal<string | null>(
    this.getCurrentUser()?.role ?? null
  );

  currentUser = signal<AuthResponse | null>(
    this.getCurrentUser()
  );

  constructor(private http: HttpClient) {}

  // ============================================================
  // REGISTER
  // ============================================================

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

  // ============================================================
  // LOGIN
  // ============================================================

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

  // ============================================================
  // FORGOT PASSWORD
  // ============================================================

  forgotPassword(
    email: string
  ): Observable<{ message: string }> {

    return this.http.post<{ message: string }>(
      `${this.apiUrl}/forgot-password`,
      {
        email
      }
    );
  }

  // ============================================================
  // RESET PASSWORD
  // ============================================================

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

  // ============================================================
  // CHANGE PASSWORD
  // ============================================================

  changePassword(payload: {
    oldPassword: string;
    newPassword: string;
  }): Observable<{ message: string }> {

    return this.http.post<{ message: string }>(
      `${this.apiUrl}/change-password`,
      payload
    );
  }

  // ============================================================
  // GET PROFILE
  // ============================================================

  getProfile(): Observable<UserProfile> {

    return this.http.get<UserProfile>(
      `${environment.apiUrl}/profile/me`
    );
  }

  // ============================================================
  // UPDATE PROFILE
  // ============================================================

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

  // ============================================================
  // LOGOUT
  // ============================================================

  logout(): void {

    localStorage.removeItem(this.tokenKey);
    localStorage.removeItem(this.userKey);

    this.isAuthenticated.set(false);
    this.currentRole.set(null);
    this.currentUser.set(null);
  }

  // ============================================================
  // GET TOKEN
  // ============================================================

  getToken(): string | null {

    return localStorage.getItem(this.tokenKey);
  }

  // ============================================================
  // GET CURRENT USER
  // ============================================================

  getCurrentUser(): AuthResponse | null {

    const raw = localStorage.getItem(this.userKey);

    return raw
      ? JSON.parse(raw)
      : null;
  }

  // ============================================================
  // UPDATE LOCAL USER (after profile edit)
  // ============================================================

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

  // ============================================================
  // CHECK SUPER ADMIN
  // ============================================================

  isSuperAdmin(): boolean {

    return this.getCurrentUser()?.role === 'Super Admin';
  }

  // ============================================================
  // SAVE AUTH SESSION
  // ============================================================

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
  }
}