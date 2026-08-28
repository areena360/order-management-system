import { Component, OnInit, HostListener } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { CommonModule } from '@angular/common';
import { FormsModule, NgForm } from '@angular/forms';
<<<<<<< Updated upstream
=======
import { FooterComponent } from "../footer/footer.component";
import { AuthService } from '../auth/auth.service';
import { PermissionService } from '../auth/permission.service';
>>>>>>> Stashed changes

export interface AppUser {
  id: number;
  firstName: string;
  lastName: string;
  firstContact: string;
  secondContact: string;
  email: string;
  homeAddress: string;
  officeAddress: string;
  roleId: number;
  role: string;         // display name, derived from roleId
  isActive: boolean;
  isDeleted: boolean;
  createdDate: string;
  createdBy: string;
  updatedDate: string;
  updatedBy: string;
}

export interface UserForm {
  id?: number;
  firstName: string;
  lastName: string;
  email: string;
  firstContact: string;
  secondContact: string;
  homeAddress: string;
  officeAddress: string;
  roleId: number;
  isActive?: boolean;
  password?: string;
}

// A column that can be hidden. key must match the *ngIf flag used in the template.
interface ColumnOption {
  key: string;
  label: string;
}

@Component({
  selector: 'app-manage-users',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './manage-users.component.html'
})
export class ManageUsersComponent implements OnInit {
  users: AppUser[] = [];
  filteredUsers: AppUser[] = [];
  loading = true;
  errorMsg = '';

  searchTerm = '';
  roleFilter = 'All';
  statusFilter: 'active' | 'deleted' | 'all' = 'active';
  roles: string[] = ['All'];
  roleOptions: { id: number; name: string }[] = [
    { id: 2, name: 'Admin' },
    { id: 3, name: 'Finance' },
    { id: 4, name: 'Customer' },
    { id: 5, name: 'Staff' },
    { id: 6, name: 'Sales' }
  ];

  currentPage = 1;
  pageSize = 8;

  // ---- Column visibility ----
  // Full Name, Role, Actions are always shown (core columns) and are not toggleable.
  // Everything else can be hidden to keep the table from getting too wide.
  columnOptions: ColumnOption[] = [
    { key: 'firstContact', label: 'First Contact' },
    { key: 'secondContact', label: 'Second Contact' },
    { key: 'email', label: 'Email' },
    { key: 'homeAddress', label: 'Home Address' },
    { key: 'officeAddress', label: 'Office Address' },
    { key: 'createdDate', label: 'Created Date' },
    { key: 'createdBy', label: 'Created By' },
    { key: 'updatedDate', label: 'Updated Date' },
    { key: 'updatedBy', label: 'Updated By' },
  ];

  // Sensible default: keep the essentials visible, hide the audit-trail noise.
  visibleColumns: Record<string, boolean> = {
    firstContact: true,
    secondContact: false,
    email: true,
    homeAddress: false,
    officeAddress: false,
    createdDate: false,
    createdBy: false,
    updatedDate: false,
    updatedBy: false,
  };

  showColumnMenu = false;
  showRoleMenu = false;

  toggleRoleMenu(): void {
    this.showRoleMenu = !this.showRoleMenu;
  }

  selectRole(r: string): void {
    this.roleFilter = r;
    this.showRoleMenu = false;
    this.applyFilters();
  }

  showFormRoleMenu = false;
  showAddPassword = false;

  toggleFormRoleMenu(): void {
    this.showFormRoleMenu = !this.showFormRoleMenu;
  }

  toggleAddPassword(): void {
  this.showAddPassword = !this.showAddPassword;
}

  selectFormRole(id: number): void {
    this.form.roleId = id;
    this.showFormRoleMenu = false;
  }

  formRoleName(): string {
    return this.roleOptions.find((r) => r.id === this.form.roleId)?.name || 'Select role';
  }

  toggleColumn(key: string): void {
    this.visibleColumns[key] = !this.visibleColumns[key];
  }

