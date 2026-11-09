import { Directive, computed, inject } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { Entity } from '../../core/models/common.models';
import { CrudPageConfig } from '../../shared/components/crud/crud.models';
import { OrgLookups, OrganizationLookupService } from './organization-lookups';

/**
 * Base for organization screens: loads the shared lookups, then renders the
 * generic CRUD page from the config built by `buildConfig`.
 */
@Directive()
export abstract class OrganizationPageBase<T extends Entity> {
  private readonly lookupService = inject(OrganizationLookupService);
  protected readonly lookups = rxResource({ stream: () => this.lookupService.load() });
  protected readonly config = computed(() => {
    const data = this.lookups.value();
    return data ? this.buildConfig(data) : null;
  });

  protected abstract buildConfig(lookups: OrgLookups): CrudPageConfig<T>;
}
