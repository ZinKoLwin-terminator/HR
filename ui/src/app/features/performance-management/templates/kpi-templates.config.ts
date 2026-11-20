import { DepartmentRef } from '../../../core/models/organization.models';
import {
  KPI_DIRECTIONS,
  KPI_FREQUENCIES,
  KPI_MEASUREMENT_TYPES,
  KPI_SCORING_METHODS,
  KpiCategory,
  KpiDirection,
  KpiMeasurementType,
  KpiScoringMethod,
  KpiTemplate,
} from '../../../core/models/kpi.models';
import { KpiTemplateDataService } from '../../../core/services/data/kpi-data.services';
import { validateKpiRule } from '../../../core/utilities/kpi-calculation';
import {
  CrudPageConfig,
  FieldConfig,
  FieldOption,
  FormValues,
} from '../../../shared/components/crud/crud.models';
import { STATUS_OPTIONS, options } from '../../organization/organization-lookups';
import { textOptions } from '../shared/kpi-options';

export interface KpiTemplateConfigOptions {
  service: KpiTemplateDataService;
  categories: KpiCategory[];
  departments: DepartmentRef[];
}

const asNumber = (value: unknown): number | null =>
  value === null || value === undefined || value === '' ? null : Number(value);

/**
 * The scoring rule typed in the form, checked with the same function (and so the same messages)
 * as the API. In the result, `target` is the form's `defaultTarget`. Nothing is reported until the
 * three choices that make up the rule have been made. The server still decides.
 */
export function templateRuleErrors(values: FormValues): Record<string, string> {
  const measurementType = values['measurementType'] as KpiMeasurementType | null;
  const direction = values['direction'] as KpiDirection | null;
  const scoringMethod = values['scoringMethod'] as KpiScoringMethod | null;
  if (!measurementType || !direction || !scoringMethod) {
    return {};
  }
  return validateKpiRule({
    measurementType,
    direction,
    scoringMethod,
    target: asNumber(values['defaultTarget']) ?? 1,
    rangeMin: asNumber(values['rangeMin']),
    rangeMax: asNumber(values['rangeMax']),
    maxRating: asNumber(values['maxRating']),
    maxAchievementPct: asNumber(values['maxAchievementPct']),
  });
}

/** The rule check of one form field (`target` is reported on the `defaultTarget` field). */
const ruleOf =
  (ruleKey: string) =>
  (values: FormValues): string | null =>
    templateRuleErrors(values)[ruleKey] ?? null;

/** Whether the cap field is offered: not for Direct Percentage, but always for Lower Is Better. */
export const showsCap = (values: FormValues): boolean =>
  values['scoringMethod'] !== 'Direct Percentage' || values['direction'] === 'Lower Is Better';

/** The measurement types a scoring method allows; Rating Based and Boolean fix it. */
export function measurementOptions(method: unknown): FieldOption[] {
  if (method === 'Rating Based') return textOptions(['Rating']);
  if (method === 'Boolean') return textOptions(['Boolean']);
  return textOptions(KPI_MEASUREMENT_TYPES.filter((t) => t !== 'Rating' && t !== 'Boolean'));
}

/** The directions a scoring method allows; Range Based fixes it. */
export function directionOptions(method: unknown): FieldOption[] {
  return method === 'Range Based'
    ? textOptions(['Range Based'])
    : textOptions(KPI_DIRECTIONS.filter((d) => d !== 'Range Based'));
}

