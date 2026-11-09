import { Entity } from './common.models';

export type ActivityCategory =
  'employee' | 'organization' | 'attendance' | 'leave' | 'payroll' | 'recruitment' | 'system';

export interface ActivityEntry extends Entity {
  category: ActivityCategory;
  message: string;
  actor: string;
  /** ISO date-time */
  timestamp: string;
}

export interface AppNotification extends Entity {
  title: string;
  message: string;
  icon: string;
  link: string | null;
  timestamp: string;
  read: boolean;
}
