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
});
