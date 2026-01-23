import { TranslocoService } from '@jsverse/transloco';
import { Injectable } from '@angular/core';
import {
  SpiderlyFormArray,
  SpiderlyFormControl,
  SpiderlyValidatorFn,
} from '../components/spiderly-form-control/spiderly-form-control';
import { ValidationErrors } from '@angular/forms';
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

  isFormArrayEmpty = (control: SpiderlyFormArray): SpiderlyValidatorFn => {
    const validator: SpiderlyValidatorFn = (): ValidationErrors | null => {
      const value = control;

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
