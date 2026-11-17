import {
  EVALUATION_PERIOD_STATUSES,
  EvaluationPeriod,
  EvaluationPeriodStatus,
  KPI_FREQUENCIES,
} from '../../../core/models/kpi.models';
import { Company } from '../../../core/models/organization.models';
import { EvaluationPeriodDataService } from '../../../core/services/data/kpi-data.services';
import {
  CrudExtraAction,
  CrudPageConfig,
  FormValues,
} from '../../../shared/components/crud/crud.models';
import { options } from '../../organization/organization-lookups';
import { textOptions } from '../shared/kpi-options';

export interface EvaluationPeriodConfigOptions {
  service: EvaluationPeriodDataService;
  companies: Company[];
}

/** Completed and Closed periods are read-only. */
const isFinished = (status: unknown): boolean => status === 'Completed' || status === 'Closed';

/** Company, type, dates and the HR review setting: editable only while the period is a Draft. */
const LOCKED_AFTER_DRAFT = (row: FormValues): boolean =>
  row['status'] !== undefined && row['status'] !== 'Draft';

/** Name and due date: editable in Draft and Active, read-only once the period is Completed. */
const LOCKED_WHEN_FINISHED = (row: FormValues): boolean => isFinished(row['status']);

/** The edit rule in words, for the form note. Null when everything is editable. */
export function periodFormNotice(row: FormValues | null): string | null {
  const status = row?.['status'];
  if (status === 'Active') {
    return 'This period is Active: only its name and submission due date can be changed. Company, type, dates and the HR review setting are fixed.';
  }
  if (isFinished(status)) {
    return `This period is ${String(status)}, so it is read-only: nothing in it can be changed.`;
  }
  return null;
}

/** Whether a period can still be edited (Draft: every field; Active: name and due date). */
export const canEditPeriod = (p: EvaluationPeriod): boolean =>
  p.status === 'Draft' || p.status === 'Active';

/** Only a Draft period can be deleted. */
export const canDeletePeriod = (p: EvaluationPeriod): boolean => p.status === 'Draft';

