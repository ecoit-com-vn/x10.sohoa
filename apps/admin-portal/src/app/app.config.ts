import { ApplicationConfig, provideBrowserGlobalErrorListeners } from '@angular/core';
import { provideRouter, withEnabledBlockingInitialNavigation, withHashLocation } from '@angular/router';

import { provideAnimationsAsync } from '@angular/platform-browser/animations/async';
import { provideHttpClient, withInterceptors, withFetch } from '@angular/common/http';
import { providePrimeNG } from 'primeng/config';
import { MessageService, ConfirmationService } from 'primeng/api';
import Aura from '@primeuix/themes/aura';

import { appRoutes } from './app.routes';
import { authInterceptor, authRefreshInterceptor, httpErrorInterceptor, APP_CONFIG } from '@sohoa.frontend/shared/core';
import { environment } from '../environments/environment';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideRouter(appRoutes, withHashLocation(), withEnabledBlockingInitialNavigation()),
    provideAnimationsAsync(),
    provideHttpClient(withInterceptors([authInterceptor, authRefreshInterceptor, httpErrorInterceptor]), withFetch()),
    MessageService,
    ConfirmationService,
    providePrimeNG({
        theme: {
            preset: Aura,
            options: {
                darkModeSelector: '.dark-mode'
            }
        }
    }),
    { provide: APP_CONFIG, useValue: environment }
  ],
};
