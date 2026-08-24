import { Component, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import {
  FormBuilder,
  FormGroup,
  ReactiveFormsModule,
  Validators,
  AbstractControl,
  ValidationErrors,
} from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../auth/auth.service';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterLink],
  templateUrl: './login.component.html',
})
export class LoginComponent {
  loginForm: FormGroup;

  showPassword = signal(false);
  rememberMe = signal(false);
  isSubmitting = signal(false);
  loginError = signal<string | null>(null);

  constructor(private fb: FormBuilder, private authService: AuthService, private router: Router) {
    this.loginForm = this.fb.group({
      email: ['', [Validators.required, Validators.pattern(/^[^\s@]+@[^\s@]+\.[^\s@]{2,}$/)]],
      password: [
        '',
        [Validators.required, Validators.minLength(8), Validators.maxLength(32), this.passwordFormatValidator],
      ],
    });

    const savedEmail = localStorage.getItem('oms_remembered_email');
    if (savedEmail) {
      this.loginForm.patchValue({ email: savedEmail });
      this.rememberMe.set(true);
    }
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

  get email() {
    return this.loginForm.get('email');
  }
  get password() {
    return this.loginForm.get('password');
  }

  togglePasswordVisibility(): void {
    this.showPassword.update((v) => !v);
  }

  toggleRememberMe(): void {
    this.rememberMe.update((v) => !v);
  }

  emailErrorMessage(): string {
    if (this.email?.hasError('required')) return 'Email is required.';
    if (this.email?.hasError('pattern')) return 'Enter a valid email address (e.g. name@example.com).';
    return '';
  }

  passwordErrorMessage(): string {
    if (this.password?.hasError('required')) return 'Password is required.';
    if (this.password?.hasError('minlength')) return 'Password must be at least 8 characters.';
    if (this.password?.hasError('maxlength')) return 'Password must not exceed 32 characters.';
    if (this.password?.hasError('passwordFormat'))
      return 'Password must include uppercase, lowercase, a number, and a special character.';
    return '';
  }

  onSubmit(): void {
    this.loginError.set(null);

    if (this.loginForm.invalid) {
      this.loginForm.markAllAsTouched();
      return;
    }

    this.isSubmitting.set(true);
    const { email, password } = this.loginForm.value;

    if (this.rememberMe()) {
      localStorage.setItem('oms_remembered_email', email);
    } else {
      localStorage.removeItem('oms_remembered_email');
    }

    this.authService.login(email, password, this.rememberMe()).subscribe({
      next: () => {
        this.isSubmitting.set(false);
        this.router.navigate(['/dashboard']);
      },
      error: (err) => {
        this.isSubmitting.set(false);
        this.loginError.set(err?.error?.message || 'Invalid email or password.');
      },
    });
  }

  goToForgotPassword(): void {
    this.router.navigate(['/forgot-password']);
  }
}