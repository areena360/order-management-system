import { Component, OnInit } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { CommonModule } from '@angular/common';
import { FooterComponent } from '../footer/footer.component';
import { FormsModule } from '@angular/forms';
import { environment } from '../../environments/environment';

interface ScreenPermission {
  screenKey: string;
  canView: boolean;
  canAdd: boolean;
  canEdit: boolean;
  canDelete: boolean;
}

@Component({
  selector: 'app-manage-roles',
  standalone: true,
  imports: [CommonModule, FooterComponent, FormsModule],
  templateUrl: './manage-roles.component.html'
})
export class ManageRolesComponent implements OnInit {
  roleOptions: { id: number; name: string }[] = [
    { id: 2, name: 'Admin' },
    { id: 3, name: 'Finance' },
    { id: 4, name: 'Customer' },
    { id: 5, name: 'Staff' },
    { id: 6, name: 'Sales' }
  ];

  selectedRoleId = 2;
  showRoleMenu = false;
  permissions: ScreenPermission[] = [];
  loading = false;
  saving = false;
  saved = false;

  // ---------- Add Role modal ----------
  addRoleOpen = false;
  newRoleName = '';
  addRoleSaving = false;
  addRoleError = '';

  private apiUrl = `${environment.apiUrl}/rolepermissions`;
  private rolesApiUrl = `${environment.apiUrl}/roles`;

  constructor(private http: HttpClient) {}

  ngOnInit(): void {
    this.loadRoles();
    this.fetchPermissions();
  }

  // =========================================================
  // Roles list
  // =========================================================
  loadRoles(): void {
    this.http.get<{ id: number; name: string }[]>(this.rolesApiUrl).subscribe({
      next: (data) => {
        if (Array.isArray(data) && data.length) {
          this.roleOptions = data;
        }
      },
      // Agar backend abhi nahi bana, fallback silently rahega
      error: () => {}
    });
  }

  get selectedRoleName(): string {
    return this.roleOptions.find(r => r.id === this.selectedRoleId)?.name || 'Select role';
  }

  toggleRoleMenu(): void {
    this.showRoleMenu = !this.showRoleMenu;
  }

  selectRole(id: number): void {
    if (this.loading || this.saving) return;
    this.selectedRoleId = id;
    this.showRoleMenu = false;
    this.fetchPermissions();
  }

  // =========================================================
  // Permissions
  // =========================================================
  fetchPermissions(): void {
    this.loading = true;
    this.saved = false;
    this.http.get<any[]>(`${this.apiUrl}/${this.selectedRoleId}`).subscribe({
      next: (data) => {
        this.permissions = data.map(d => ({
          screenKey: d.screenKey,
          canView: d.canView,
          canAdd: d.canAdd,
          canEdit: d.canEdit,
          canDelete: d.canDelete
        }));
        this.loading = false;
      },
      error: () => this.loading = false
    });
  }

  toggleAllForRow(row: ScreenPermission, checked: boolean): void {
    row.canView = checked;
    if (!checked) {
      row.canAdd = false;
      row.canEdit = false;
      row.canDelete = false;
    }
  }

  isNoActionScreen(screenKey: string): boolean {
    return screenKey === 'Dashboard' || screenKey === 'Manage Roles';
  }

  isChatScreen(key: string): boolean {
    return key === 'Order Customer Chat' || key === 'Order Group Chat';
  }

  saveChanges(): void {
    this.saving = true;
    this.saved = false;
    const payload = {
      roleId: this.selectedRoleId,
      permissions: this.permissions.map(p => ({
        screenKey: p.screenKey,
        canView: p.canView,
        canAdd: p.canAdd,
        canEdit: p.canEdit,
        canDelete: p.canDelete
      }))
    };
    this.http.put(this.apiUrl, payload).subscribe({
      next: () => {
        this.saving = false;
        this.saved = true;
        setTimeout(() => this.saved = false, 2500);
      },
      error: () => {
        this.saving = false;
        alert('Failed to save permissions.');
      }
    });
  }

  // =========================================================
  // Add Role modal
  // =========================================================
  openAddRole(): void {
    this.addRoleOpen = true;
    this.newRoleName = '';
    this.addRoleError = '';
  }

  closeAddRole(): void {
    if (this.addRoleSaving) return;
    this.addRoleOpen = false;
  }

  createRole(): void {
    const name = this.newRoleName.trim();
    if (!name) {
      this.addRoleError = 'Role name is required.';
      return;
    }

    this.addRoleSaving = true;
    this.addRoleError = '';

    this.http.post<{ id: number; name: string }>(this.rolesApiUrl, { name }).subscribe({
      next: (created) => {
        this.addRoleSaving = false;
        this.addRoleOpen = false;

        // Naya role dropdown me add karo
        if (!this.roleOptions.some(r => r.id === created.id)) {
          this.roleOptions = [...this.roleOptions, created]
            .sort((a, b) => a.name.localeCompare(b.name));
        }

        // Naya role select karo aur uska permission table load karo
        this.selectedRoleId = created.id;
        this.fetchPermissions();
      },
      error: (err) => {
        this.addRoleSaving = false;
        this.addRoleError =
          err?.error?.message ??
          (err.status === 409 ? 'This role already exists.' : 'Failed to create role. Please try again.');
      }
    });
  }
}