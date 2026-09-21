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
  imports: [CommonModule, ReactiveFormsModule, RouterLink],
  templateUrl: './register.component.html',
})
export class RegisterComponent {

  registerForm: FormGroup;

  showPassword = signal(false);
  showConfirmPassword = signal(false);
  isSubmitting = signal(false);

  // kept for template binding; interceptor shows the toast now
  errorMessage = signal<string | null>(null);
  successMessage = signal<string | null>(null);

  constructor(
    private fb: FormBuilder,
    private authService: AuthService,
    private router: Router
  ) {
    this.registerForm = this.fb.group(
      {
        firstName: ['', [Validators.required, Validators.minLength(2), Validators.maxLength(50)]],
        lastName: ['', [Validators.required, Validators.minLength(2), Validators.maxLength(50)]],
        websiteUrl: ['https://', [Validators.pattern(/^https:\/\/(?:$|(?:[a-zA-Z0-9-]+\.)+[a-zA-Z]{2,}(?::\d+)?(?:[/?#].*)?)$/)]],
        email: ['', [Validators.required, Validators.pattern(/^[^\s@]+@[^\s@]+\.[^\s@]{2,}$/)]],
        firstContact: ['', [Validators.required, Validators.pattern(/^(?:03\d{2} \d{7}|\+92 3\d{2} \d{7})$/)]],
        secondContact: ['', [Validators.pattern(/^(?:03\d{2} \d{7}|\+92 3\d{2} \d{7})$/)]],
        homeAddress: ['', [Validators.maxLength(250)]],
        officeAddress: ['', [Validators.maxLength(250)]],
        password: ['', [Validators.required, Validators.minLength(8), Validators.maxLength(32), this.passwordFormatValidator]],
        confirmPassword: ['', [Validators.required]],
        agreeTerms: [false, [Validators.requiredTrue]]
      },
      { validators: this.passwordsMatchValidator }
    );
  }

  private passwordFormatValidator(control: AbstractControl): ValidationErrors | null {
    const value: string = control.value || '';
    if (!value) return null;
    const hasUpper = /[A-Z]/.test(value);
    const hasLower = /[a-z]/.test(value);
    const hasDigit = /[0-9]/.test(value);
    const hasSpecial = /[!@#$%^&*(),.?":{}|<>_\-\\]/.test(value);
    return hasUpper && hasLower && hasDigit && hasSpecial ? null : { passwordFormat: true };
  }

  private passwordsMatchValidator(group: AbstractControl): ValidationErrors | null {
    const password = group.get('password')?.value;
    const confirm = group.get('confirmPassword')?.value;
    if (!confirm) return null;
    return password === confirm ? null : { passwordsMismatch: true };
  }

  get f() {
    return this.registerForm.controls;
  }

  togglePassword(): void {
    this.showPassword.update((v) => !v);
  }

  toggleConfirmPassword(): void {
    this.showConfirmPassword.update((v) => !v);
  }

  fieldError(name: string): string {
    const control = this.registerForm.get(name);
    if (!control || !control.touched || control.valid) return '';

    if (control.hasError('required')) return 'This field is required.';
    if (name === 'email' && control.hasError('pattern')) return 'Enter a valid email address.';
    if (name === 'websiteUrl' && control.hasError('pattern')) return 'Enter a valid website URL.';
    if (name === 'firstContact' && control.hasError('pattern')) return 'Use 0300 1234567 or +92 300 1234567 format.';
    if (name === 'secondContact' && control.hasError('pattern')) return 'Use 0300 1234567 or +92 300 1234567 format.';
    if (control.hasError('minlength')) return `Minimum ${control.errors?.['minlength'].requiredLength} characters required.`;
    if (control.hasError('maxlength')) return `Maximum ${control.errors?.['maxlength'].requiredLength} characters allowed.`;
    if (name === 'password' && control.hasError('passwordFormat'))
      return 'Must include uppercase, lowercase, a number, and a special character.';

    return '';
  }

  showPasswordMismatch(): boolean {
    return this.registerForm.hasError('passwordsMismatch') && !!this.registerForm.get('confirmPassword')?.touched;
  }

  formatPakistanPhone(controlName: 'firstContact' | 'secondContact'): void {
    const control = this.registerForm.get(controlName);
    if (!control) return;

    const raw = String(control.value ?? '');
    let digits = raw.replace(/\D/g, '');
    const international = raw.trim().startsWith('+') || digits.startsWith('92');
    let formatted = '';

    if (international) {
      if (digits.startsWith('92')) digits = digits.slice(2);
      if (digits.startsWith('0')) digits = digits.slice(1);
      digits = digits.slice(0, 10);
      formatted = '+92';
      if (digits.length) formatted += ` ${digits.slice(0, 3)}`;
      if (digits.length > 3) formatted += ` ${digits.slice(3)}`;
    } else {
      digits = digits.slice(0, 11);
      formatted = digits.length > 4 ? `${digits.slice(0, 4)} ${digits.slice(4)}` : digits;
    }

    control.setValue(formatted, { emitEvent: false });
    control.updateValueAndValidity({ emitEvent: false });
  }

  normalizeWebsiteUrl(): void {
    const control = this.registerForm.get('websiteUrl');
    if (!control) return;
    const value = String(control.value ?? '').trim();
    if (!value) control.setValue('https://');
    else if (!/^https:\/\//i.test(value)) control.setValue(`https://${value.replace(/^https?:\/\//i, '')}`);
  }

  onSubmit(): void {
    if (this.registerForm.invalid) {
      this.registerForm.markAllAsTouched();
      return;
    }

    this.isSubmitting.set(true);

    const value = this.registerForm.value;
    const payload = {
      ...value,
      websiteUrl: value.websiteUrl === 'https://' ? undefined : value.websiteUrl,
      secondContact: value.secondContact || undefined
    };

    this.authService.register(payload).subscribe({
      next: () => {
        this.isSubmitting.set(false);
        this.router.navigate(['/dashboard/profile']);
      },
      error: () => {
        // Toast already shown by error interceptor.
        this.isSubmitting.set(false);
      }
    });
  }
}
