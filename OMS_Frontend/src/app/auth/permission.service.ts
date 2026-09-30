import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, tap, BehaviorSubject, of, finalize, shareReplay } from 'rxjs';
import { environment } from '../../environments/environment';
import { AuthService } from './auth.service';

interface ScreenPermission {
  screenKey: string;
  canView: boolean;
  canAdd: boolean;
  canEdit: boolean;
  canDelete: boolean;
  adminAssignedOnly?: boolean;
}

@Injectable({ providedIn: 'root' })
export class PermissionService {
  private map = new Map<string, ScreenPermission>();
  private loaded = false;
  private generation = 0;
  private apiUrl = `${environment.apiUrl}/profile/permissions`;
  
  // For guards to wait for permissions to load
  private permissionsLoadedSubject = new BehaviorSubject<boolean>(false);
  permissionsLoaded$ = this.permissionsLoadedSubject.asObservable();

  constructor(private http: HttpClient, auth: AuthService) {
    auth.sessionChanged$.subscribe(loggedIn => {
      this.reset();
      if (loggedIn) this.load().subscribe({ error: () => {} });
    });
  }

  private request?: Observable<ScreenPermission[]>;
  load(force = false): Observable<ScreenPermission[]> {
    if (this.request) return this.request;
    if (this.loaded && !force) return of([...this.map.values()]);
    const generation = this.generation;
    this.request = this.http.get<ScreenPermission[]>(this.apiUrl).pipe(
      tap(data => {
        if (generation !== this.generation) return;
        this.map = new Map(data.map(p => [p.screenKey, p]));
        this.loaded = true;
        this.permissionsLoadedSubject.next(true);
      }),
      finalize(() => {
        if (generation === this.generation) this.request = undefined;
      }),
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
    if (screenKey === 'Orders' && permission?.adminAssignedOnly) return true;
    return permission?.canView ?? false;
  }

  adminAssignedOrdersOnly(): boolean {
    return this.map.get('Orders')?.adminAssignedOnly === true;
  }

  canAdd(screenKey: string): boolean {
    if (screenKey === 'Orders' && this.adminAssignedOrdersOnly()) return false;
    const permission = this.map.get(screenKey);
    return permission?.canAdd ?? false;
  }

  canEdit(screenKey: string): boolean {
    if (['Orders', 'Order Tracking', 'Order Amount'].includes(screenKey) && this.adminAssignedOrdersOnly()) return false;
    const permission = this.map.get(screenKey);
    return permission?.canEdit ?? false;
  }

  canDelete(screenKey: string): boolean {
    if (screenKey === 'Orders' && this.adminAssignedOrdersOnly()) return false;
    const permission = this.map.get(screenKey);
    return permission?.canDelete ?? false;
  }

  reset(): void {
    this.generation++;
    this.request = undefined;
    this.map.clear();
    this.loaded = false;
    this.permissionsLoadedSubject.next(false);
  }
}
