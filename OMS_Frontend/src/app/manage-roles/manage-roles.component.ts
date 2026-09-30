import { Component, OnInit } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { CommonModule } from '@angular/common';
import { FooterComponent } from '../footer/footer.component';
import { FormsModule } from '@angular/forms';
import { RolesService, RoleOption } from '../admin/roles.service';
import { environment } from '../../environments/environment';

interface ScreenPermission {
  screenKey: string;
  canView: boolean;
  canAdd: boolean;
  canEdit: boolean;
  canDelete: boolean;
  adminAssignedOnly: boolean;
}

@Component({
  selector: 'app-manage-roles',
  standalone: true,
  imports: [CommonModule, FooterComponent, FormsModule],
  templateUrl: './manage-roles.component.html'
})
export class ManageRolesComponent implements OnInit {
  roleOptions: RoleOption[] = [];
  rolesLoading = false;
  rolesError = '';
  permissionsError = '';
  assignmentPermissionError = '';

  selectedRoleId: number | null = null;
  showRoleMenu = false;
  permissions: ScreenPermission[] = [];
  loading = false;
  saving = false;
  saved = false;

  // ---------- Manage Roles modal ----------
  rolesModalOpen = false;

  // Add role
  newRoleName = '';
  addRoleSaving = false;
  addRoleError = '';

  // Inline rename
  editingRoleId: number | null = null;
  editRoleName = '';
  editRoleError = '';
  savingRoleId: number | null = null;

  // Delete
  roleToDelete: RoleOption | null = null;
  deletingRole = false;
  deleteRoleError = '';

  private apiUrl = `${environment.apiUrl}/rolepermissions`;

  constructor(private http: HttpClient, private rolesService: RolesService) {}

  ngOnInit(): void {
    this.loadRoles();
  }

  // =========================================================
  // Roles list
  // =========================================================
  loadRoles(): void {
    this.rolesLoading = true;
    this.rolesError = '';
    this.rolesService.getRoles().subscribe({
      next: data => {
        this.roleOptions = data.filter(r => r.name !== 'Super Admin');
        if (!this.roleOptions.some(r => r.id === this.selectedRoleId))
          this.selectedRoleId = this.roleOptions[0]?.id ?? null;
        this.rolesLoading = false;
        this.fetchPermissions();
      },
      error: () => {
        this.roleOptions = [];
        this.permissions = [];
        this.rolesLoading = false;
        this.rolesError = 'Unable to load roles. Please retry.';
      }
    });
  }

  get selectedRoleInactive(): boolean {
    return this.roleOptions.some(r => r.id === this.selectedRoleId && !r.isActive);
  }

  activateRole(): void {
    if (!this.selectedRoleId || this.saving) return;
    this.saving = true;
    this.rolesService.activate(this.selectedRoleId).subscribe({
      next: () => { this.saving = false; this.loadRoles(); },
      error: () => { this.saving = false; this.rolesError = 'Unable to activate role.'; }
    });
  }

  get selectedRoleName(): string {
    return this.roleOptions.find(r => r.id === this.selectedRoleId)?.name || 'Select role';
  }

  toggleRoleMenu(): void {
    this.showRoleMenu = !this.showRoleMenu;
  }

  selectRole(id: number): void {
    if (this.loading || this.saving || this.rolesLoading) return;
    this.selectedRoleId = id;
    this.showRoleMenu = false;
    this.fetchPermissions();
  }

  // =========================================================
  // Permissions
  // =========================================================
  fetchPermissions(): void {
    this.permissions = [];
    this.permissionsError = '';
    if (!this.selectedRoleId) return;
    this.loading = true;
    this.saved = false;
    this.http.get<any[]>(`${this.apiUrl}/${this.selectedRoleId}`).subscribe({
      next: (data) => {
        this.permissions = data.map(d => ({
          screenKey: d.screenKey,
          canView: d.canView,
          canAdd: d.canAdd,
          canEdit: d.canEdit,
          canDelete: d.canDelete,
          adminAssignedOnly: d.adminAssignedOnly === true
        }));
        this.loading = false;
      },
      error: () => { this.loading = false; this.permissionsError = 'Unable to load permissions. Please retry.'; }
    });
  }

