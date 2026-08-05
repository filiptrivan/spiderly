import { Component } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import {
  TranslocoTestingModule,
  TranslocoTestingOptions,
} from '@jsverse/transloco';

import {
  SpiderlyFormControl,
  SpiderlyFormGroup,
} from '../../components/spiderly-form-control/spiderly-form-control';
import { BaseEntity } from '../../entities/base-entity';
import { Namebook } from '../../entities/namebook';
import { BaseFormService } from '../../services/base-form.service';
import { SpiderlyMessageService } from '../../services/spiderly-message.service';
import { ValidatorAbstractService } from '../../services/validator-abstract.service';
import { SpiderlyMultiSelectComponent } from './spiderly-multiselect.component';

function translocoTesting(): TranslocoTestingOptions {
  return {
    langs: { en: {} },
    translocoConfig: { availableLangs: ['en'], defaultLang: 'en' },
    preloadLangs: true,
  };
}

// Mirrors a generated SaveBody class (e.g. IntegrationRuleGroupSaveBody.selectedBrandsIds).
// The control under test MUST come from BaseFormService.initFormGroup — the defect these
// specs pin lives in the updateOn mode that factory assigns, so a hand-constructed
// control would exercise the wrong layer.
class TestSaveBody extends BaseEntity {
  selectedBrandsIds?: number[];

  static readonly typeName = 'TestSaveBody' as const;
  static readonly schema = {
    selectedBrandsIds: {
      type: 'number[]',
    },
  } as const;
}

@Component({
  template: `<spiderly-multiselect
    [control]="control"
    [options]="options"
    [label]="'Brands'"
  />`,
  imports: [SpiderlyMultiSelectComponent],
})
class HostComponent {
  control: SpiderlyFormControl<number[]>;
  options: Namebook[] = [
    new Namebook({ id: 1, displayName: 'Bosch' }),
    new Namebook({ id: 2, displayName: 'Makita' }),
  ];
}

// The save flow reads the form model synchronously on the Save click
// (BaseFormComponent.onSave -> parentFormGroup.getRawValue()), and a deselect on this
// control never produces a focus/blur cycle: PrimeNG's chip remove stops propagation
// before onContainerClick can focus the hidden input, so no blur ever fires. The
// deselect therefore has to be committed to the form model at the moment it happens —
// anything deferred to blur silently ships the OLD selection to the backend while the
// UI already shows the item removed (the 2026-08-06 IntegrationRuleGroup regression).
describe('SpiderlyMultiSelectComponent deselect -> form model (save-time contract)', () => {
  let fixture: ComponentFixture<HostComponent>;
  let formGroup: SpiderlyFormGroup<TestSaveBody>;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [HostComponent, TranslocoTestingModule.forRoot(translocoTesting())],
      providers: [
        provideNoopAnimations(),
        {
          provide: ValidatorAbstractService,
          useValue: { setValidator: () => null, setFormArrayValidator: () => null },
        },
        { provide: SpiderlyMessageService, useValue: {} },
      ],
    });

    formGroup = new SpiderlyFormGroup<TestSaveBody>({});
    TestBed.inject(BaseFormService).initFormGroup(formGroup, TestSaveBody, {
      selectedBrandsIds: [1, 2],
    } as TestSaveBody);

    fixture = TestBed.createComponent(HostComponent);
    fixture.componentInstance.control =
      formGroup.getControl('selectedBrandsIds');
    fixture.detectChanges();
  });

  // The overlay teleports to document.body (appendTo="body"); remove leftovers so a
  // later spec can't click a stale panel.
  afterEach(() => {
    document
      .querySelectorAll('.p-overlay, .p-multiselect-overlay')
      .forEach((el) => el.remove());
  });

  it('removing a chip is visible in getRawValue() with no later focus or blur', () => {
    const removeIcon: HTMLElement =
      fixture.nativeElement.querySelector('.p-chip-remove-icon');
    expect(removeIcon).withContext('chip remove icon must render').toBeTruthy();

    removeIcon.click();
    fixture.detectChanges();

    expect(formGroup.getRawValue().selectedBrandsIds).toEqual([2]);
  });

  it('unchecking an option in the open panel is visible in getRawValue() before any blur', async () => {
    (
      fixture.nativeElement.querySelector('.p-multiselect') as HTMLElement
    ).click();
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const bosch = Array.from(
      document.querySelectorAll<HTMLElement>('.p-multiselect-option'),
    ).find((el) => el.textContent?.includes('Bosch'));
    expect(bosch).withContext('panel option "Bosch" must render').toBeTruthy();

    bosch.click();
    fixture.detectChanges();

    expect(formGroup.getRawValue().selectedBrandsIds).toEqual([2]);
  });
});
