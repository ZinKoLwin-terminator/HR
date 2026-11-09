import { Directive, TemplateRef, inject, input } from '@angular/core';

export interface CellTemplateContext<T> {
  $implicit: T;
}

/**
 * Custom cell renderer for a data-table column:
 *   <ng-template appCellTemplate="fullName" let-row>…</ng-template>
 */
@Directive({ selector: 'ng-template[appCellTemplate]' })
export class CellTemplateDirective<T = unknown> {
  readonly columnKey = input.required<string>({ alias: 'appCellTemplate' });
  readonly template = inject<TemplateRef<CellTemplateContext<T>>>(TemplateRef);
}
