import { Component } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { TranslocoTestingModule } from '@jsverse/transloco';
import { Observable, of } from 'rxjs';
import { delay } from 'rxjs/operators';

import { PaginatedResult } from '../../entities/paginated-result';
import { translocoTesting } from '../../testing/spec-support.spec';
import { SpiderlyDataViewComponent } from './spiderly-data-view.component';

// The data view is a copy-paste fork of the data table and carried the same pending-state
// bug. It has no consumer in the workspace, which is why it had no spec at all — but this
// change is behavioural and lands in both, so shipping it untested here would give one
// component full coverage and the other none.

// delay(0) so the result lands after the initial change-detection pass, avoiding NG0100 from
// a synchronous lazy-load emission — and, usefully, leaving a synchronously observable window
// in which the fetch is still in flight.
const paginated = (
  data: any[],
  totalRecords: number = data.length,
): Observable<PaginatedResult> =>
  of({ data, totalRecords } as PaginatedResult).pipe(delay(0));

@Component({
  imports: [SpiderlyDataViewComponent],
  template: `
    <spiderly-data-view [getPaginatedListObservableMethod]="getList">
      <ng-template #cardBody let-item>
        <span class="card-name">{{ item.name }}</span>
      </ng-template>
    </spiderly-data-view>
  `,
})
class HostWithEmptyResultComponent {
  getList = () => paginated([]);
}

@Component({
  imports: [SpiderlyDataViewComponent],
  template: `
    <spiderly-data-view [getPaginatedListObservableMethod]="getList">
      <ng-template #cardBody let-item>
        <span class="card-name">{{ item.name }}</span>
      </ng-template>
    </spiderly-data-view>
  `,
})
class HostWithChangingResultComponent {
  private page = 0;
  getList = () => paginated([{ id: 1, name: `page-${++this.page}` }], 100);
}

function createFixture<T>(host: new () => T): ComponentFixture<T> {
  TestBed.configureTestingModule({
    imports: [host, TranslocoTestingModule.forRoot(translocoTesting())],
    providers: [provideNoopAnimations()],
  });
  const fixture = TestBed.createComponent(host);
  fixture.detectChanges();
  return fixture;
}

function createWithDataView<T>(host: new () => T): {
  fixture: ComponentFixture<T>;
  dataView: SpiderlyDataViewComponent<any>;
} {
  const fixture = createFixture(host);
  const dataView = fixture.debugElement.query(
    By.directive(SpiderlyDataViewComponent),
  ).componentInstance as SpiderlyDataViewComponent<any>;
  return { fixture, dataView };
}

async function renderRows(fixture: ComponentFixture<unknown>): Promise<void> {
  await fixture.whenStable();
  fixture.detectChanges();
}

const tbodyText = (fixture: ComponentFixture<unknown>): string =>
  (fixture.nativeElement as HTMLElement).querySelector('tbody')!.textContent!;

describe('SpiderlyDataViewComponent — pending state', () => {
  // PrimeNG gates its empty message on `isEmpty() && !loading`, so with the flag never raised
  // on a refetch a view whose last result was empty answers "no records" for the whole of the
  // next request — an answer it does not have yet.
  it('does not claim there are no records while a refetch is in flight', async () => {
    const { fixture, dataView } = createWithDataView(HostWithEmptyResultComponent);
    await renderRows(fixture);

    expect(tbodyText(fixture))
      .withContext('a settled empty result does say so')
      .toContain('NoRecordsFound');

    // Inside the zone, as a real filter commit is — outside it the refetch's delay(0) is
    // scheduled where whenStable will not wait for it.
    fixture.ngZone!.run(() => dataView.table._filter());
    fixture.detectChanges();

    expect(tbodyText(fixture))
      .withContext('mid-flight it must not answer for data it does not have')
      .not.toContain('NoRecordsFound');

    await renderRows(fixture);

    expect(tbodyText(fixture))
      .withContext('and it says so again once the new result lands')
      .toContain('NoRecordsFound');
  });

  it('marks the container busy while a refetch is in flight', async () => {
    const { fixture, dataView } = createWithDataView(HostWithEmptyResultComponent);
    await renderRows(fixture);
    const busy = (): string | null =>
      (fixture.nativeElement as HTMLElement)
        .querySelector('.spiderly-table-container')!
        .getAttribute('aria-busy');

    expect(busy()).withContext('settled').toBe('false');

    fixture.ngZone!.run(() => dataView.table._filter());
    fixture.detectChanges();

    expect(busy()).withContext('in flight').toBe('true');

    await renderRows(fixture);

    expect(busy()).withContext('settled again').toBe('false');
  });

  it('keeps the current cards on screen while reload refetches', async () => {
    const { fixture, dataView } = createWithDataView(
      HostWithChangingResultComponent,
    );
    await renderRows(fixture);

    expect(tbodyText(fixture)).withContext('the loaded page').toContain('page-1');

    fixture.ngZone!.run(() => dataView.reload());
    fixture.detectChanges();

    expect(tbodyText(fixture))
      .withContext('still readable under the veil')
      .toContain('page-1');

    await renderRows(fixture);

    expect(tbodyText(fixture))
      .withContext('the fresh page lands')
      .toContain('page-2');
  });
});

describe('SpiderlyDataViewComponent — pending overlay styling', () => {
  // The ::ng-deep computed-style assert Angular/CLAUDE.md requires for any rule targeting
  // markup this component does not author. Contract itself: the mixin in _mixins.scss.
  const maskPins: [property: string, expected: string][] = [
    ['align-items', 'flex-start'],
    ['padding-top', '96px'],
  ];

  it('keeps the veil rules matching PrimeNG mask markup', () => {
    // Un-awaited on purpose: the fixture returns with the first fetch pending, so the mask
    // is mounted.
    const fixture = createFixture(HostWithEmptyResultComponent);
    const mask = (fixture.nativeElement as HTMLElement).querySelector<HTMLElement>(
      '.p-datatable-mask',
    );

    expect(mask)
      .withContext('the overlay should be mounted while the first fetch is pending')
      .toBeTruthy();
    for (const [property, expected] of maskPins) {
      expect(getComputedStyle(mask!).getPropertyValue(property))
        .withContext(property)
        .toBe(expected);
    }
    expect(getComputedStyle(mask!).backgroundColor)
      .withContext('PrimeNG scrim replaced')
      .not.toContain('0, 0, 0');
  });
});