  toggleAllForRow(row: ScreenPermission, checked: boolean): void {
    if (row.adminAssignedOnly) return;
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

  toggleAdminAssigned(row: ScreenPermission, checked: boolean): void {
    if (row.screenKey !== 'Orders' || this.selectedRoleName === 'Customer' || !this.selectedRoleId || this.saving || this.loading) return;
    const previous = { ...row };
    row.adminAssignedOnly = checked;
    if (checked) row.canView = row.canAdd = row.canEdit = row.canDelete = false;
    this.saving = true;
    this.assignmentPermissionError = '';
    // Save only this row; other unsaved permission edits stay in the matrix.
    this.http.put(this.apiUrl, { roleId: this.selectedRoleId, permissions: [{ ...row }] }).subscribe({
      next: () => { this.saving = false; },
      error: () => {
        Object.assign(row, previous);
        this.saving = false;
        this.assignmentPermissionError = 'Assignment access was not saved. Please retry.';
      }
    });
  }

  isChatScreen(key: string): boolean {
    return key === 'Order Customer Chat' || key === 'Order Group Chat';
  }

  saveChanges(): void {
    if (!this.selectedRoleId || this.loading || this.rolesLoading || this.permissionsError || this.rolesError) return;
    this.saving = true;
    this.saved = false;
    const payload = {
      roleId: this.selectedRoleId,
      permissions: this.permissions.map(p => ({
        screenKey: p.screenKey,
        canView: p.canView,
        canAdd: p.canAdd,
        canEdit: p.canEdit,
        canDelete: p.canDelete,
        adminAssignedOnly: p.adminAssignedOnly
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
  // Manage Roles modal
  // =========================================================
  openRolesModal(): void {
    this.rolesModalOpen = true;
    this.newRoleName = '';
    this.addRoleError = '';
    this.cancelEditRole();
    this.loadRoles();
  }

  closeRolesModal(): void {
    if (this.addRoleSaving || this.savingRoleId !== null || this.deletingRole) return;
    this.rolesModalOpen = false;
    this.cancelEditRole();
  }

  createRole(): void {
    const name = this.newRoleName.trim();
    if (!name) {
      this.addRoleError = 'Role name is required.';
      return;
    }

    this.addRoleSaving = true;
    this.addRoleError = '';

    this.rolesService.create(name).subscribe({
      next: (created) => {
        this.addRoleSaving = false;
        this.newRoleName = '';

        if (!this.roleOptions.some(r => r.id === created.id)) {
          this.roleOptions = [...this.roleOptions, created]
            .sort((a, b) => a.name.localeCompare(b.name));
        }

        this.selectedRoleId = created.id;
        this.loadRoles();
      },
      error: (err) => {
        this.addRoleSaving = false;
        this.addRoleError =
          err?.error?.message ??
          (err.status === 409 ? 'This role already exists.' : 'Failed to create role. Please try again.');
      }
    });
  }

  // ---------- Inline rename ----------
  startEditRole(role: RoleOption): void {
    this.editingRoleId = role.id;
    this.editRoleName = role.name;
    this.editRoleError = '';
  }

  cancelEditRole(): void {
    this.editingRoleId = null;
    this.editRoleName = '';
    this.editRoleError = '';
  }

  saveRoleName(role: RoleOption): void {
    const name = this.editRoleName.trim();
    if (!name) {
      this.editRoleError = 'Role name is required.';
      return;
    }
    if (name === role.name) {
      this.cancelEditRole();
      return;
    }

    this.savingRoleId = role.id;
    this.editRoleError = '';

    this.rolesService.rename(role.id, name).subscribe({
      next: () => {
        this.savingRoleId = null;
        this.cancelEditRole();
        this.loadRoles();
      },
      error: (err) => {
        this.savingRoleId = null;
        this.editRoleError =
          err?.error?.message ??
          (err.status === 409 ? 'This role name already exists.' : 'Failed to rename role.');
      }
    });
  }

  // ---------- Delete ----------
  openDeleteRole(role: RoleOption): void {
    if (!role) return;
    if (this.saving || this.loading || this.rolesLoading) return;

    this.roleToDelete = role;
    this.deleteRoleError = '';
    this.showRoleMenu = false;
  }

  closeDeleteRole(): void {
    if (!this.deletingRole) this.roleToDelete = null;
  }

  confirmDeleteRole(): void {
    if (!this.roleToDelete || this.deletingRole) return;
    const id = this.roleToDelete.id;
    this.deletingRole = true;
    this.deleteRoleError = '';
    this.rolesService.delete(id).subscribe({
      next: () => {
        this.deletingRole = false;
        this.roleToDelete = null;
        this.roleOptions = this.roleOptions.filter(r => r.id !== id);
        this.selectedRoleId = null;
        this.permissions = [];
        this.saved = false;
        this.loadRoles();
      },
      error: err => {
        this.deletingRole = false;
        this.deleteRoleError = err.error?.message ?? (err.status === 404
          ? 'This role no longer exists. Close this dialog and refresh the page.'
          : 'Unable to delete role. Please try again.');
      }
    });
  }
}
