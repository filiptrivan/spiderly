import { Type } from '@angular/core';
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

// getTranslatedLabel() is the shared read that used to throw, but each control below renders
// several other `control?.` bindings around it — so these are five templates' tolerance of an
// absent control, not five copies of one assertion. The three controls that dereference `control`
// in their own ngOnInit (autocomplete, colorpicker, checkbox with initializeToFalse) are excluded:
// they are not tolerant yet, see BaseControl.getTranslatedLabel.
const LABEL_BEARING_CONTROLS: Type<BaseControl>[] = [
  SpiderlyTextboxComponent,
  SpiderlyTextareaComponent,
  SpiderlyNumberComponent,
  SpiderlyPasswordComponent,
  SpiderlyCheckboxComponent,
];

describe('BaseControl with no control bound', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [TranslocoTestingModule.forRoot(translocoTesting())],
      providers: [provideNoopAnimations()],
    });
  });

  for (const type of LABEL_BEARING_CONTROLS) {
    it(`${type.name} renders with neither a control nor a label`, () => {
      const fixture = TestBed.createComponent(type);

      expect(() => fixture.detectChanges()).not.toThrow();
    });
  }

  it('prefers an explicit label over the control it does not have', () => {
    const fixture = TestBed.createComponent(SpiderlyTextboxComponent);
    fixture.componentInstance.label = 'Kod';
    fixture.detectChanges();

    expect(fixture.componentInstance.getTranslatedLabel()).toBe('Kod');
  });
});
