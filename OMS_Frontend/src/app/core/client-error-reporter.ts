import { ErrorHandler, Injectable } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { environment } from '../../environments/environment';

type ErrorKind = 'Runtime' | 'Promise' | 'Bootstrap' | 'Network' | 'SignalR' | 'Handled';
type Report = { kind: ErrorKind; locations: string };
const seen = new WeakSet<object>();
const pending: Report[] = [];
let sending = false;
let installed = false;
let retry: ReturnType<typeof setTimeout> | undefined;
let retryDelay = 5000;
let lastSent = 0;

// Keep a small in-memory queue. Do not retain personal data, URLs, tokens, messages
// or full stacks. A closed browser cannot deliver reports while the server is down.
export function reportClientError(error: unknown, kind: ErrorKind = 'Handled'): void {
  if (typeof window === 'undefined') return;
  // HTTP errors are owned by the server; only transport failures need a client report.
  if (error instanceof HttpErrorResponse && error.status !== 0) return;
  if (error && typeof error === 'object') {
    if (seen.has(error)) return;
    seen.add(error);
  }
  const stack = error instanceof Error ? error.stack ?? '' : '';
  const locations = Array.from(stack.matchAll(/(?:main|polyfills|scripts|chunk)(?:-[A-Za-z0-9]+)?\.js:\d{1,8}:\d{1,8}/g))
    .slice(0, 20).map(match => match[0]).join('\n');
  if (pending.some(item => item.kind === kind && item.locations === locations)) return;
  if (pending.length < 50) pending.push({ kind, locations });
  void flush();
}

async function flush(): Promise<void> {
  if (sending || retry || !pending.length || !navigator.onLine) return;
  const pause = Math.max(0, 4000 - (Date.now() - lastSent));
  if (pause) { schedule(pause); return; }
  sending = true;
  const controller = new AbortController();
  const timer = setTimeout(() => controller.abort(), 5000);
  try {
    let token: string | null = null;
    try { token = localStorage.getItem('oms_token'); } catch { /* Storage can be disabled. */ }
    // fetch bypasses Angular HTTP interceptors, preventing recursive error reporting.
    const response = await fetch(`${environment.apiUrl}/diagnostics/client-errors`, {
      method: 'POST', headers: { 'Content-Type': 'application/json', ...(token ? { Authorization: `Bearer ${token}` } : {}) },
      body: JSON.stringify(pending[0]), signal: controller.signal,
    });
    if (!response.ok && (response.status === 429 || response.status >= 500)) throw new Error('Retry telemetry');
    // Invalid reports are discarded; retrying them would never succeed.
    pending.shift();
    lastSent = Date.now();
    retryDelay = 5000;
  } catch {
    schedule(retryDelay);
    retryDelay = Math.min(retryDelay * 2, 60000);
  } finally {
    clearTimeout(timer);
    sending = false;
    if (pending.length && !retry) schedule(4000);
  }
}

function schedule(delay: number): void {
  if (!retry) retry = setTimeout(() => { retry = undefined; void flush(); }, delay);
}

export function installClientErrorReporting(): void {
  if (installed || typeof window === 'undefined') return;
  installed = true;
  window.addEventListener('error', event => reportClientError(event.error, 'Runtime'));
  window.addEventListener('unhandledrejection', event => reportClientError(event.reason, 'Promise'));
  window.addEventListener('online', () => { void flush(); });
}

@Injectable()
export class GlobalClientErrorHandler implements ErrorHandler {
  handleError(error: unknown): void {
    reportClientError(error, 'Runtime');
    if (!environment.production) console.error(error);
  }
}
