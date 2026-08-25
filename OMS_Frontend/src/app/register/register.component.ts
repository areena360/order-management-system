import { Component, HostListener, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import {
  FormBuilder,
  FormGroup,
  ReactiveFormsModule,
  Validators,
  AbstractControl,
  ValidationErrors
} from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../auth/auth.service';

@Component({
  selector: 'app-register',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    RouterLink
  ],
  templateUrl: './register.component.html',
})
export class RegisterComponent {

  registerForm: FormGroup;

  showPassword = signal(false);
  showConfirmPassword = signal(false);

  isSubmitting = signal(false);

  errorMessage = signal<string | null>(null);
  successMessage = signal<string | null>(null);

  constructor(
    private fb: FormBuilder,
    private authService: AuthService,
    private router: Router
  ) {

    this.registerForm = this.fb.group(
      {
        // ============================================================
        // PERSONAL INFORMATION
        // ============================================================

        firstName: [
          '',
          [
            Validators.required,
            Validators.minLength(2),
            Validators.maxLength(50)
          ]
        ],

        lastName: [
          '',
          [
            Validators.required,
            Validators.minLength(2),
            Validators.maxLength(50)
          ]
        ],

        // ============================================================
        // WEBSITE
        // ============================================================

        websiteUrl: [
          '',
          [
            Validators.pattern(
              /^https?:\/\/.+\..+/
            )
          ]
        ],

        // ============================================================
        // EMAIL
        // ============================================================

        email: [
          '',
          [
            Validators.required,
            Validators.pattern(
              /^[^\s@]+@[^\s@]+\.[^\s@]{2,}$/
            )
          ]
        ],

        // ============================================================
        // CONTACT NUMBERS
        // ============================================================

        firstContact: [
          '',
          [
            Validators.required,
            Validators.pattern(
              /^[0-9+\-\s()]{7,15}$/
            )
          ]
        ],

        secondContact: [
          '',
          [
            Validators.pattern(
              /^[0-9+\-\s()]{7,15}$/
            )
          ]
        ],

        // ============================================================
        // ADDRESSES
        // ============================================================

        homeAddress: [
          '',
          [
            Validators.maxLength(250)
          ]
        ],

        officeAddress: [
          '',
          [
            Validators.maxLength(250)
          ]
        ],

        // ============================================================
        // PASSWORD
        // ============================================================

        password: [
          '',
          [
            Validators.required,
            Validators.minLength(8),
            Validators.maxLength(32),
            this.passwordFormatValidator
          ]
        ],

        confirmPassword: [
          '',
          [
            Validators.required
          ]
        ],

        // ============================================================
        // TERMS
        // ============================================================

        agreeTerms: [
          false,
          [
            Validators.requiredTrue
          ]
        ]
      },

      {
        validators: this.passwordsMatchValidator
      }
    );
  }

  // ================================================================
  // PASSWORD FORMAT VALIDATOR
  // ================================================================

  private passwordFormatValidator(
    control: AbstractControl
  ): ValidationErrors | null {

    const value: string = control.value || '';

    if (!value) {
      return null;
    }

    const hasUpper = /[A-Z]/.test(value);
    const hasLower = /[a-z]/.test(value);
    const hasDigit = /[0-9]/.test(value);

    const hasSpecial =
      /[!@#$%^&*(),.?":{}|<>_\-\\]/.test(value);

    return hasUpper &&
      hasLower &&
      hasDigit &&
      hasSpecial
      ? null
      : {
          passwordFormat: true
        };
  }

  // ================================================================
  // PASSWORD MATCH VALIDATOR
  // ================================================================

  private passwordsMatchValidator(
    group: AbstractControl
  ): ValidationErrors | null {

    const password =
      group.get('password')?.value;

    const confirm =
      group.get('confirmPassword')?.value;

    if (!confirm) {
      return null;
    }

    return password === confirm
      ? null
      : {
          passwordsMismatch: true
        };
  }

  // ================================================================
  // FORM CONTROLS
  // ================================================================

  get f() {
    return this.registerForm.controls;
  }

  // ================================================================
  // PASSWORD VISIBILITY
  // ================================================================

  togglePassword(): void {

    this.showPassword.update(
      (v) => !v
    );
  }

  toggleConfirmPassword(): void {

    this.showConfirmPassword.update(
      (v) => !v
    );
  }

  // ================================================================
  // FIELD ERROR
  // ================================================================

  fieldError(name: string): string {

    const control =
      this.registerForm.get(name);

    if (
      !control ||
      !control.touched ||
      control.valid
    ) {
      return '';
    }

    if (control.hasError('required')) {
      return 'This field is required.';
    }

    if (
      name === 'email' &&
      control.hasError('pattern')
    ) {
      return 'Enter a valid email address.';
    }

    if (
      name === 'websiteUrl' &&
      control.hasError('pattern')
    ) {
      return 'Enter a valid website URL.';
    }

    if (
      name === 'firstContact' &&
      control.hasError('pattern')
    ) {
      return 'Enter a valid phone number.';
    }

    if (
      name === 'secondContact' &&
      control.hasError('pattern')
    ) {
      return 'Enter a valid phone number.';
    }

    if (
      control.hasError('minlength')
    ) {
      return `Minimum ${
        control.errors?.['minlength'].requiredLength
      } characters required.`;
    }

    if (
      control.hasError('maxlength')
    ) {
      return `Maximum ${
        control.errors?.['maxlength'].requiredLength
      } characters allowed.`;
    }

    if (
      name === 'password' &&
      control.hasError('passwordFormat')
    ) {
      return 'Must include uppercase, lowercase, a number, and a special character.';
    }

    return '';
  }

  // ================================================================
  // PASSWORD MISMATCH
  // ================================================================

  showPasswordMismatch(): boolean {

    return (
      this.registerForm.hasError(
        'passwordsMismatch'
      ) &&
      !!this.registerForm
        .get('confirmPassword')
        ?.touched
    );
  }

  // ================================================================
  // SUBMIT
  // ================================================================

  onSubmit(): void {

    if (this.registerForm.invalid) {

      this.registerForm.markAllAsTouched();

      return;
    }

    this.isSubmitting.set(true);

    this.errorMessage.set(null);
    this.successMessage.set(null);

    this.authService
      .register(this.registerForm.value)
      .subscribe({

        // ==========================================================
        // REGISTRATION SUCCESS
        // ==========================================================

        next: () => {

          this.isSubmitting.set(false);

          // User is already logged in because
          // AuthService.register() saves JWT.
          this.router.navigate([
            '/dashboard/profile'
          ]);
        },

        // ==========================================================
        // REGISTRATION ERROR
        // ==========================================================

        error: (err) => {

          this.isSubmitting.set(false);

          this.errorMessage.set(
            err.error?.message ||
            'Registration failed. Please try again.'
          );
        }
      });
  }
}