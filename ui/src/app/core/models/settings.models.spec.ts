import { MOCK_ROLES, MOCK_ROLE_PERMISSIONS } from '../../mock-data/settings/settings.mock';
import { PERMISSION_MODULES } from './settings.models';

describe('permission modules', () => {
  const find = (key: string) => PERMISSION_MODULES.find((m) => m.key === key);

  it('has unique keys', () => {
    const keys = PERMISSION_MODULES.map((m) => m.key);
    expect(new Set(keys).size).toBe(keys.length);
  });

  it('lists the KPI modules with the API module keys, labels and actions', () => {
    expect(find('kpi-setup')).toEqual({
      key: 'kpi-setup',
      label: 'KPI Setup',
      actions: ['View', 'Create', 'Edit', 'Delete'],
    });
    expect(find('kpi')).toEqual({
      key: 'kpi',
      label: 'Performance',
      actions: ['View', 'Create', 'Approve'],
    });
  });

  it('keeps View first in every module', () => {
    for (const module of PERMISSION_MODULES) {
      expect(module.actions[0], module.key).toBe('View');
    }
  });

  describe('starter KPI grants of the mock roles (the database seed)', () => {
    const grantsOf = (roleName: string) => {
      const role = MOCK_ROLES.find((r) => r.name === roleName)!;
      return MOCK_ROLE_PERMISSIONS.find((p) => p.roleId === role.id)!.grants;
    };

    it('gives HR Administrator everything', () => {
      const grants = grantsOf('HR Administrator');
      expect(grants['kpi-setup']).toEqual(['View', 'Create', 'Edit', 'Delete']);
      expect(grants['kpi']).toEqual(['View', 'Create', 'Approve']);
    });

    it('gives HR Officer KPI setup in full and the performance review rights', () => {
      const grants = grantsOf('HR Officer');
      expect(grants['kpi-setup']).toEqual(['View', 'Create', 'Edit', 'Delete']);
      expect(grants['kpi']).toEqual(['View', 'Create', 'Approve']);
    });

    it('gives Department Manager review rights but no setup', () => {
      const grants = grantsOf('Department Manager');
      expect(grants['kpi']).toEqual(['View', 'Create', 'Approve']);
      expect(grants['kpi-setup']).toBeUndefined();
    });

    it.each(['Payroll Officer', 'Recruiter', 'Employee'])(
      'gives %s View and Create on performance only',
      (roleName) => {
        const grants = grantsOf(roleName);
        expect(grants['kpi']).toEqual(['View', 'Create']);
        expect(grants['kpi-setup']).toBeUndefined();
      },
    );

    it('gives Auditor View on both', () => {
      const grants = grantsOf('Auditor');
      expect(grants['kpi-setup']).toEqual(['View']);
      expect(grants['kpi']).toEqual(['View']);
    });
  });

  it('lists the four grievance modules with their actions', () => {
    expect(find('grievance')?.actions).toEqual(['View', 'Create']);
    expect(find('grievance-investigation')?.actions).toEqual(['View', 'Edit']);
    expect(find('grievance-management')?.actions).toEqual(['View', 'Create', 'Edit', 'Delete']);
    expect(find('grievance-review')?.actions).toEqual(['View', 'Approve']);
  });

  describe('starter grievance grants of the mock roles', () => {
    const grantsOf = (roleName: string) => {
      const role = MOCK_ROLES.find((r) => r.name === roleName)!;
      return MOCK_ROLE_PERMISSIONS.find((p) => p.roleId === role.id)!.grants;
    };

    it.each(['HR Officer', 'Department Manager', 'Payroll Officer', 'Recruiter', 'Employee'])(
      'lets %s report their own grievances',
      (roleName) => {
        expect(grantsOf(roleName)['grievance']).toEqual(['View', 'Create']);
      },
    );

    it('makes HR Officer an investigator only', () => {
      const grants = grantsOf('HR Officer');
      expect(grants['grievance-investigation']).toEqual(['View', 'Edit']);
      expect(grants['grievance-management']).toBeUndefined();
      expect(grants['grievance-review']).toBeUndefined();
    });

    it('gives no case-team module to any role but HR Administrator', () => {
      for (const name of ['Department Manager', 'Payroll Officer', 'Recruiter', 'Employee']) {
        const grants = grantsOf(name);
        expect(grants['grievance-investigation'], name).toBeUndefined();
        expect(grants['grievance-management'], name).toBeUndefined();
        expect(grants['grievance-review'], name).toBeUndefined();
      }
      expect(grantsOf('HR Administrator')['grievance-review']).toEqual(['View', 'Approve']);
    });

    it('gives the read-only Auditor no grievance module (confidential)', () => {
      const grants = grantsOf('Auditor');
      for (const key of [
        'grievance',
        'grievance-investigation',
        'grievance-management',
        'grievance-review',
      ]) {
        expect(grants[key], key).toBeUndefined();
      }
    });
  });
});
