import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';

export interface RoleOption {
  id: number;
  name: string;
}

@Injectable({ providedIn: 'root' })
export class AdminService {
  private readonly apiUrl = `${environment.apiUrl}/admin`;

  constructor(private http: HttpClient) {}

  getAssignableRoles(): Observable<RoleOption[]> {
    return this.http.get<RoleOption[]>(`${this.apiUrl}/roles`);
  }
}