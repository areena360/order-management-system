import { fakeAsync, flushMicrotasks, tick } from '@angular/core/testing';
import { HttpErrorResponse } from '@angular/common/http';
import { reportClientError } from './client-error-reporter';

describe('client exception delivery', () => {
  it('retries transport failure, deduplicates errors and sends no private messages or URLs', fakeAsync(() => {
    spyOnProperty(navigator, 'onLine', 'get').and.returnValue(true);
    const request = spyOn(window, 'fetch').and.returnValues(
      Promise.reject(new Error('offline')),
      Promise.resolve(new Response(null, { status: 202 }))
    );
    const error = new Error('password=secret customer@example.com');
    error.stack = 'Error: password=secret\n at https://site.example/main-ABC.js:12:34?token=secret';
    reportClientError(error, 'Runtime');
    reportClientError(error, 'Runtime');
    reportClientError(new HttpErrorResponse({ status: 500 }), 'Handled');
    flushMicrotasks();
    expect(request).toHaveBeenCalledTimes(1);
    const payload = String(request.calls.mostRecent().args[1]?.body);
    expect(JSON.parse(payload)).toEqual({ kind: 'Runtime', locations: 'main-ABC.js:12:34' });
    expect(payload).not.toContain('secret');
    expect(payload).not.toContain('site.example');
    tick(5000);
    flushMicrotasks();
    expect(request).toHaveBeenCalledTimes(2);
    tick(60000);
    expect(request).toHaveBeenCalledTimes(2);
  }));
});
