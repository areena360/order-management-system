import { fakeAsync, TestBed, tick } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { environment } from '../../environments/environment';
import { ManageUsersComponent } from './manage-users.component';

describe('Server-paginated users', () => {
  let component: ManageUsersComponent;
  let http: HttpTestingController;
  const url = `${environment.apiUrl}/users`;
  const page = (pageNumber = 1, totalCount = 100) => ({
    items: [{ id: pageNumber * 8, firstName: 'Page', lastName: String(pageNumber), role: 'Auditor' }],
    totalCount, pageNumber, pageSize: 8
  });
  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    component = TestBed.createComponent(ManageUsersComponent).componentInstance;
    http = TestBed.inject(HttpTestingController);
  });
  afterEach(() => http.verify());

  it('requests the displayed page and uses server totals without slicing it again', () => {
    component.fetchUsers();
    let request = http.expectOne(r => r.url === url);
    expect(request.request.params.get('pageSize')).toBe('8');
    request.flush(page());
    expect(component.totalPages).toBe(13);
    component.selectedUserIds.add(8);
    component.goToPage(2);
    request = http.expectOne(r => r.url === url);
    expect(request.request.params.get('pageNumber')).toBe('2');
    request.flush(page(2));
    expect(component.paginatedUsers.map(u => u.id)).toEqual([16]);
    expect(component.selectedUserIds.size).toBe(0);
    expect(component.pageNumbers.length).toBeLessThanOrEqual(5);
  });

  it('sends filters and sorting to the API and resets the page', () => {
    component.currentPage = 5;
    component.searchTerm = 'Example';
    component.roleFilter = 'Auditor';
    component.statusFilter = 'deleted';
    component.sort('fullName');
    const request = http.expectOne(r => r.url === url);
    for (const [key, value] of Object.entries({ pageNumber: '1', search: 'Example', role: 'Auditor', status: 'deleted', sortBy: 'fullName', sortDirection: 'asc' }))
      expect(request.request.params.get(key)).toBe(value);
    request.flush(page(1, 1));
  });

  it('requests the new page size and accepts a server-adjusted page after deletion', () => {
    component.currentPage = 4;
    component.selectPageSize(25);
    const request = http.expectOne(r => r.url === url);
    expect(request.request.params.get('pageSize')).toBe('25');
    expect(request.request.params.get('pageNumber')).toBe('1');
    request.flush({ ...page(1, 1), pageSize: 25 });
    component.currentPage = 2;
    component.fetchUsers();
    http.expectOne(r => r.url === url).flush({ items: [], totalCount: 0, pageNumber: 1, pageSize: 25 });
    expect(component.currentPage).toBe(1);
    expect(component.totalPages).toBe(1);
  });

  it('debounces search and cancels outdated requests', fakeAsync(() => {
    component.fetchUsers();
    const stale = http.expectOne(r => r.url === url);
    component.searchTerm = 'Aud';
    component.onSearchChange();
    expect(stale.cancelled).toBeTrue();
    tick(200);
    component.searchTerm = 'Auditor';
    component.onSearchChange();
    tick(299);
    http.expectNone(r => r.url === url);
    tick(1);
    const request = http.expectOne(r => r.url === url);
    expect(request.request.params.get('search')).toBe('Auditor');
    request.flush(page());
    tick();
  }));

  it('clears old data on failure and permits retry', () => {
    component.fetchUsers();
    http.expectOne(r => r.url === url).flush({}, { status: 500, statusText: 'Failed' });
    expect(component.errorMsg).toContain('retry');
    expect(component.users).toEqual([]);
    component.fetchUsers();
    http.expectOne(r => r.url === url).flush(page());
    expect(component.errorMsg).toBe('');
  });
});
