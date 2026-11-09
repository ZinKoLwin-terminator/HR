import { Component } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { RouterLink } from '@angular/router';
import { EmptyStateComponent } from '../empty-state/empty-state.component';

@Component({
  selector: 'app-not-found',
  imports: [EmptyStateComponent, MatButtonModule, MatIconModule, RouterLink],
  template: `
    <app-empty-state
      icon="travel_explore"
      title="Page not found"
      message="The page you are looking for does not exist or has been moved."
    >
      <a mat-flat-button routerLink="/dashboard"><mat-icon>home</mat-icon>Go to dashboard</a>
    </app-empty-state>
  `,
})
export class NotFoundComponent {}
