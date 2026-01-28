import {
  FormArray,
  FormControl,
  FormControlOptions,
  FormGroup,
  ValidatorFn,
} from '@angular/forms';
import { TranslocoService } from '@jsverse/transloco';
import { MenuItem } from 'primeng/api';
import { Observable } from 'rxjs';
import { SchemaAwareConstructor } from '../../entities/base-entity';
import { Namebook } from '../../entities/namebook';
import { BaseFormService } from '../../services/base-form.service';
import { BaseEntity } from './../../entities/base-entity';

export interface SpiderlyValidatorFn extends ValidatorFn {
  hasNotEmptyRule?: boolean;
}

// It's made like generic type because of <number>, <string> etc. not to put class like User.
export class SpiderlyFormControl<T = any> extends FormControl<T> {
  public label: string;
  public labelForDisplay: string;
  public required: boolean;
  public parentClassName: string;
  public type: string; // number, Date, string, Namebook[], number[]...
  private _spiderlyValidator: SpiderlyValidatorFn | null;

  constructor(
    value: any,
    opts: FormControlOptions = null,
    required: boolean = false,
  ) {
    opts = opts ?? { updateOn: 'blur' };
    super(value, opts);
    this.required = required;
  }

  override getRawValue(): T {
    // Doing this because .value gets only not disabled values
    return super.getRawValue() as T;
  }

  public override get validator(): SpiderlyValidatorFn | null {
    return this._spiderlyValidator;
  }

  public override set validator(validator: SpiderlyValidatorFn | null) {
    this._spiderlyValidator = validator;
    this.setValidators(validator);
  }
}

type SpiderlyControlsOfType<TValue> = {
  [P in keyof TValue]: TValue[P] extends (infer U)[]
    ? U extends Namebook | string | number | Date | boolean
      ? SpiderlyFormControl<TValue[P]>
      : SpiderlyFormArray<U>
    : TValue[P] extends Date
      ? SpiderlyFormControl<TValue[P]>
      : TValue[P] extends object
        ? SpiderlyFormGroup<TValue[P]>
        : SpiderlyFormControl<TValue[P]>;
};

export class SpiderlyFormGroup<TValue = any> extends FormGroup {
  declare controls: SpiderlyControlsOfType<TValue>;

  constructor(controls: SpiderlyControlsOfType<TValue>) {
    super(controls);
  }

  override getRawValue(): TValue {
    // Doing this because .value gets only not disabled values
    return super.getRawValue() as TValue;
  }

  public targetClass: SchemaAwareConstructor<TValue>;
  public trackingId: string = crypto.randomUUID();
  public name?: string; // Using for nested form groups
  public saveObservableMethod?: (saveBody: any) => Observable<any>;
  // TODO: Delete controlNamesFromHtml and add UIDoNotGenerate flag into ng entity generator, we shouldn't even add those into parentFormGroup
  public controlNamesFromHtml?: string[] = [];

  public getControl = <TKey extends Extract<keyof TValue, string>>(
    formControlName: TKey,
  ): SpiderlyControlsOfType<TValue>[TKey] => {
    if (
      this.controlNamesFromHtml.findIndex((x) => x === formControlName) === -1
    )
      this.controlNamesFromHtml.push(formControlName);

    let formControl = this.controls[formControlName];
    if (formControl == null) {
      console.error(
        `Spiderly: The property ${formControlName} in the form group ${this.targetClass.typeName} doesn't exist`,
      );
      return null;
    }

    return formControl;
  };
}

export class SpiderlyFormArray<
  TValue extends BaseEntity = any,
> extends FormArray {
  constructor(
    controls: SpiderlyControlsOfType<TValue>[],
    private translocoService: TranslocoService,
    private baseFormService: BaseFormService,
  ) {
    super(controls);
  }

  public label: string;
  public labelForDisplay: string;
  override value: TValue[]; // There is no getRawValue in FormArray
  public required: boolean;
  public formGroupInitialValues: Partial<TValue> = {};
  public targetClass: SchemaAwareConstructor<TValue>;
  public lastMenuIconIndexClicked: number;

  public getCrudMenuForOrderedData = () => {
    let crudMenuForOrderedData: MenuItem[] = [
      {
        label: this.translocoService.translate('Remove'),
        icon: 'pi pi-minus',
        command: () => {
          this.baseFormService.removeFormControlFromTheFormArray(
            this,
            this.lastMenuIconIndexClicked,
          );
        },
      },
      {
        label: this.translocoService.translate('AddAbove'),
        icon: 'pi pi-arrow-up',
        command: () => {
          this.baseFormService.addNewFormGroupToFormArray(
            this,
            this.targetClass,
            this.formGroupInitialValues,
            this.lastMenuIconIndexClicked,
          );
        },
      },
      {
        label: this.translocoService.translate('AddBelow'),
        icon: 'pi pi-arrow-down',
        command: () => {
          this.baseFormService.addNewFormGroupToFormArray(
            this,
            this.targetClass,
            this.formGroupInitialValues,
            this.lastMenuIconIndexClicked + 1,
          );
        },
      },
    ];

    return crudMenuForOrderedData;
  };

  public addNewFormGroup = (index: number) => {
    this.baseFormService.addNewFormGroupToFormArray(
      this,
      this.targetClass,
      this.formGroupInitialValues,
      index,
    );
  };

  public getFormGroups = () => {
    return this.controls as SpiderlyFormGroup<TValue>[];
  };

  disableAllFormControls() {
    this.controls.forEach((segmentationItemFormGroup: SpiderlyFormGroup) => {
      Object.keys(segmentationItemFormGroup.controls).forEach((key) => {
        segmentationItemFormGroup.controls[key].disable();
      });
    });
  }

  enableAllFormControls() {
    this.controls.forEach((segmentationItemFormGroup: SpiderlyFormGroup) => {
      Object.keys(segmentationItemFormGroup.controls).forEach((key) => {
        segmentationItemFormGroup.controls[key].enable();
      });
    });
  }
}
