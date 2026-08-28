import { Component, OnInit } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { CommonModule } from '@angular/common';
import { FooterComponent } from "../footer/footer.component";
import { FormsModule } from '@angular/forms';

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

  private apiUrl = 'https://localhost:44370/api/rolepermissions';

  constructor(private http: HttpClient) {}

  ngOnInit(): void {
    this.fetchPermissions();
  }

  get selectedRoleName(): string {
    return this.roleOptions.find(r => r.id === this.selectedRoleId)?.name || 'Select role';
  }

  toggleRoleMenu(): void {
    this.showRoleMenu = !this.showRoleMenu;
  }

  selectRole(id: number): void {
    this.selectedRoleId = id;
    this.showRoleMenu = false;
    this.fetchPermissions();
  }

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
    if (!checked) { row.canAdd = false; row.canEdit = false; row.canDelete = false; }
  }

  isNoActionScreen(screenKey: string): boolean {
  return screenKey === 'Dashboard' || screenKey === 'Manage Roles';
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
}