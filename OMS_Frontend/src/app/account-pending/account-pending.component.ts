import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';

@Component({
    selector: 'app-account-pending',
    standalone: true,
    imports: [RouterLink],
    template: `
    <div class="min-h-screen w-full flex items-center justify-center bg-gray-100 px-4 py-10">
      <div class="w-full max-w-lg text-center">

        <!-- Status icon -->
        <div class="mx-auto h-16 w-16 rounded-full bg-amber-50 border-2 border-amber-200 flex items-center justify-center mb-6">
          <svg xmlns="http://www.w3.org/2000/svg" class="h-8 w-8 text-amber-500" fill="none" viewBox="0 0 24 24" stroke-width="1.6" stroke="currentColor">
            <path stroke-linecap="round" stroke-linejoin="round" d="M12 6v6l4 2" />
            <circle cx="12" cy="12" r="9" stroke-linecap="round" />
          </svg>
        </div>

        <h1 class="text-2xl font-semibold text-gray-900">Your account is under review</h1>
        <p class="mt-2 text-sm text-gray-500 max-w-sm mx-auto">
          Thank you for registering. Your account has been created successfully and is currently under review.
            Our team will verify your details shortly, and you'll be able to log in once your account is activated.
        </p>

        <!-- Progress steps -->
        <div class="mt-10 flex items-center justify-center">
          <div class="flex items-center">
            <div class="flex flex-col items-center">
              <div class="h-8 w-8 rounded-full bg-gray-800 flex items-center justify-center">
                <svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4 text-white" fill="none" viewBox="0 0 24 24" stroke-width="2" stroke="currentColor">
                  <path stroke-linecap="round" stroke-linejoin="round" d="M4.5 12.75l6 6 9-13.5" />
                </svg>
              </div>
              <span class="mt-2 text-xs font-medium text-gray-700">Registered</span>
            </div>

            <div class="h-px w-16 bg-amber-300 mx-1 sm:w-24"></div>

            <div class="flex flex-col items-center">
              <div class="h-8 w-8 rounded-full bg-amber-100 border-2 border-amber-400 flex items-center justify-center">
                <span class="h-2 w-2 rounded-full bg-amber-500"></span>
              </div>
              <span class="mt-2 text-xs font-medium text-amber-600">Under review</span>
            </div>

            <div class="h-px w-16 bg-gray-200 mx-1 sm:w-24"></div>

            <div class="flex flex-col items-center">
              <div class="h-8 w-8 rounded-full bg-gray-100 border-2 border-gray-200 flex items-center justify-center">
                <span class="h-2 w-2 rounded-full bg-gray-300"></span>
              </div>
              <span class="mt-2 text-xs font-medium text-gray-400">Activated</span>
            </div>
          </div>
        </div>

        <a
          routerLink="/login"
          class="mt-10 inline-flex items-center gap-1.5 text-sm font-medium text-gray-700 hover:text-gray-900 transition"
        >
          <svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke-width="1.8" stroke="currentColor">
            <path stroke-linecap="round" stroke-linejoin="round" d="M10.5 19.5L3 12m0 0l7.5-7.5M3 12h18" />
          </svg>
          Back to login
        </a>

        <p class="mt-12 text-xs text-gray-400">© 2026 Areena Design OMS. All rights reserved.</p>
      </div>
    </div>
  `
})
export class AccountPendingComponent { }