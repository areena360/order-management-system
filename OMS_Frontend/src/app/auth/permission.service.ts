import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, tap } from 'rxjs';

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
  private apiUrl = 'https://localhost:44370/api/profile/permissions';

  constructor(private http: HttpClient) {}

  load(): Observable<ScreenPermission[]> {
    return this.http.get<ScreenPermission[]>(this.apiUrl).pipe(
      tap((data) => {
        this.map.clear();
        data.forEach((p) => this.map.set(p.screenKey, p));
        this.loaded = true;
      })
    );
  }

  isLoaded(): boolean {
    return this.loaded;
  }

  canView(screenKey: string): boolean {
    return this.map.get(screenKey)?.canView ?? false;
  }

  canAdd(screenKey: string): boolean {
    return this.map.get(screenKey)?.canAdd ?? false;
  }

  canEdit(screenKey: string): boolean {
    return this.map.get(screenKey)?.canEdit ?? false;
  }

  canDelete(screenKey: string): boolean {
    return this.map.get(screenKey)?.canDelete ?? false;
  }

  reset(): void {
    this.map.clear();
    this.loaded = false;
  }
}