import { ComponentFixture } from '@angular/core/testing';
import { TranslocoTestingOptions } from '@jsverse/transloco';
import { Observable, of } from 'rxjs';
import { delay } from 'rxjs/operators';

import { PaginatedResult } from '../entities/paginated-result';

// Shared spec bootstrap. Named *.spec.ts so tsconfig.lib's `**/*.spec.ts`
// exclude keeps it out of the published library build while tsconfig.spec
// picks it up; it intentionally contains no tests.

// Every control in this library routes static text through Transloco
// (Angular/CLAUDE.md), so component specs boot it from this one empty-map
// config instead of each carrying its own copy.
export function translocoTesting(): TranslocoTestingOptions {
  return {
    langs: { en: {} },
    translocoConfig: { availableLangs: ['en'], defaultLang: 'en' },
    preloadLangs: true,
  };
}

// delay(0) so a result lands AFTER the change-detection pass that triggered the load, avoiding
// NG0100 from a synchronous lazy-load emission. It also buys the specs their most useful tool:
// a synchronously observable window in which the fetch is still in flight.
export const paginated = (
  data: any[],
  totalRecords: number = data.length,
): Observable<PaginatedResult> =>
  of({ data, totalRecords } as PaginatedResult).pipe(delay(0));

// The settle-and-render ritual. Anything driving a refetch must do it inside
// fixture.ngZone.run(...), or the delay(0) above is scheduled where whenStable will not wait
// for it and the assertion measures a still-pending table.
export async function renderRows(
  fixture: ComponentFixture<unknown>,
): Promise<HTMLElement> {
  await fixture.whenStable();
  fixture.detectChanges();
  return fixture.nativeElement as HTMLElement;
}

export const tbodyText = (fixture: ComponentFixture<unknown>): string =>
  (fixture.nativeElement as HTMLElement).querySelector('tbody')!.textContent!;

// One pin table for one shared declaration: `table-pending-veil()` in styles/layout/_mixins.scss,
// included by both list components. Written per-suite it had already drifted — the `color`
// line, which the mixin's own comment calls out as the fragile one, went unpinned in one copy.
// Colour and background resolve to the mixin's FALLBACKS here, since Karma loads no PrimeNG theme.
export function expectPendingVeil(mask: HTMLElement | null): void {
  expect(mask)
    .withContext('the overlay should be mounted while a fetch is pending')
    .toBeTruthy();

  const styles = getComputedStyle(mask!);
  expect(styles.alignItems)
    .withContext('top-anchored, not centred over a table taller than the viewport')
    .toBe('flex-start');
  expect(styles.paddingTop).withContext('padding-top').toBe('96px');
  expect(styles.backgroundColor)
    .withContext("PrimeNG's modal-strength scrim replaced")
    .not.toContain('0, 0, 0');
  expect(styles.color)
    .withContext('set with the background: SpinnerIcon paints fill="currentColor"')
    .toBe('rgb(51, 65, 85)');
}
