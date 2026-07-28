import { LayoutServiceBase } from './app-layout.service.base';

const COLLAPSED_KEY = 'spiderly-layout:menu-desktop-inactive';

// Constructor deps are only stored for later use; the sidebar-state behavior
// under test never touches them.
function createService(): LayoutServiceBase {
  return new LayoutServiceBase(null as any, null as any, null as any);
}

describe('LayoutServiceBase sidebar-state persistence', () => {
  afterEach(() => {
    localStorage.clear();
  });

  it('persists the collapsed state when the menu is toggled on desktop', () => {
    const service = createService();
    spyOn(service, 'isDesktop').and.returnValue(true);

    service.onMenuToggle();

    expect(localStorage.getItem(COLLAPSED_KEY)).toBe('true');
  });

  it('restores a stored collapsed state on construction', () => {
    localStorage.setItem(COLLAPSED_KEY, 'true');

    const service = createService();

    expect(service.state.staticMenuDesktopInactive).toBeTrue();
  });

  it('falls back to the expanded default when the stored value is corrupted', () => {
    localStorage.setItem(COLLAPSED_KEY, 'not-json{');

    const service = createService();

    expect(service.state.staticMenuDesktopInactive).toBeFalse();
  });

  it('does not persist anything when the menu is toggled on mobile', () => {
    const service = createService();
    spyOn(service, 'isDesktop').and.returnValue(false);

    service.onMenuToggle();

    expect(localStorage.getItem(COLLAPSED_KEY)).toBeNull();
  });
});
