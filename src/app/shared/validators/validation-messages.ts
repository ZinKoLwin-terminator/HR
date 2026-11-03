import { ValidationErrors } from '@angular/forms';

type MessageFactory = (label: string, error: unknown) => string;

const MESSAGES: Record<string, MessageFactory> = {
  required: (label) => `${label} is required.`,
  email: () => 'Enter a valid email address.',
  minlength: (label, e) =>
    `${label} must be at least ${(e as { requiredLength: number }).requiredLength} characters.`,
  maxlength: (label, e) =>
    `${label} cannot exceed ${(e as { requiredLength: number }).requiredLength} characters.`,
  min: (label, e) => `${label} must be at least ${(e as { min: number }).min}.`,
  max: (label, e) => `${label} cannot be more than ${(e as { max: number }).max}.`,
  pattern: (label) => `${label} has an invalid format.`,
  phone: () => 'Enter a valid phone number (digits, spaces or dashes, optional +).',
  code: () => 'Use upper-case letters, numbers and dashes only.',
  notBlank: (label) => `${label} cannot be blank.`,
  notInFuture: (label) => `${label} cannot be in the future.`,
  minAge: (_, e) => `Employee must be at least ${(e as { required: number }).required} years old.`,
  number: (label) => `${label} must be a number.`,
  fileType: () => 'Allowed file types: PDF, Word, PNG or JPG.',
  fileSize: () => 'The file is larger than 5 MB.',
  numberBetween: (label, e) => {
    const { min, max } = e as { min: number; max: number };
    return `${label} must be between ${min} and ${max}.`;
  },
  dateRange: () => 'End date must be on or after the start date.',
  timeOrder: () => 'End time must be after the start time, or mark the shift as a night shift.',
  minMax: () => 'Maximum must be greater than or equal to minimum.',
  matDatepickerParse: () => 'Enter a valid date.',
  matDatepickerMin: (label) => `${label} is earlier than allowed.`,
  matDatepickerMax: (label) => `${label} is later than allowed.`,
  server: (_, e) => String(e),
};

/** First human-readable message for a control's errors. */
export function validationMessage(
  errors: ValidationErrors | null | undefined,
  label: string,
): string {
  if (!errors) {
    return '';
  }
  const [key, value] = Object.entries(errors)[0] ?? [];
  if (!key) {
    return '';
  }
  return MESSAGES[key]?.(label, value) ?? `${label} is invalid.`;
}
