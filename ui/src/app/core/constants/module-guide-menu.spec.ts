import { MODULE_GUIDE } from './module-guide';
import { MODULE_GUIDE_MENU } from './module-guide-menu';

describe('MODULE_GUIDE_MENU', () => {
  it('lists every guide module with the same id, name and icon, in guide order', () => {
    expect(MODULE_GUIDE_MENU).toEqual(
      MODULE_GUIDE.map(({ id, name, icon }) => ({ id, name, icon })),
    );
  });
});
