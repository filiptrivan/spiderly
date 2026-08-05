import { Component } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { TranslocoTestingModule } from '@jsverse/transloco';

import {
  SpiderlyFormControl,
  SpiderlyFormGroup,
} from '../../components/spiderly-form-control/spiderly-form-control';
import { BaseEntity } from '../../entities/base-entity';
import { Namebook } from '../../entities/namebook';
import { BaseFormService } from '../../services/base-form.service';
import { SpiderlyMessageService } from '../../services/spiderly-message.service';
import { ValidatorAbstractService } from '../../services/validator-abstract.service';
import { translocoTesting } from '../../testing/spec-support.spec';
import { SpiderlyMultiSelectComponent } from './spiderly-multiselect.component';

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
    label="Brands"
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
// (BaseFormComponent.onSave -> parentFormGroup.getRawValue()), and a multiselect
// deselect never produces a focus/blur cycle on this control — so the deselect must
// be committed to the form model the moment it happens. Full failure story at the
// updateOn assignment in BaseFormService.initFormGroup.
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

  // fixture.nativeElement is any, so type it once — untyped calls reject generics (TS2347).
  const hostElement = () => fixture.nativeElement as HTMLElement;

  it('removing a chip is visible in getRawValue() with no later focus or blur', () => {
    const removeIcon = hostElement().querySelector<HTMLElement>(
      '.p-chip-remove-icon',
    );
    expect(removeIcon).withContext('chip remove icon must render').toBeTruthy();

    removeIcon.click();
    fixture.detectChanges();

    expect(formGroup.getRawValue().selectedBrandsIds).toEqual([2]);
  });

  it('unchecking an option in the open panel is visible in getRawValue() before any blur', async () => {
    hostElement().querySelector<HTMLElement>('.p-multiselect').click();
    // whenStable + second detectChanges: PrimeNG overlay writes resolve in a
    // microtask (see spiderly-data-table/CLAUDE.md -> "Spec gotcha").
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    // The panel teleports to document.body (appendTo="body"), so query the document.
    const bosch = Array.from(
      document.querySelectorAll<HTMLElement>('.p-multiselect-option'),
    ).find((el) => el.textContent?.includes('Bosch'));
    expect(bosch).withContext('panel option "Bosch" must render').toBeTruthy();

    bosch.click();
    fixture.detectChanges();

    expect(formGroup.getRawValue().selectedBrandsIds).toEqual([2]);
  });
});
