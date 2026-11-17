import { RatingBand } from '../../../core/models/kpi.models';
import { RatingBandDataService } from '../../../core/services/data/kpi-data.services';
import { CrudPageConfig } from '../../../shared/components/crud/crud.models';
import { STATUS_OPTIONS } from '../../organization/organization-lookups';

/** Rating bands (`kpi-setup`): the label an overall score earns, from its minimum score up. */
export function buildRatingBandsConfig(service: RatingBandDataService): CrudPageConfig<RatingBand> {
  return {
    title: 'Rating bands',
    description:
      'The label a scorecard earns: the band with the highest minimum score that the overall score reaches.',
    entityLabel: 'Rating band',
    service,
    searchLabel: 'Search by label',
    defaultSort: { active: 'minScore', direction: 'desc' },
    filters: [{ key: 'status', label: 'Status', options: STATUS_OPTIONS }],
    nameOf: (b) => b.label,
    columns: [
      { key: 'minScore', header: 'Minimum score', type: 'number', align: 'end', sortable: true },
      { key: 'label', header: 'Label', sortable: true },
      { key: 'description', header: 'Description', hideBelow: 'md' },
      { key: 'status', header: 'Status', type: 'status', sortable: true },
    ],
    newItem: () => ({ status: 'Active' }),
    fields: [
      { key: 'label', label: 'Label', type: 'text', required: true, maxLength: 60 },
      {
        key: 'minScore',
        label: 'Minimum score',
        type: 'number',
        required: true,
        min: 0,
        max: 500,
        hint: 'From 0 to 500; no two bands share a minimum score',
      },
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