/** Evaluation periods (`kpi-setup`): the windows scorecards belong to. The status moves by command only. */
export function buildEvaluationPeriodsConfig(
  o: EvaluationPeriodConfigOptions,
): CrudPageConfig<EvaluationPeriod> {
  const companyName = new Map(o.companies.map((c) => [c.id, c.name]));

  const command = (
    id: 'activate' | 'complete' | 'close',
    label: string,
    icon: string,
    from: EvaluationPeriodStatus,
    confirm: NonNullable<CrudExtraAction<EvaluationPeriod>['confirm']>,
    done: string,
    destructive = false,
  ): CrudExtraAction<EvaluationPeriod> => ({
    id,
    label,
    icon,
    destructive,
    visible: (p) => p.status === from,
    confirm,
    run: (p) => o.service[id](p.id, p.rowVersion),
    successMessage: (p) => `Period "${p.name}" ${done}.`,
  });

  return {
    title: 'Evaluation periods',
    description:
      'The windows KPIs are planned, measured and reviewed in. A period moves Draft, Active, Completed, Closed.',
    entityLabel: 'Evaluation period',
    service: o.service,
    hasStatus: false,
    searchLabel: 'Search by period name',
    defaultSort: { active: 'startDate', direction: 'desc' },
    filters: [
      { key: 'companyId', label: 'Company', options: options(o.companies, (c) => c.name) },
      { key: 'periodType', label: 'Period type', options: textOptions(KPI_FREQUENCIES) },
      { key: 'status', label: 'Status', options: textOptions(EVALUATION_PERIOD_STATUSES) },
    ],
    nameOf: (p) => p.name,
    canEdit: canEditPeriod,
    canDelete: canDeletePeriod,
    formNotice: periodFormNotice,
    extraActions: [
      command(
        'activate',
        'Activate',
        'play_circle',
        'Draft',
        {
          title: 'Activate this period?',
          message: (p) =>
            `"${p.name}" becomes Active: employees can enter and submit their results. Its company, type, dates and HR review setting can no longer be changed.`,
          confirmText: 'Activate',
        },
        'activated',
      ),
      command(
        'complete',
        'Complete',
        'task_alt',
        'Active',
        {
          title: 'Complete this period?',
          message: (p) =>
            `"${p.name}" becomes Completed: results can no longer be entered or submitted and no KPIs can be assigned. Reviews can still be finished. It cannot be edited any more.`,
          confirmText: 'Complete',
        },
        'completed',
      ),
      command(
        'close',
        'Close',
        'lock',
        'Completed',
        {
          title: 'Close this period?',
          message: (p) =>
            `"${p.name}" becomes final: nothing in it can be changed or reviewed afterwards. This cannot be undone.`,
          confirmText: 'Close period',
        },
        'closed',
        true,
      ),
    ],
    columns: [
      { key: 'name', header: 'Period', sortable: true, nowrap: true },
      {
        key: 'companyId',
        header: 'Company',
        hideBelow: 'md',
        value: (p) => companyName.get(p.companyId) ?? `#${p.companyId}`,
      },
      { key: 'periodType', header: 'Type', hideBelow: 'sm', sortable: true },
      { key: 'startDate', header: 'Start', type: 'date', sortable: true, nowrap: true },
      {
        key: 'endDate',
        header: 'End',
        type: 'date',
        sortable: true,
        nowrap: true,
        hideBelow: 'sm',
      },
      {
        key: 'submissionDueDate',
        header: 'Submission due',
        type: 'date',
        nowrap: true,
        hideBelow: 'lg',
      },
      {
        key: 'requiresHrReview',
        header: 'HR review',
        hideBelow: 'lg',
        value: (p) => (p.requiresHrReview ? 'Required' : 'Not required'),
      },
      { key: 'status', header: 'Status', type: 'status', sortable: true },
    ],
    newItem: () => ({
      periodType: 'Quarterly',
      requiresHrReview: false,
      submissionDueDate: null,
      status: 'Draft',
    }),
    fields: [
      {
        key: 'companyId',
        label: 'Company',
        type: 'select',
        required: true,
        lockedWhen: LOCKED_AFTER_DRAFT,
        hint: 'Fixed once the period is Active',
        options: (v) =>
          options(
            o.companies.filter((c) => c.status === 'Active' || c.id === v['companyId']),
            (c) => c.name,
          ),
      },
      {
        key: 'name',
        label: 'Period name',
        type: 'text',
        required: true,
        maxLength: 100,
        lockedWhen: LOCKED_WHEN_FINISHED,
        hint: 'e.g. 2027 Q1; unique within the company',
      },
      {
        key: 'periodType',
        label: 'Period type',
        type: 'select',
        required: true,
        lockedWhen: LOCKED_AFTER_DRAFT,
        options: () => textOptions(KPI_FREQUENCIES),
        hint: 'Fixed once Active; periods of the same type cannot overlap in a company',
      },
      {
        key: 'startDate',
        label: 'Start date',
        type: 'date',
        required: true,
        lockedWhen: LOCKED_AFTER_DRAFT,
        hint: 'Fixed once the period is Active',
      },
      {
        key: 'endDate',
        label: 'End date',
        type: 'date',
        required: true,
        lockedWhen: LOCKED_AFTER_DRAFT,
        hint: 'After the start date; fixed once the period is Active',
        rule: (v) =>
          v['startDate'] && v['endDate'] && String(v['endDate']) <= String(v['startDate'])
            ? 'The end date must be after the start date.'
            : null,
      },
      {
        key: 'submissionDueDate',
        label: 'Submission due date',
        type: 'date',
        lockedWhen: LOCKED_WHEN_FINISHED,
        hint: 'Optional; a later submission is still accepted',
        rule: (v) =>
          v['startDate'] &&
          v['submissionDueDate'] &&
          String(v['submissionDueDate']) < String(v['startDate'])
            ? 'The due date cannot be before the period starts.'
            : null,
      },
      {
        key: 'requiresHrReview',
        label: 'Requires HR final review',
        type: 'toggle',
        lockedWhen: LOCKED_AFTER_DRAFT,
        hint: 'After the manager approves, HR gives a final review before the scorecard is finalized',
      },
      { key: 'status', label: 'Status', type: 'text', detailsOnly: true },
    ],
  };
}
