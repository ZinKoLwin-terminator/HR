import { ApplicationConfig, provideBrowserGlobalErrorListeners } from '@angular/core';
import { MAT_FORM_FIELD_DEFAULT_OPTIONS } from '@angular/material/form-field';
import { MAT_ICON_DEFAULT_OPTIONS } from '@angular/material/icon';
import { MAT_PAGINATOR_DEFAULT_OPTIONS } from '@angular/material/paginator';
import { MAT_SELECT_CONFIG } from '@angular/material/select';
import { DATE_PIPE_DEFAULT_OPTIONS } from '@angular/common';
import {
  TitleStrategy,
  provideRouter,
  withComponentInputBinding,
  withInMemoryScrolling,
  withRouterConfig,
} from '@angular/router';
import { routes } from './app.routes';
import { DEFAULT_APP_CONFIG } from './core/configuration/app-config';
import { PageTitleStrategy } from './core/services/page-title.strategy';
import { provideMockDataServices } from './mock-services/provide-mock-data-services';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideRouter(
      routes,
      withComponentInputBinding(),
      // Child routes (e.g. /employees/:id/edit) receive their parent's :id parameter.
      withRouterConfig({ paramsInheritanceStrategy: 'always' }),
      withInMemoryScrolling({ scrollPositionRestoration: 'top' }),
    ),
    { provide: TitleStrategy, useClass: PageTitleStrategy },

    // Data layer: in-memory mock implementations (frontend-only phase).
    provideMockDataServices(),

    { provide: MAT_ICON_DEFAULT_OPTIONS, useValue: { fontSet: 'material-symbols-outlined' } },
    { provide: MAT_FORM_FIELD_DEFAULT_OPTIONS, useValue: { appearance: 'outline' } },
    // Dropdown panels size to their longest option (never narrower than the field).
    {
      provide: MAT_SELECT_CONFIG,
      useValue: { panelWidth: 'auto', overlayPanelClass: 'app-select-panel' },
    },
    { provide: MAT_PAGINATOR_DEFAULT_OPTIONS, useValue: { formFieldAppearance: 'outline' } },
    { provide: DATE_PIPE_DEFAULT_OPTIONS, useValue: { dateFormat: DEFAULT_APP_CONFIG.dateFormat } },
  ],
};
