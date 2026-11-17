import { AssignKpiResult, AssignmentItemInput, KpiTemplate } from '../../../core/models/kpi.models';
import {
  KPI_WEIGHT_TOTAL,
  sumKpiWeights,
  validateKpiRule,
} from '../../../core/utilities/kpi-calculation';

/** One KPI chosen in an assign dialog. Target and weight start at the template's and stay editable. */
export interface AssignmentRow {
  templateId: number;
  target: number | null;
  weight: number | null;
}

/** A new row prefilled from its template. */
export function rowFromTemplate(template: KpiTemplate): AssignmentRow {
  return {
    templateId: template.id,
    target: template.defaultTarget,
    weight: template.defaultWeight,
  };
}

/** The request items for the chosen rows (dates and order default on the server). */
export function toItemInputs(rows: readonly AssignmentRow[]): AssignmentItemInput[] {
  return rows.map((row) => ({
    templateId: row.templateId,
    target: row.target,
    weight: row.weight,
  }));
}

/** Total weight of the plan: what the scorecard already holds plus the chosen rows (exact decimals). */
export function assignmentTotal(rows: readonly AssignmentRow[], existingWeight = 0): number {
  return sumKpiWeights([existingWeight, ...rows.map((row) => row.weight ?? 0)]);
}

/** True when the plan would hold more than 100% of weight, which the server refuses. */
export const exceedsTotal = (total: number): boolean => total > KPI_WEIGHT_TOTAL;

/** True when the value has at most `places` decimals (tolerates float noise). */
function hasAtMostDecimals(value: number, places: number): boolean {
  const scaled = value * 10 ** places;
  return Math.abs(scaled - Math.round(scaled)) < 1e-6;
}

/**
 * Client-side check of the chosen rows, with the server's keys (`items[i].target`,
 * `items[i].weight`, `items`) so the same row shows a local or a server message. A blank target or
 * weight is refused here: the server would silently use the template's default instead. The rule
 * check is the shared demo mirror (`validateKpiRule`); the server decides.
 */
export function validateAssignmentRows(
  rows: readonly AssignmentRow[],
  templates: readonly KpiTemplate[],
  existingWeight = 0,
): Record<string, string> {
  const errors: Record<string, string> = {};
  if (rows.length === 0) {
    errors['items'] = 'Choose at least one KPI to assign.';
    return errors;
  }
  rows.forEach((row, i) => {
    const target = row.target;
    if (target === null || !Number.isFinite(target)) {
      errors[`items[${i}].target`] = 'Enter a target.';
    } else if (target < 0) {
      errors[`items[${i}].target`] = 'The target cannot be negative.';
    } else if (!hasAtMostDecimals(target, 4)) {
      errors[`items[${i}].target`] = 'Use at most 4 decimal places.';
    } else {
      const template = templates.find((t) => t.id === row.templateId);
      const rule = template
        ? validateKpiRule({
            measurementType: template.measurementType,
            direction: template.direction,
            scoringMethod: template.scoringMethod,
            target,
            rangeMin: template.rangeMin,
            rangeMax: template.rangeMax,
            maxRating: template.maxRating,
            maxAchievementPct: template.maxAchievementPct,
          })
        : {};
      if (rule['target']) {
        errors[`items[${i}].target`] = rule['target'];
      }
    }
    const weight = row.weight;
    if (weight === null || !Number.isFinite(weight)) {
      errors[`items[${i}].weight`] = 'Enter a weight.';
    } else if (!(weight > 0 && weight <= KPI_WEIGHT_TOTAL)) {
      errors[`items[${i}].weight`] = 'The weight must be greater than 0 and at most 100.';
    } else if (!hasAtMostDecimals(weight, 2)) {
      errors[`items[${i}].weight`] = 'Use at most 2 decimal places.';
    }
  });
  if (exceedsTotal(assignmentTotal(rows, existingWeight))) {
    errors['items'] = 'The weights cannot add up to more than 100%.';
  }
  return errors;
}

/**
 * Whether a template may be offered for an employee of a department. A general template always
 * is; a department-specific one only for its own department (compared by name: the employee lookup
 * carries the department name, not its id; the server checks the id). With no employee chosen
 * yet, only general ones.
 */
export function templateFitsDepartment(
  template: KpiTemplate,
  departmentName: string | null,
  departmentNames: ReadonlyMap<number, string>,
): boolean {
  if (template.departmentId === null) {
    return true;
  }
  return departmentName !== null && departmentNames.get(template.departmentId) === departmentName;
}

/** One error of row `index` and field (keys `items[index].<field>`), or ''. */
export const rowError = (
  errors: Readonly<Record<string, string>>,
  index: number,
  field: string,
): string => errors[`items[${index}].${field}`] ?? '';

/**
 * What to tell the user once an assignment answered: the API answers 201 when it created the
 * scorecard and 200 when it added KPIs to the one already in Planning.
 */
export function assignmentMessage(result: AssignKpiResult): string {
  const card = result.scorecard;
  return result.created
    ? `Scorecard created for ${card.employeeCode} (${card.periodName}) with ${card.itemCount} KPI${card.itemCount === 1 ? '' : 's'}.`
    : `KPIs added to the scorecard of ${card.employeeCode} (${card.periodName}).`;
}
