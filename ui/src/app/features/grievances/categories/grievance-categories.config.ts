import { GrievanceCategory } from '../../../core/models/grievance.models';
import { GrievanceCategoryDataService } from '../../../core/services/data/grievance-data.services';
import { CrudPageConfig } from '../../../shared/components/crud/crud.models';
import { STATUS_OPTIONS } from '../../organization/organization-lookups';

/**
 * Grievance categories (`grievance-management`): what an employee chooses when reporting, whether it
 * may be anonymous, and the default number of days to resolve from acknowledgement. A category used
 * by a case cannot be deleted (the server says so): deactivate it instead.
 */
export function buildGrievanceCategoriesConfig(
  service: GrievanceCategoryDataService,
): CrudPageConfig<GrievanceCategory> {
  return {
    title: 'Grievance categories',
    description:
      'What employees choose when they report a grievance. Deactivate a category instead of deleting it once cases use it.',
    entityLabel: 'Category',
    service,
    searchLabel: 'Search by code or name',
    defaultSort: { active: 'sortOrder', direction: 'asc' },
    filters: [{ key: 'status', label: 'Status', options: STATUS_OPTIONS }],
    nameOf: (c) => c.name,
    columns: [
      { key: 'sortOrder', header: 'Order', sortable: true, type: 'number', width: '80px' },
      { key: 'code', header: 'Code', sortable: true, width: '160px' },
      { key: 'name', header: 'Category', sortable: true },
      {
        key: 'allowsAnonymous',
        header: 'Anonymous',
        value: (c) => (c.allowsAnonymous ? 'Allowed' : 'Named only'),
      },
      {
        key: 'defaultTargetDays',
        header: 'Target days',
        type: 'number',
        align: 'end',
        hideBelow: 'sm',
      },
      { key: 'description', header: 'Description', hideBelow: 'lg' },
      { key: 'status', header: 'Status', type: 'status', sortable: true },
    ],
    newItem: () => ({
      status: 'Active',
      allowsAnonymous: true,
      defaultTargetDays: 30,
      sortOrder: 99,
    }),
    fields: [
      {
        key: 'code',
        label: 'Code',
        type: 'text',
        required: true,
        pattern: 'code',
        maxLength: 20,
        lockedOnEdit: true,
        hint: 'Capital letters, numbers and dashes, e.g. HARASSMENT',
      },
      { key: 'name', label: 'Category name', type: 'text', required: true, maxLength: 100 },
      {
        key: 'description',
        label: 'Description',
        type: 'textarea',
        maxLength: 300,
        fullWidth: true,
      },
      {
        key: 'allowsAnonymous',
        label: 'Anonymous reports allowed',
        type: 'toggle',
        hint: 'Turn off where HR must know who is concerned (for example pay).',
      },
      {
        key: 'defaultTargetDays',
        label: 'Default target days',
        type: 'number',
        required: true,
        min: 1,
        max: 365,
        hint: 'Days from acknowledgement to the default target resolution date.',
      },
      { key: 'sortOrder', label: 'Sort order', type: 'number', required: true, min: 0, max: 9999 },
      {
        key: 'status',
        label: 'Status',
        type: 'select',
        required: true,
        options: () => STATUS_OPTIONS,
      },
    ],
  };
}
