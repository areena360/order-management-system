import { ApplicationConfig } from '@angular/core';
import { provideRouter } from '@angular/router';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { routes } from './app.routes';
import { jwtInterceptor } from './interceptors/jwt.interceptor';
 
export const appConfig: ApplicationConfig = {
  providers: [
    provideRouter(routes),
<<<<<<< Updated upstream
    provideHttpClient(withInterceptors([jwtInterceptor])),
=======
    provideAnimations(), // required by ngx-toastr
    provideHttpClient(withInterceptors([jwtInterceptor, errorInterceptor])),
    provideToastr({
      timeOut: 4000,
      positionClass: 'toast-top-center',
      preventDuplicates: true,
      closeButton: true,
      progressBar: true,
    }),
>>>>>>> Stashed changes
  ],
};