import { ApplicationInitStatus } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import {
  provideTransloco,
  Translation,
  TRANSLOCO_LOADER,
  TranslocoLoader,
  TranslocoService,
} from '@jsverse/transloco';
import { Observable, of, throwError } from 'rxjs';

import { SpiderlyReturnButtonComponent } from '../components/spiderly-buttons/return-button/return-button.component';
import { provideSpiderlyTranslations } from './spiderly-scope';

// The app's own loader, as an app writes it: one file per path. Transloco asks it for
// `spiderly/<lang>` as for any scope of the app's own, so the app's Serbian for the library's
// strings is one more file beside its own. The app has no German for the library.
const appFiles: Record<string, Translation> = {
  en: {},
  sr: {},
  de: {},
  'spiderly/sr': { Return: 'Nazad' },
};

class AppLoader implements TranslocoLoader {
  readonly asked: string[] = [];

  getTranslation(path: string): Observable<Translation> {
    this.asked.push(path);
    return path in appFiles ? of(appFiles[path]) : throwError(() => new Error(`No ${path}`));
  }
}

describe('The library scope', () => {
  let appLoader: AppLoader;

  function configure(defaultLang: string) {
    appLoader = new AppLoader();
    TestBed.configureTestingModule({
      imports: [SpiderlyReturnButtonComponent],
      providers: [
        provideRouter([]),
        provideTransloco({
          config: { availableLangs: ['en', 'sr', 'de'], defaultLang, reRenderOnLangChange: true },
        }),
        { provide: TRANSLOCO_LOADER, useValue: appLoader },
        provideSpiderlyTranslations(),
      ],
    });
  }

  const label = (fixture: { nativeElement: HTMLElement }): string =>
    fixture.nativeElement.querySelector('.p-button-label')!.textContent!.trim();

  async function render() {
    const fixture = TestBed.createComponent(SpiderlyReturnButtonComponent);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
    return fixture;
  }

  // Inside the zone, so whenStable waits for the load the switch starts (spec-support.spec.ts).
  async function switchTo(fixture: Awaited<ReturnType<typeof render>>, lang: string) {
    fixture.ngZone!.run(() => TestBed.inject(TranslocoService).setActiveLang(lang));
    await fixture.whenStable();
    fixture.detectChanges();
  }

  it('ships its English inline, so an app with no file for the scope reads it', async () => {
    configure('en');
    const fixture = await render();

    expect(label(fixture)).toBe('Return');
    expect(appLoader.asked).not.toContain('spiderly/en');
  });

  it("reads an app's own file for another language, and a runtime switch switches its strings", async () => {
    configure('en');
    const fixture = await render();

    await switchTo(fixture, 'sr');

    expect(appLoader.asked).toContain('spiderly/sr');
    expect(label(fixture)).toBe('Nazad');
    expect(TestBed.inject(TranslocoService).translate('spiderly.Return')).toBe('Nazad');

    await switchTo(fixture, 'en');

    expect(label(fixture)).toBe('Return');
  });

  it('shows English for a language the app has no file for, never a blank component', async () => {
    configure('en');
    spyOn(console, 'warn');
    const fixture = await render();

    await switchTo(fixture, 'de');

    expect(label(fixture)).toBe('Return');
    expect(console.warn).toHaveBeenCalledWith(jasmine.stringContaining('spiderly/de'));
  });

  // A form's toast text and a menu's labels are translated as their class is built, before any
  // library template has rendered.
  it("loads the app's language before the first page, so a class reads it as it is built", async () => {
    configure('sr');

    await TestBed.inject(ApplicationInitStatus).donePromise;

    expect(TestBed.inject(TranslocoService).translate('spiderly.Return')).toBe('Nazad');
  });
});
