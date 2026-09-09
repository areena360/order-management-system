import { Component, signal, OnInit } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { PermissionService } from './auth/permission.service';
import { AuthService } from './auth/auth.service';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet],
  templateUrl: './app.html',
  styleUrl: './app.css'
})
export class App implements OnInit {
  protected readonly title = signal('OrderManagementSystem');

  constructor(
    private permissionService: PermissionService,
    private authService: AuthService
  ) {}

  ngOnInit(): void {
    // Agar user logged in hai toh permissions load karo
    if (this.authService.getToken()) {
      console.log('🔵 Loading permissions on app start...');
      this.permissionService.load().subscribe({
        next: () => {
          console.log('✅ Permissions loaded successfully on app start');
        },
        error: (error) => {
          console.error('❌ Failed to load permissions on app start:', error);
        }
      });
    }
  }
}