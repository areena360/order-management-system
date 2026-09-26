import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, tap, BehaviorSubject, of, finalize, shareReplay } from 'rxjs';
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

  private request?: Observable<ScreenPermission[]>;
  load(force = false): Observable<ScreenPermission[]> {
    if (this.request) return this.request;
    if (this.loaded && !force) return of([...this.map.values()]);
    this.request = this.http.get<ScreenPermission[]>(this.apiUrl).pipe(
      tap(data => {
        this.map = new Map(data.map(p => [p.screenKey, p]));
        this.loaded = true;
        this.permissionsLoadedSubject.next(true);
      }),
      finalize(() => this.request = undefined),
      shareReplay({ bufferSize: 1, refCount: false })
    );
    return this.request;
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