/** KPI templates (`kpi-setup`): reusable KPI definitions. A scorecard copies one when it is assigned. */
export function buildKpiTemplatesConfig(o: KpiTemplateConfigOptions): CrudPageConfig<KpiTemplate> {
  const categoryName = new Map(o.categories.map((c) => [c.id, c.name]));
  const departmentName = new Map(o.departments.map((d) => [d.id, d.name]));

  const fields: FieldConfig[] = [
    {
      key: 'code',
      label: 'Code',
      type: 'text',
      required: true,
      pattern: 'code',
      maxLength: 20,
      lockedOnEdit: true,
      hint: 'Capital letters, numbers and dashes, e.g. SALES-TARGET',
    },
    { key: 'name', label: 'KPI name', type: 'text', required: true, maxLength: 150 },
    { key: 'description', label: 'Description', type: 'textarea', maxLength: 500 },
    {
      key: 'categoryId',
      label: 'Category',
      type: 'select',
      required: true,
      // New templates need an active category; an edited one may keep its current (inactive) one.
      options: (v) =>
        options(
          o.categories.filter((c) => c.status === 'Active' || c.id === v['categoryId']),
          (c) => c.name,
        ),
    },
    {
      key: 'departmentId',
      label: 'Department-specific (optional)',
      type: 'select',
      nullable: true,
      options: () => options(o.departments, (d) => d.name),
      hint: 'Leave empty to offer it for every department',
    },
    {
      key: 'scoringMethod',
      label: 'Scoring method',
      type: 'select',
      required: true,
      options: () => textOptions(KPI_SCORING_METHODS),
      rule: ruleOf('scoringMethod'),
    },
    {
      key: 'measurementType',
      label: 'Measurement type',
      type: 'select',
      required: true,
      dependsOn: 'scoringMethod',
      options: (v) => measurementOptions(v['scoringMethod']),
      force: (v) =>
        v['scoringMethod'] === 'Rating Based'
          ? { value: 'Rating' }
          : v['scoringMethod'] === 'Boolean'
            ? { value: 'Boolean' }
            : null,
      hintFor: (v) =>
        v['scoringMethod'] === 'Rating Based'
          ? 'Rating Based scoring always measures a Rating'
          : v['scoringMethod'] === 'Boolean'
            ? 'Boolean scoring always measures a yes / no result'
            : undefined,
    },
    {
      key: 'direction',
      label: 'Direction',
      type: 'select',
      required: true,
      dependsOn: 'scoringMethod',
      options: (v) => directionOptions(v['scoringMethod']),
      force: (v) => (v['scoringMethod'] === 'Range Based' ? { value: 'Range Based' } : null),
      hintFor: (v) =>
        v['scoringMethod'] === 'Range Based'
          ? 'Range Based scoring always uses the Range Based direction'
          : v['direction'] === 'Lower Is Better'
            ? 'A smaller result scores higher'
            : undefined,
    },
    { key: 'unit', label: 'Unit', type: 'text', maxLength: 20, hint: 'e.g. tasks, %, hours' },
    {
      key: 'frequency',
      label: 'Frequency',
      type: 'select',
      required: true,
      options: () => textOptions(KPI_FREQUENCIES),
    },
    {
      key: 'defaultTarget',
      label: 'Default target',
      type: 'number',
      required: true,
      min: 0,
      rule: ruleOf('target'),
      force: (v) => (v['scoringMethod'] === 'Boolean' ? { value: 1 } : null),
      hintFor: (v) =>
        v['scoringMethod'] === 'Boolean'
          ? 'A yes / no KPI has a target of 1 (achieved)'
          : undefined,
    },
    {
      key: 'defaultWeight',
      label: 'Default weight (%)',
      type: 'number',
      required: true,
      min: 0.01,
      max: 100,
      hint: 'Prefilled when assigned; a plan must add up to 100%',
    },
    {
      key: 'maxRating',
      label: 'Maximum rating',
      type: 'number',
      visibleWhen: (v) => v['scoringMethod'] === 'Rating Based',
      rule: ruleOf('maxRating'),
      hint: 'The top of the rating scale, e.g. 5',
    },
    {
      key: 'rangeMin',
      label: 'Range minimum',
      type: 'number',
      visibleWhen: (v) => v['scoringMethod'] === 'Range Based',
      rule: ruleOf('rangeMin'),
      hint: 'A result from here to the maximum scores 100%',
    },
    {
      key: 'rangeMax',
      label: 'Range maximum',
      type: 'number',
      visibleWhen: (v) => v['scoringMethod'] === 'Range Based',
      rule: ruleOf('rangeMax'),
    },
    {
      key: 'maxAchievementPct',
      label: 'Maximum achievement (%)',
      type: 'number',
      min: 1,
      max: 500,
      visibleWhen: showsCap,
      rule: ruleOf('maxAchievementPct'),
      hintFor: (v) =>
        v['direction'] === 'Lower Is Better'
          ? 'Required for Lower Is Better: a result of 0 scores the cap (use Capped Percentage)'
          : v['scoringMethod'] === 'Capped Percentage'
            ? 'Required: the most a result can score, 1 to 500'
            : 'Optional: the most a result can score, 1 to 500',
    },
    {
      key: 'status',
      label: 'Status',
      type: 'select',
      required: true,
      options: () => STATUS_OPTIONS,
    },
  ];

  return {
    title: 'KPI templates',
    description:
      'Reusable KPI definitions. Assigning one copies it to the scorecard, so later edits never change a plan.',
    // Buttons lower-case the label ("Add template"), so it carries no acronym.
    entityLabel: 'Template',
    service: o.service,
    searchLabel: 'Search by code or name',
    defaultSort: { active: 'code', direction: 'asc' },
    filters: [
      { key: 'categoryId', label: 'Category', options: options(o.categories, (c) => c.name) },
      { key: 'departmentId', label: 'Department', options: options(o.departments, (d) => d.name) },
      { key: 'scoringMethod', label: 'Scoring method', options: textOptions(KPI_SCORING_METHODS) },
      { key: 'status', label: 'Status', options: STATUS_OPTIONS },
    ],
    nameOf: (t) => `${t.code} ${t.name}`,
    // Only the API's sort keys are sortable (the HTTP adapter drops any other key).
    columns: [
      { key: 'code', header: 'Code', sortable: true, nowrap: true },
      { key: 'name', header: 'KPI', sortable: true },
      {
        key: 'categoryId',
        header: 'Category',
        hideBelow: 'sm',
        value: (t) => categoryName.get(t.categoryId) ?? `#${t.categoryId}`,
      },
      {
        key: 'departmentId',
        header: 'Department',
        hideBelow: 'lg',
        value: (t) =>
          t.departmentId === null
            ? 'All departments'
            : (departmentName.get(t.departmentId) ?? `#${t.departmentId}`),
      },
      { key: 'direction', header: 'Direction', hideBelow: 'md' },
      { key: 'scoringMethod', header: 'Scoring', hideBelow: 'md', sortable: true },
      {
        key: 'defaultTarget',
        header: 'Target',
        type: 'number',
        align: 'end',
        hideBelow: 'lg',
      },
      {
        key: 'defaultWeight',
        header: 'Weight %',
        type: 'number',
        align: 'end',
        hideBelow: 'lg',
        sortable: true,
      },
      { key: 'status', header: 'Status', type: 'status', sortable: true },
    ],
    newItem: () => ({
      departmentId: null,
      scoringMethod: 'Direct Percentage',
      measurementType: 'Number',
      direction: 'Higher Is Better',
      frequency: 'Quarterly',
      defaultTarget: 100,
      defaultWeight: 10,
      maxRating: null,
      rangeMin: null,
      rangeMax: null,
      maxAchievementPct: null,
      status: 'Active',
    }),
    fields,
  };
}
