import { ApplicationConfig, provideZoneChangeDetection } from '@angular/core';
import { provideRouter } from '@angular/router';
import { HttpClientModule, provideHttpClient, withFetch } from '@angular/common/http';
import { provideAnimationsAsync } from '@angular/platform-browser/animations/async';
import { importProvidersFrom } from '@angular/core';

import { routes } from './app.routes';
import { BrowserModule } from '@angular/platform-browser';
import { BrowserAnimationsModule } from '@angular/platform-browser/animations';
import { MatGridListModule } from '@angular/material/grid-list';
import { MatCardModule } from '@angular/material/card';
import { MatDialogModule } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';

export const appConfig: ApplicationConfig = {
  providers: [
    provideZoneChangeDetection({ eventCoalescing: true }),
    provideRouter(routes),
    provideHttpClient(withFetch()),
    provideAnimationsAsync(),
    importProvidersFrom(
      HttpClientModule,
      BrowserModule,
      BrowserAnimationsModule,
      MatGridListModule,
      MatCardModule,
      MatDialogModule,
      MatButtonModule
    ), provideAnimationsAsync('noop')
  ]
};
