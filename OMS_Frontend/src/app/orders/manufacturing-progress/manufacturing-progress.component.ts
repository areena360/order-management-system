import { AfterViewInit, Component, ElementRef, EventEmitter, HostListener, Input, OnDestroy, OnInit, Output, ViewChild, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Subject, takeUntil } from 'rxjs';
import { ManufacturingEvent, OrdersService } from '../orders.service';
import { PollingService } from '../../core/polling/polling.service';
import { manufacturingStatusClass, manufacturingStatusLabel } from '../manufacturing-status.util';

interface MemberProgress { userId: number; name: string; status: string; events: ManufacturingEvent[]; }
interface RoleProgress { roleId: number; name: string; members: MemberProgress[]; }

@Component({
  selector: 'app-manufacturing-progress', standalone: true, imports: [CommonModule],
  templateUrl: './manufacturing-progress.component.html',
  styles: [`:host { display: contents; } .progress-panel { animation: enter .18s ease-out; }
    @keyframes enter { from { opacity: 0; transform: translateY(12px) scale(.98); } to { opacity: 1; transform: none; } }
    @media (prefers-reduced-motion: reduce) { .progress-panel { animation: none; } }`]
})
export class ManufacturingProgressComponent implements OnInit, AfterViewInit, OnDestroy {
  @Input({ required: true }) orderId!: number;
  @Input() orderNumber = '';
  @Output() closed = new EventEmitter<void>();
  @ViewChild('closeButton') closeButton?: ElementRef<HTMLButtonElement>;
  @ViewChild('panel') panel?: ElementRef<HTMLElement>;
  private readonly orders = inject(OrdersService);
  private readonly polling = inject(PollingService);
  private readonly destroy$ = new Subject<void>();
  private opener = document.activeElement as HTMLElement | null;
  groups: RoleProgress[] = [];
  loading = true;
  refreshing = false;
  error = '';
  updatedAt: Date | null = null;
  statusLabel = manufacturingStatusLabel;
  statusClass = manufacturingStatusClass;
  get members(): MemberProgress[] { return this.groups.flatMap(g => g.members).filter(m => m.status !== 'unassigned'); }
  get done(): number { return this.members.filter(m => m.status === 'done').length; }
  get inProgress(): number { return this.members.filter(m => m.status === 'inprogress').length; }
  get percent(): number { return this.members.length ? Math.round(this.done / this.members.length * 100) : 0; }

  ngOnInit(): void {
    this.refresh();
    this.polling.poll(10000).pipe(takeUntil(this.destroy$)).subscribe(() => this.refresh());
  }
  ngAfterViewInit(): void { this.closeButton?.nativeElement.focus(); }
  ngOnDestroy(): void { this.destroy$.next(); this.destroy$.complete(); this.opener?.focus(); }
  @HostListener('document:keydown.escape') close(): void { this.closed.emit(); }
  @HostListener('keydown', ['$event']) trapFocus(event: KeyboardEvent): void {
    if (event.key !== 'Tab') return;
    const nodes = this.panel?.nativeElement.querySelectorAll<HTMLElement>('button:not([disabled]), [tabindex="0"]');
    if (!nodes?.length) return;
    const first = nodes[0], last = nodes[nodes.length - 1];
    if (event.shiftKey && document.activeElement === first) { event.preventDefault(); last.focus(); }
    else if (!event.shiftKey && document.activeElement === last) { event.preventDefault(); first.focus(); }
  }
  refresh(): void {
    if (this.refreshing) return;
    this.refreshing = true;
    this.orders.getManufacturing(this.orderId).pipe(takeUntil(this.destroy$)).subscribe({
      next: response => {
        const groups = new Map<number, RoleProgress>();
        for (const event of response.events) {
          let group = groups.get(event.roleId);
          if (!group) { group = { roleId: event.roleId, name: event.roleName, members: [] }; groups.set(event.roleId, group); }
          let member = group.members.find(m => m.userId === event.userId);
          if (!member) { member = { userId: event.userId, name: event.userName, status: event.status, events: [] }; group.members.push(member); }
          member.status = event.status;
          member.events.push(event);
        }
        this.groups = [...groups.values()];
        this.loading = this.refreshing = false;
        this.error = '';
        this.updatedAt = new Date();
      },
      error: () => { this.loading = this.refreshing = false; this.error = 'Unable to refresh manufacturing progress. Please retry.'; }
    });
  }
}
