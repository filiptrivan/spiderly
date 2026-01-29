import { Injectable } from '@angular/core';
import { ValidationErrors } from '@angular/forms';
import { TranslocoService } from '@jsverse/transloco';
import {
  SpiderlyFormArray,
  SpiderlyFormControl,
  SpiderlyValidatorFn,
} from '../components/spiderly-form-control/spiderly-form-control';
import { ImageDimensionsValidationResult } from '../entities/image-dimensions-validation-result';

@Injectable({
  providedIn: 'root',
})
export abstract class ValidatorAbstractService {
  constructor(protected translocoService: TranslocoService) {}

  abstract setValidator(
    formControl: SpiderlyFormControl,
    className: string,
  ): SpiderlyValidatorFn;

  abstract setFormArrayValidator(
    formArray: SpiderlyFormArray,
    className: string,
  ): void;

  validateImageDimensions(
    file: File,
    imageWidth: number,
    imageHeight: number,
  ): Promise<ImageDimensionsValidationResult> {
    return new Promise((resolve) => {
      const img = new Image();
      const objectUrl = URL.createObjectURL(file);

      img.onload = () => {
        URL.revokeObjectURL(objectUrl);
        const width = img.width;
        const height = img.height;
        const errors: string[] = [];

        if (imageWidth > 0 && width !== imageWidth) {
          errors.push(
            this.translocoService.translate('ImageWidthMustBeExact', {
              0: imageWidth,
              1: width,
            }),
          );
        }

        if (imageHeight > 0 && height !== imageHeight) {
          errors.push(
            this.translocoService.translate('ImageHeightMustBeExact', {
              0: imageHeight,
              1: height,
            }),
          );
        }

        resolve({ isValid: errors.length === 0, errors });
      };

      img.onerror = () => {
        URL.revokeObjectURL(objectUrl);
        resolve({ isValid: true, errors: [] });
      };

      img.src = objectUrl;
    });
  }

  notEmpty = (control: SpiderlyFormControl): void => {
    const validator: SpiderlyValidatorFn = (): ValidationErrors | null => {
      const value = control.value;

      const notEmptyRule =
        typeof value !== 'undefined' && value !== null && value !== '';

      const arrayValid = notEmptyRule;

      return arrayValid
        ? null
        : { _: this.translocoService.translate('NotEmpty') };
    };
    validator.hasNotEmptyRule = true;
    control.required = true;
    control.validator = validator;
    control.updateValueAndValidity();
  };

  /** Validates that a SpiderlyFormArray (collection of form controls/groups) is not empty. */
  isFormArrayEmpty = (control: SpiderlyFormArray): void => {
    const validator: SpiderlyValidatorFn = (): ValidationErrors | null => {
      const value = control;

      const notEmptyRule =
        typeof value !== 'undefined' && value !== null && value.length !== 0;

      const arrayValid = notEmptyRule;

      return arrayValid
        ? null
        : {
            _: this.translocoService.translate('ListCanNotBeEmpty', {
              value: control.labelForDisplay,
            }),
          };
    };
    validator.hasNotEmptyRule = true;
    control.required = true;
    control.setValidators(validator);
    control.updateValueAndValidity();
  };

  /** Validates that a SpiderlyFormControl holding an array value (e.g., multi-select dropdown) is not empty. */
  isArrayEmpty = (control: SpiderlyFormControl): SpiderlyValidatorFn => {
    const validator: SpiderlyValidatorFn = (): ValidationErrors | null => {
      const value = control.value;

      const notEmptyRule =
        typeof value !== 'undefined' && value !== null && value.length !== 0;

      const arrayValid = notEmptyRule;

      return arrayValid
        ? null
        : { _: this.translocoService.translate('NotEmpty') };
    };
    validator.hasNotEmptyRule = true;
    control.required = true;
    return validator;
  };
}
