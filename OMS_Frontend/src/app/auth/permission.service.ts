import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, tap, BehaviorSubject, of } from 'rxjs';
import { environment } from '../../environments/environment';

interface ScreenPermission {
  screenKey: string;
  canView: boolean;
  canAdd: boolean;
  canEdit: boolean;
  canDelete: boolean;
}

@Injectable({ providedIn: 'root' })
export class PermissionService {
  private map = new Map<string, ScreenPermission>();
  private loaded = false;
  private loading = false;
  private apiUrl = `${environment.apiUrl}/profile/permissions`;
  
  // For guards to wait for permissions to load
  private permissionsLoadedSubject = new BehaviorSubject<boolean>(false);
  permissionsLoaded$ = this.permissionsLoadedSubject.asObservable();

  constructor(private http: HttpClient) {}

  load(): Observable<ScreenPermission[]> {
    // Agar already loaded hain toh return karo
    if (this.loaded) {
      this.permissionsLoadedSubject.next(true);
      return of([]);
    }

    // Agar already loading ho rahi hai toh wait karo
    if (this.loading) {
      return new Observable(observer => {
        const subscription = this.permissionsLoaded$.subscribe(loaded => {
          if (loaded) {
            observer.next([]);
            observer.complete();
            subscription.unsubscribe();
          }
        });
      });
    }

    this.loading = true;

    return this.http.get<ScreenPermission[]>(this.apiUrl).pipe(
      tap({
        next: (data) => {
          this.map.clear();
          data.forEach((p) => this.map.set(p.screenKey, p));
          this.loaded = true;
          this.loading = false;
          this.permissionsLoadedSubject.next(true);
          console.log('✅ Permissions loaded successfully:', data);
        },
        error: (error) => {
          console.error('❌ Failed to load permissions:', error);
          this.loading = false;
          this.permissionsLoadedSubject.next(true);
        }
      })
    );
  }

  isLoaded(): boolean {
    return this.loaded;
  }

  permissionsLoaded(): boolean {
    return this.loaded;
  }

  canView(screenKey: string): boolean {
    const permission = this.map.get(screenKey);
    return permission?.canView ?? false;
  }

  canAdd(screenKey: string): boolean {
    const permission = this.map.get(screenKey);
    return permission?.canAdd ?? false;
  }

  canEdit(screenKey: string): boolean {
    const permission = this.map.get(screenKey);
    return permission?.canEdit ?? false;
  }

  canDelete(screenKey: string): boolean {
    const permission = this.map.get(screenKey);
    return permission?.canDelete ?? false;
  }

  reset(): void {
    this.map.clear();
    this.loaded = false;
    this.loading = false;
    this.permissionsLoadedSubject.next(false);
  }
}