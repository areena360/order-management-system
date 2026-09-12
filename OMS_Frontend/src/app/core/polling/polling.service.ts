import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';

@Injectable({ providedIn: 'root' })
export class PollingService {

  /**
   * Emits on a fixed interval while the tab is visible.
   * Automatically pauses when the tab is hidden, and emits
   * once immediately when the tab becomes visible again.
   */
  poll(intervalMs: number): Observable<void> {
    return new Observable<void>(subscriber => {
      let timerId: ReturnType<typeof setInterval> | null = null;

      const tick = () => subscriber.next();

      const startTimer = () => {
        if (timerId !== null) return;
        timerId = setInterval(tick, intervalMs);
      };

      const stopTimer = () => {
        if (timerId === null) return;
        clearInterval(timerId);
        timerId = null;
      };

      const onVisibilityChange = () => {
        if (document.hidden) {
          stopTimer();
        } else {
          tick();
          startTimer();
        }
      };

      document.addEventListener('visibilitychange', onVisibilityChange);

      if (!document.hidden) {
        startTimer();
      }

      return () => {
        stopTimer();
        document.removeEventListener('visibilitychange', onVisibilityChange);
      };
    });
  }
}