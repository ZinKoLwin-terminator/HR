import { Component, inject } from '@angular/core';
import { CrudPageComponent } from '../../../shared/components/crud/crud-page.component';
import { CrudPageConfig } from '../../../shared/components/crud/crud.models';
import { Holiday } from '../../../core/models/attendance.models';
import { HolidayDataService } from '../../../core/services/data/attendance-data.services';

const TYPE_OPTIONS = [
  { value: 'Public', label: 'Public' },
  { value: 'Company', label: 'Company' },
];

/** Public and company holidays. They are excluded from leave day counts, working days and payroll. */
@Component({
  selector: 'app-holidays-page',
  imports: [CrudPageComponent],
  template: `<app-crud-page [config]="config" />`,
})
export class HolidaysPageComponent {
  private readonly holidays = inject(HolidayDataService);

  protected readonly config: CrudPageConfig<Holiday> = {
    title: 'Holiday calendar',
    description:
      'Public and company holidays. Holidays affect leave day counts and working days: they are not counted as leave days or payable working days.',
    entityLabel: 'Holiday',
    // Changing the calendar needs attendance:Edit on the server (Create is for the employee's own punches).
    permissionKeys: { Create: 'attendance:Edit' },
    service: this.holidays,
    // A holiday has no status to switch.
    hasStatus: false,
    searchLabel: 'Search by name',
    defaultSort: { active: 'date', direction: 'asc' },
    filters: [{ key: 'type', label: 'Type', options: TYPE_OPTIONS }],
    nameOf: (h) => `${h.name} (${h.date})`,
    columns: [
      { key: 'date', header: 'Date', type: 'date', sortable: true, nowrap: true },
      { key: 'name', header: 'Holiday', sortable: true },
      { key: 'type', header: 'Type', sortable: true },
    ],
    newItem: () => ({ type: 'Public' }),
    fields: [
      {
        key: 'date',
        label: 'Date',
        type: 'date',
        required: true,
        hint: 'One holiday per date',
      },
      { key: 'name', label: 'Holiday name', type: 'text', required: true, maxLength: 100 },
      {
        key: 'type',
        label: 'Type',
        type: 'select',
        required: true,
        options: () => TYPE_OPTIONS,
      },
    ],
  };
}
