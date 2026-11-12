import { Component, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { RouterLink } from '@angular/router';
import { PermissionService } from '../../../core/services/permission.service';
import { EmptyStateComponent } from '../empty-state/empty-state.component';

/** Shown when a route needs a permission the user's role does not include. */
@Component({
  selector: 'app-forbidden',
  imports: [EmptyStateComponent, MatButtonModule, MatIconModule, RouterLink],
  template: `
    <app-empty-state
      icon="lock"
      title="You do not have access to this page"
      message="Your role does not include this area. If you need it, ask an HR administrator."
    >
      @if (permissions.can('dashboard:View')) {
        <a mat-flat-button routerLink="/dashboard"><mat-icon>home</mat-icon>Go to dashboard</a>
      } @else {
        <a mat-flat-button routerLink="/help"><mat-icon>help</mat-icon>Open help</a>
      }
    </app-empty-state>
  `,
})
export class ForbiddenComponent {
  protected readonly permissions = inject(PermissionService);
}
