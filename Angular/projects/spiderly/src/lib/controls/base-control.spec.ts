import { ErrorHandler, Type } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { TranslocoTestingModule } from '@jsverse/transloco';

import { translocoTesting } from '../testing/spec-support.spec';
import { BaseControl } from './base-control';
import { SpiderlyCheckboxComponent } from './spiderly-checkbox/spiderly-checkbox.component';
import { SpiderlyNumberComponent } from './spiderly-number/spiderly-number.component';
import { SpiderlyPasswordComponent } from './spiderly-password/spiderly-password.component';
import { SpiderlyTextareaComponent } from './spiderly-textarea/spiderly-textarea.component';
import { SpiderlyTextboxComponent } from './spiderly-textbox/spiderly-textbox.component';

// A throw inside a template binding never reaches the spec as a failure — Angular routes it to
// ErrorHandler, which in a Spiderly app is SpiderlyErrorHandler: the generic error toast, and
// (unless the consumer wires an error tracker) the error's only destination. Recording it is the
// only way a spec can assert "the consumer saw no error".
class RecordingErrorHandler implements ErrorHandler {
  errors: unknown[] = [];
  handleError(error: unknown): void {
    this.errors.push(error);
  }
}

// Every control template calls getTranslatedLabel() unconditionally for its label, so a control
// that is briefly absent used to take the whole page down through the base class rather than
// render label-less. Representative set: the plain controls, which need no options/host wiring —
// the defect is in the shared base, not in any one template.
const LABEL_BEARING_CONTROLS: { selector: string; type: Type<BaseControl> }[] = [
  { selector: 'spiderly-textbox', type: SpiderlyTextboxComponent },
  { selector: 'spiderly-textarea', type: SpiderlyTextareaComponent },
  { selector: 'spiderly-number', type: SpiderlyNumberComponent },
  { selector: 'spiderly-password', type: SpiderlyPasswordComponent },
  { selector: 'spiderly-checkbox', type: SpiderlyCheckboxComponent },
];

describe('BaseControl with no control bound', () => {
  let errorHandler: RecordingErrorHandler;

  beforeEach(() => {
    errorHandler = new RecordingErrorHandler();

    TestBed.configureTestingModule({
      imports: [TranslocoTestingModule.forRoot(translocoTesting())],
      providers: [provideNoopAnimations(), { provide: ErrorHandler, useValue: errorHandler }],
    });
  });

  for (const { selector, type } of LABEL_BEARING_CONTROLS) {
    it(`${selector} renders with neither a control nor a label`, () => {
      const fixture = TestBed.createComponent(type);

      expect(() => fixture.detectChanges()).not.toThrow();
      expect(errorHandler.errors).toEqual([]);
    });
  }

  it('prefers an explicit label over the control it does not have', () => {
    const fixture = TestBed.createComponent(SpiderlyTextboxComponent);
    fixture.componentInstance.label = 'Kod';
    fixture.detectChanges();

    expect(fixture.componentInstance.getTranslatedLabel()).toBe('Kod');
    expect(errorHandler.errors).toEqual([]);
  });
});
