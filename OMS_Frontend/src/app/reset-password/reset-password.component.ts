import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators, AbstractControl, ValidationErrors } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { AuthService } from '../auth/auth.service';

@Component({
  selector: 'app-reset-password',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterLink],
  templateUrl: './reset-password.component.html',
})
export class ResetPasswordComponent implements OnInit {
  resetForm: FormGroup;

  token = '';
  email = '';
  linkInvalid = signal(false);

  showPassword = signal(false);
  showConfirmPassword = signal(false);
  isSubmitting = signal(false);
  resetSuccess = signal(false);
  errorMessage = signal<string | null>(null);

  constructor(
    private fb: FormBuilder,
    private authService: AuthService,
    private route: ActivatedRoute,
    private router: Router
  ) {
    this.resetForm = this.fb.group(
      {
        newPassword: [
          '',
          [Validators.required, Validators.minLength(8), Validators.maxLength(32), this.passwordFormatValidator],
        ],
        confirmPassword: ['', [Validators.required]],
      },
      { validators: this.passwordsMatchValidator }
    );
  }

  ngOnInit(): void {
    this.route.queryParamMap.subscribe((params) => {
      this.token = params.get('token') || '';
      this.email = params.get('email') || '';

      if (!this.token || !this.email) {
        this.linkInvalid.set(true);
      }
    });
  }

  private passwordFormatValidator(control: AbstractControl): ValidationErrors | null {
    const value: string = control.value || '';
    if (!value) return null;
    const hasUpper = /[A-Z]/.test(value);
    const hasLower = /[a-z]/.test(value);
    const hasDigit = /[0-9]/.test(value);
    const hasSpecial = /[!@#$%^&*(),.?":{}|<>_\-]/.test(value);
    return hasUpper && hasLower && hasDigit && hasSpecial ? null : { passwordFormat: true };
  }

  private passwordsMatchValidator(group: AbstractControl): ValidationErrors | null {
    const password = group.get('newPassword')?.value;
    const confirm = group.get('confirmPassword')?.value;
    if (!confirm) return null;
    return password === confirm ? null : { passwordsMismatch: true };
  }

  get newPassword() {
    return this.resetForm.get('newPassword');
  }

  togglePassword(): void {
    this.showPassword.update((v) => !v);
  }

  toggleConfirmPassword(): void {
    this.showConfirmPassword.update((v) => !v);
  }

  passwordErrorMessage(): string {
    if (this.newPassword?.hasError('required')) return 'Password is required.';
    if (this.newPassword?.hasError('minlength')) return 'Password must be at least 8 characters.';
    if (this.newPassword?.hasError('maxlength')) return 'Password must not exceed 32 characters.';
    if (this.newPassword?.hasError('passwordFormat'))
      return 'Must include uppercase, lowercase, a number, and a special character.';
    return '';
  }

  showPasswordMismatch(): boolean {
    return this.resetForm.hasError('passwordsMismatch') && !!this.resetForm.get('confirmPassword')?.touched;
  }

  onSubmit(): void {
    this.errorMessage.set(null);

    if (this.resetForm.invalid) {
      this.resetForm.markAllAsTouched();
      return;
    }

    this.isSubmitting.set(true);
    const { newPassword, confirmPassword } = this.resetForm.value;

    this.authService
      .resetPassword({ token: this.token, email: this.email, newPassword, confirmPassword })
      .subscribe({
        next: () => {
          this.isSubmitting.set(false);
          this.resetSuccess.set(true);
          setTimeout(() => this.router.navigate(['/login']), 2000);
        },
        error: (err) => {
          this.isSubmitting.set(false);
          this.errorMessage.set(err?.error?.message || 'This reset link is invalid or has expired.');
        },
      });
  }
}