import { TranslocoService } from '@jsverse/transloco';
import { Injectable } from "@angular/core";
import { SpiderlyFormArray, SpiderlyFormControl, SpiderlyValidatorFn } from "../components/spiderly-form-control/spiderly-form-control";
import { ValidationErrors } from "@angular/forms";

@Injectable({
    providedIn: 'root',
})
export abstract class ValidatorAbstractService {

    constructor(
        protected translocoService: TranslocoService
    ) {
    }

    abstract setValidator (formControl: SpiderlyFormControl, className: string): SpiderlyValidatorFn;

    isArrayEmpty = (control: SpiderlyFormControl): SpiderlyValidatorFn => {
        const validator: SpiderlyValidatorFn = (): ValidationErrors | null => {
            const value = control.value;
    
            const notEmptyRule = typeof value !== 'undefined' && value !== null && value.length !== 0;
    
            const arrayValid = notEmptyRule;
    
            return arrayValid ? null : { _ : this.translocoService.translate('NotEmpty')};
        };
        validator.hasNotEmptyRule = true;
        control.required = true;
        return validator;
    }

    notEmpty = (control: SpiderlyFormControl): void => {
        const validator: SpiderlyValidatorFn = (): ValidationErrors | null => {
            const value = control.value;
    
            const notEmptyRule = typeof value !== 'undefined' && value !== null && value !== '';
    
            const arrayValid = notEmptyRule;
    
            return arrayValid ? null : { _ : this.translocoService.translate('NotEmpty')};
        };
        validator.hasNotEmptyRule = true;
        control.required = true;
        control.validator = validator;
        control.updateValueAndValidity();
    }
    
    isFormArrayEmpty = (control: SpiderlyFormArray): SpiderlyValidatorFn => {
        const validator: SpiderlyValidatorFn = (): ValidationErrors | null => {
            const value = control;
    
            const notEmptyRule = typeof value !== 'undefined' && value !== null && value.length !== 0;
    
            const arrayValid = notEmptyRule;
    
            return arrayValid ? null : { _ : this.translocoService.translate('NotEmpty')};
        };
        validator.hasNotEmptyRule = true;
        control.required = true;
        return validator;
    }
}
