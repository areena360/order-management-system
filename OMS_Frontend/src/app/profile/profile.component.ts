import { Component, OnInit, computed, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { AuthService, UserProfile } from '../auth/auth.service';
import { FooterComponent } from "../footer/footer.component";

@Component({
  selector: 'app-profile',
  standalone: true,
  imports: [CommonModule, FormsModule, FooterComponent],
  templateUrl: './profile.component.html',
})
export class ProfileComponent implements OnInit {
  user = signal<UserProfile | null>(null);
  isLoading = signal(true);
  errorMessage = signal<string | null>(null);

  // Change password modal state
  showPasswordModal = signal(false);
  isSaving = signal(false);
  passwordError = signal<string | null>(null);
  passwordSuccess = signal(false);

  showOldPassword = signal(false);
  showNewPassword = signal(false);
  showConfirmPassword = signal(false);

  oldPassword = '';
  newPassword = '';
  confirmPassword = '';

  initials = computed(() => {
    const u = this.user();
    return u ? `${u.firstName?.[0] ?? ''}${u.lastName?.[0] ?? ''}`.toUpperCase() : '';
  });

  constructor(private authService: AuthService, private router: Router) {}

  ngOnInit(): void {
    this.loadProfile();
  }

  private loadProfile(): void {
    this.isLoading.set(true);
    this.authService.getProfile().subscribe({
      next: (profile) => {
        this.user.set(profile);
        this.isLoading.set(false);
      },
      error: () => this.isLoading.set(false),
    });
  }

  onEdit(): void {
    this.router.navigate(['/profile/edit']);
  }

  onChangePassword(): void {
    this.oldPassword = '';
    this.newPassword = '';
    this.confirmPassword = '';
    this.passwordError.set(null);
    this.passwordSuccess.set(false);
    this.showOldPassword.set(false);
    this.showNewPassword.set(false);
    this.showConfirmPassword.set(false);
    this.showPasswordModal.set(true);
  }

  closePasswordModal(): void {
    this.showPasswordModal.set(false);
  }

  toggleOldPassword(): void {
    this.showOldPassword.update((v) => !v);
  }

  toggleNewPassword(): void {
    this.showNewPassword.update((v) => !v);
  }

  toggleConfirmPassword(): void {
    this.showConfirmPassword.update((v) => !v);
  }

  private isValidPasswordFormat(value: string): boolean {
    const hasUpper = /[A-Z]/.test(value);
    const hasLower = /[a-z]/.test(value);
    const hasDigit = /[0-9]/.test(value);
    const hasSpecial = /[!@#$%^&*(),.?":{}|<>_\-\\]/.test(value);
    return hasUpper && hasLower && hasDigit && hasSpecial;
  }

  submitPasswordChange(): void {
    this.passwordError.set(null);

    if (!this.oldPassword || !this.newPassword || !this.confirmPassword) {
      this.passwordError.set('Please fill in all fields.');
      return;
    }
    if (this.newPassword.length < 8 || this.newPassword.length > 32) {
      this.passwordError.set('New password must be 8–32 characters.');
      return;
    }
    if (!this.isValidPasswordFormat(this.newPassword)) {
      this.passwordError.set('Must include uppercase, lowercase, a number, and a special character.');
      return;
    }
    if (this.newPassword !== this.confirmPassword) {
      this.passwordError.set('New password and confirm password do not match.');
      return;
    }

    this.isSaving.set(true);
    this.authService.changePassword({
      oldPassword: this.oldPassword,
      newPassword: this.newPassword,
    }).subscribe({
      next: () => {
        this.isSaving.set(false);
        this.passwordSuccess.set(true);
        setTimeout(() => this.closePasswordModal(), 1200);
      },
      error: (err) => {
        this.isSaving.set(false);
        this.passwordError.set(err?.error?.message || 'Failed to change password.');
      },
    });
  }
}