  isColumnVisible(key: string): boolean {
    return !!this.visibleColumns[key];
  }

  toggleColumnMenu(): void {
    this.showColumnMenu = !this.showColumnMenu;
  }

  resetColumns(): void {
    this.columnOptions.forEach((c) => (this.visibleColumns[c.key] = true));
  }

  hideAllOptionalColumns(): void {
    this.columnOptions.forEach((c) => (this.visibleColumns[c.key] = false));
  }

  visibleColumnCount(): number {
    return Object.values(this.visibleColumns).filter(Boolean).length;
  }

  // Close the column menu when clicking outside of it
  @HostListener('document:click', ['$event'])
  onDocumentClick(event: MouseEvent): void {
    const target = event.target as HTMLElement;
    if (!target.closest('[data-column-menu]')) this.showColumnMenu = false;
    if (!target.closest('[data-role-menu]')) this.showRoleMenu = false;
    if (!target.closest('[data-form-role-menu]') && !target.closest('[data-form-role-menu-edit]')) this.showFormRoleMenu = false;
  }

  // modal state
  showAddModal = false;
  showEditModal = false;
  showDeleteModal = false;
  showToggleActiveModal = false;
  selectedUser: AppUser | null = null;
  form: UserForm = this.emptyForm();
  saving = false;

  private apiUrl = 'https://localhost:44370/api/users';

<<<<<<< Updated upstream
  constructor(private http: HttpClient) {}
=======
  constructor(private http: HttpClient, public auth: AuthService,  public perm: PermissionService) {}
>>>>>>> Stashed changes

  ngOnInit(): void {
    this.fetchUsers();
  }

   get canAdd(): boolean {
    return this.auth.isSuperAdmin() || this.perm.canAdd('Manage Users');
  }

  get canEdit(): boolean {
    return this.auth.isSuperAdmin() || this.perm.canEdit('Manage Users');
  }

  get canDelete(): boolean {
    return this.auth.isSuperAdmin() || this.perm.canDelete('Manage Users');
  }

  emptyForm(): UserForm {
    return {
      firstName: '',
      lastName: '',
      email: '',
      firstContact: '',
      secondContact: '',
      homeAddress: '',
      officeAddress: '',
      roleId: 4,
      isActive: true,
      password: ''
    };
  }

  fetchUsers(): void {
    this.loading = true;
    this.errorMsg = '';
    this.http.get<AppUser[]>(this.apiUrl).subscribe({
      next: (data) => {
        this.users = data;
        this.roles = ['All', ...Array.from(new Set(data.map(u => u.role)))];
        this.applyFilters();
        this.loading = false;
      },
      error: () => {
        this.errorMsg = 'Failed to load users. Please try again.';
        this.loading = false;
      }
    });
  }

  applyFilters(): void {
    let list = [...this.users];

    if (this.statusFilter === 'active') list = list.filter(u => !u.isDeleted);
    else if (this.statusFilter === 'deleted') list = list.filter(u => u.isDeleted);

    if (this.roleFilter !== 'All') list = list.filter(u => u.role === this.roleFilter);

    if (this.searchTerm.trim()) {
      const term = this.searchTerm.toLowerCase();
      list = list.filter(u =>
        u.firstName.toLowerCase().includes(term) ||
        u.lastName.toLowerCase().includes(term) ||
        u.email.toLowerCase().includes(term)
      );
    }

    this.filteredUsers = list;
    this.currentPage = 1;
  }

  get paginatedUsers(): AppUser[] {
    const start = (this.currentPage - 1) * this.pageSize;
    return this.filteredUsers.slice(start, start + this.pageSize);
  }

  get totalPages(): number {
    return Math.max(1, Math.ceil(this.filteredUsers.length / this.pageSize));
  }

  goToPage(page: number): void {
    if (page >= 1 && page <= this.totalPages) this.currentPage = page;
  }

