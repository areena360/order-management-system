import { Component, ElementRef, HostListener, Input, forwardRef, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ControlValueAccessor, NG_VALUE_ACCESSOR } from '@angular/forms';

@Component({
  selector: 'app-woo-select', standalone: true, imports: [CommonModule],
  providers: [{provide: NG_VALUE_ACCESSOR, useExisting: forwardRef(() => WooSelectComponent), multi: true}],
  template: `<div class="relative">
    <button type="button" [disabled]="disabled" [attr.aria-label]="label" aria-haspopup="listbox" [attr.aria-expanded]="open"
      (click)="open = !open" (keydown.arrowdown)="$event.preventDefault(); open = true"
      class="w-full flex items-center justify-between gap-3 rounded-lg border border-gray-300 bg-white px-3 py-2 text-sm text-left text-gray-700 hover:bg-gray-50 focus:outline-none focus:ring-2 focus:ring-gray-500 disabled:opacity-50">
      <span class="truncate" [class.text-gray-400]="value === null || value === 0">{{ selectedLabel }}</span>
      <svg class="h-3.5 w-3.5 shrink-0 text-gray-400 transition-transform" [class.rotate-180]="open" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2"><path d="m6 9 6 6 6-6"/></svg>
    </button>
    <div *ngIf="open" role="listbox" [attr.aria-label]="label" class="absolute left-0 z-30 mt-2 w-full max-h-56 overflow-auto rounded-lg bg-white p-1.5 shadow-lg ring-1 ring-gray-200">
      <button type="button" role="option" [attr.aria-selected]="value === emptyValue" (click)="choose(emptyValue)" class="block w-full rounded-md px-2.5 py-2 text-left text-sm text-gray-500 hover:bg-gray-50">{{placeholder}}</button>
      <button *ngFor="let o of options" type="button" role="option" [attr.aria-selected]="value === o.id" (click)="choose(o.id)"
        class="block w-full rounded-md px-2.5 py-2 text-left text-sm text-gray-700 hover:bg-gray-50 focus:bg-gray-100" [class.bg-gray-100]="value === o.id" [class.font-medium]="value === o.id">{{o.name}}</button>
    </div>
  </div>`,
  styles: [':host{display:block;margin-top:6px}button{cursor:pointer}']
})
export class WooSelectComponent implements ControlValueAccessor {
  @Input() options: {id: any; name: string}[] = [];
  @Input() placeholder = 'Select';
  @Input() label = 'Select option';
  @Input() emptyValue: any = 0;
  value: any = 0; open = false; disabled = false;
  private host = inject(ElementRef);
  change = (_: any) => {}; touched = () => {};
  get selectedLabel() { return this.options.find(o => o.id === this.value)?.name ?? this.placeholder; }
  choose(value: any) { this.value = value; this.change(value); this.touched(); this.open = false; }
  writeValue(value: any) { this.value = value; }
  registerOnChange(fn: any) { this.change = fn; }
  registerOnTouched(fn: any) { this.touched = fn; }
  setDisabledState(value: boolean) { this.disabled = value; }
  @HostListener('document:click', ['$event']) outside(event: MouseEvent) { if (!this.host.nativeElement.contains(event.target)) { if(this.open) this.touched(); this.open = false; } }
  @HostListener('keydown.escape') escape() { this.open = false; }
}
