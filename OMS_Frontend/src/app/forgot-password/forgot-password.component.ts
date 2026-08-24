import { Component, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { AuthService } from '../auth/auth.service';

@Component({
  selector: 'app-forgot-password',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterLink],
  templateUrl: './forgot-password.component.html',
})
export class ForgotPasswordComponent {
  forgotForm: FormGroup;
  isSubmitting = signal(false);
  emailSent = signal(false);
  errorMessage = signal<string | null>(null);

  constructor(private fb: FormBuilder, private authService: AuthService) {
    this.forgotForm = this.fb.group({
      email: ['', [Validators.required, Validators.pattern(/^[^\s@]+@[^\s@]+\.[^\s@]{2,}$/)]],
    });
  }

  get email() {
    return this.forgotForm.get('email');
  }

  emailErrorMessage(): string {
    if (this.email?.hasError('required')) return 'Email is required.';
    if (this.email?.hasError('pattern')) return 'Enter a valid email address.';
    return '';
  }

  onSubmit(): void {
    this.errorMessage.set(null);

    if (this.forgotForm.invalid) {
      this.forgotForm.markAllAsTouched();
      return;
    }

    this.isSubmitting.set(true);
    const { email } = this.forgotForm.value;

    this.authService.forgotPassword(email).subscribe({
      next: () => {
        this.isSubmitting.set(false);
        this.emailSent.set(true);
      },
      error: () => {
        this.isSubmitting.set(false);
        // Still show generic success to avoid leaking whether email exists,
        // real network/server errors can show this instead:
        this.errorMessage.set('Something went wrong. Please try again.');
      },
    });
  }
}