  roleBadgeClass(role: string): string {
    const map: Record<string, string> = {
      Admin: 'bg-blue-100 text-blue-700 ring-blue-600/20',
      Finance: 'bg-emerald-100 text-emerald-700 ring-emerald-600/20',
      Customer: 'bg-amber-100 text-amber-700 ring-amber-600/20',
      Staff: 'bg-cyan-100 text-cyan-700 ring-cyan-600/20',
      Sales: 'bg-pink-100 text-pink-700 ring-pink-600/20'
    };
    return map[role] || 'bg-gray-100 text-gray-700 ring-gray-600/20';
  }

  roleName(roleId: number): string {
    return this.roleOptions.find(r => r.id === roleId)?.name || 'Unknown';
  }

  fullName(user: AppUser): string {
  return `${user.firstName} ${user.lastName}`.trim();
}

  // ---- Add ----
  openAddModal(): void {
    this.form = this.emptyForm();
    this.showAddModal = true;
  }

  submitAdd(addForm?: NgForm): void {
    if (addForm && addForm.invalid) {
      Object.values(addForm.controls).forEach(c => c.markAsTouched());
      return;
    }
    this.saving = true;
    this.http.post(this.apiUrl, this.form).subscribe({
      next: () => {
        this.saving = false;
        this.showAddModal = false;
        this.fetchUsers();
      },
      error: () => {
        this.saving = false;
        alert('Failed to add user.');
      }
    });
  }

  // ---- Edit ----
  openEditModal(user: AppUser): void {
    this.selectedUser = user;
    this.form = {
      id: user.id,
      firstName: user.firstName,
      lastName: user.lastName,
      email: user.email,
      firstContact: user.firstContact,
      secondContact: user.secondContact,
      homeAddress: user.homeAddress,
      officeAddress: user.officeAddress,
      roleId: user.roleId,
      isActive: user.isActive
    };
    this.showEditModal = true;
  }

  submitEdit(editForm?: NgForm): void {
    if (!this.selectedUser) return;
    if (editForm && editForm.invalid) {
      Object.values(editForm.controls).forEach(c => c.markAsTouched());
      return;
    }
    this.saving = true;
    this.http.put(`${this.apiUrl}/${this.selectedUser.id}`, this.form).subscribe({
      next: () => {
        this.saving = false;
        this.showEditModal = false;
        this.fetchUsers();
      },
      error: () => {
        this.saving = false;
        alert('Failed to update user.');
      }
    });
  }

  // ---- Delete ----
  openDeleteModal(user: AppUser): void {
    this.selectedUser = user;
    this.showDeleteModal = true;
  }

  confirmDelete(): void {
    if (!this.selectedUser) return;
    this.saving = true;
    this.http.delete(`${this.apiUrl}/${this.selectedUser.id}`).subscribe({
      next: () => {
        this.saving = false;
        this.showDeleteModal = false;
        this.fetchUsers();
      },
      error: () => {
        this.saving = false;
        alert('Failed to delete user.');
      }
    });
  }

  restoreUser(user: AppUser): void {
    this.http.post(`${this.apiUrl}/${user.id}/restore`, {}).subscribe({
      next: () => {
        user.isDeleted = false;
        this.applyFilters();
      },
      error: () => alert('Failed to restore user.')
    });
  }

 confirmToggleActive(): void {
  if (!this.selectedUser) return;
  this.saving = true;
  this.http.patch<{ isActive: boolean }>(`${this.apiUrl}/${this.selectedUser.id}/toggle-active`, {}).subscribe({
    next: (res) => {
      if (this.selectedUser) this.selectedUser.isActive = res.isActive;
      this.saving = false;
      this.showToggleActiveModal = false;
      this.selectedUser = null;
    },
    error: () => {
      this.saving = false;
      alert('Failed to update status.');
    }
  });
}

openToggleActiveModal(user: AppUser): void {
  this.selectedUser = user;
  this.showToggleActiveModal = true;
}

  closeModals(): void {
  this.showAddModal = false;
  this.showEditModal = false;
  this.showDeleteModal = false;
  this.showToggleActiveModal = false;
  this.selectedUser = null;
}
}