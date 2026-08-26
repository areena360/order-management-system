import { Component, OnInit, computed, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { AuthService, UserProfile } from '../auth/auth.service';
import { FooterComponent } from "../footer/footer.component";

@Component({
  selector: 'app-profile',
  standalone: true,
  imports: [CommonModule, FooterComponent],
  templateUrl: './profile.component.html',
})
export class ProfileComponent implements OnInit {
  user = signal<UserProfile | null>(null);
  isLoading = signal(true);

  // kept for template binding; interceptor shows the toast now
  errorMessage = signal<string | null>(null);

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
      error: () => {
        // Toast already shown by error interceptor.
        this.isLoading.set(false);
      },
    });
  }

  onEdit(): void {
    this.router.navigate(['/profile/edit']);
  }

  onChangePassword(): void {
    this.router.navigate(['/profile/change-password']);
  }
}