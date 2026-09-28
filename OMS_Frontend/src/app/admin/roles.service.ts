import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { map } from 'rxjs';
import { environment } from '../../environments/environment';

export interface RoleOption { id: number; name: string; isActive: boolean; canDelete?: boolean; }

@Injectable({ providedIn: 'root' })
export class RolesService {
  private readonly url = `${environment.apiUrl}/roles`;
  constructor(private http: HttpClient) {}

  // Fetch on entry and after mutations; do not retain a stale role catalog.
  getRoles() { return this.http.get<RoleOption[]>(this.url); }
  getAssignableRoles() {
    return this.getRoles().pipe(map(roles => roles.filter(r => r.isActive && r.name !== 'Super Admin')));
  }
  create(name: string) { return this.http.post<RoleOption>(this.url, { name }); }
  activate(id: number) { return this.http.patch(`${this.url}/${id}/activate`, {}); }
  delete(id: number) { return this.http.delete(`${this.url}/${id}`); }
}
