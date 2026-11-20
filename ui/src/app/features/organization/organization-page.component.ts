import { Directive, computed, inject } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { Entity } from '../../core/models/common.models';
import { PermissionService } from '../../core/services/permission.service';
import { CrudPageConfig } from '../../shared/components/crud/crud.models';
import { safeValue } from '../../shared/utilities/resource-value';
import { OrgLookups, OrganizationLookupService } from './organization-lookups';

/**
 * Base for organization screens: loads the shared lookups (names only, see
 * OrganizationLookupService), then renders the generic CRUD page from the config built by
 * `buildConfig`.
 */
@Directive()
export abstract class OrganizationPageBase<T extends Entity> {
  private readonly lookupService = inject(OrganizationLookupService);
  private readonly permissions = inject(PermissionService);
  /**
   * Whether the role can read employees (employees:View). Without it the employee list holds at
   * most the user's own employee, so head pickers and head counts are not offered.
   */
  protected readonly canSeeEmployees = computed(() => this.permissions.can('employees:View'));
  protected readonly lookups = rxResource({ stream: () => this.lookupService.load() });
  protected readonly config = computed(() => {
    // load() never fails (each part falls back to empty lists); read safely all the same.
    const data = safeValue(this.lookups);
    return data ? this.buildConfig(data) : null;
  });

  protected abstract buildConfig(lookups: OrgLookups): CrudPageConfig<T>;
}
