import { Injectable } from '@angular/core';
import { RolesService, RoleOption } from './roles.service';
import { Observable } from 'rxjs';
export type { RoleOption } from './roles.service';

@Injectable({ providedIn: 'root' })
export class AdminService {

  constructor(private roles: RolesService) {}

  getAssignableRoles(): Observable<RoleOption[]> {
    return this.roles.getAssignableRoles();
  }
}
