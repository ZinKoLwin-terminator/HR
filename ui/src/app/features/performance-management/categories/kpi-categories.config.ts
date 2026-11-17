import { KpiCategory } from '../../../core/models/kpi.models';
import { KpiCategoryDataService } from '../../../core/services/data/kpi-data.services';
import { CrudPageConfig } from '../../../shared/components/crud/crud.models';
import { STATUS_OPTIONS } from '../../organization/organization-lookups';

/** KPI categories (`kpi-setup`): the groups a KPI template belongs to. */
export function buildKpiCategoriesConfig(
  service: KpiCategoryDataService,
): CrudPageConfig<KpiCategory> {
  return {
    title: 'KPI categories',
    description: 'Groups of KPIs, such as Productivity or Quality. A template belongs to one.',
    // Buttons lower-case the label ("Add category"), so it carries no acronym.
    entityLabel: 'Category',
    service,
    searchLabel: 'Search by code or name',
    defaultSort: { active: 'name', direction: 'asc' },
    filters: [{ key: 'status', label: 'Status', options: STATUS_OPTIONS }],
    nameOf: (c) => c.name,
    columns: [
      { key: 'code', header: 'Code', sortable: true, width: '160px' },
      { key: 'name', header: 'Category', sortable: true },
      { key: 'description', header: 'Description', hideBelow: 'md' },
      { key: 'status', header: 'Status', type: 'status', sortable: true },
    ],
    newItem: () => ({ status: 'Active' }),
    fields: [
      {
        key: 'code',
        label: 'Code',
        type: 'text',
        required: true,
        pattern: 'code',
        maxLength: 20,
        lockedOnEdit: true,
        hint: 'Capital letters, numbers and dashes, e.g. QUALITY',
      },
      { key: 'name', label: 'Category name', type: 'text', required: true, maxLength: 100 },
      { key: 'description', label: 'Description', type: 'textarea', maxLength: 300 },
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
