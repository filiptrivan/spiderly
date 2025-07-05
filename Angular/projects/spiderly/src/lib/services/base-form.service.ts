import { TranslocoService } from '@jsverse/transloco';
import { Injectable } from '@angular/core';
import { SpiderlyFormArray, SpiderlyFormControl, SpiderlyFormGroup } from '../components/spiderly-form-control/spiderly-form-control';
import { BaseEntity } from '../entities/base-entity';
import { TranslateLabelsAbstractService } from './translate-labels-abstract.service';
import { ValidatorAbstractService } from './validator-abstract.service';
import { SpiderlyMessageService } from './spiderly-message.service';

@Injectable({
  providedIn: 'root',
})
export class BaseFormService {
  constructor(
    private translateLabelsService: TranslateLabelsAbstractService,
    private validatorService: ValidatorAbstractService,
    private messageService: SpiderlyMessageService,
    private translocoService: TranslocoService
  ) {}

  addFormGroup = <T>(
    formGroup: SpiderlyFormGroup<T>, 
    parentFormGroup: SpiderlyFormGroup, 
    modelConstructor: any, 
    propertyNameInSaveBody: string,
    updateOnChangeControls?: (keyof T)[]
  ) => {
    if (modelConstructor == null)
      return null;

    if (formGroup == null)
      console.error('Spiderly: You need to instantiate the form group.')

    this.initFormGroup(formGroup, modelConstructor, updateOnChangeControls);
    parentFormGroup.setControl(propertyNameInSaveBody, formGroup); // Use setControl because it will update formGroup if it already exists

    return formGroup;
  }

  initFormGroup = <T>(
    formGroup: SpiderlyFormGroup<T>, 
    modelConstructor: T & BaseEntity, 
    updateOnChangeControls?: (keyof T)[]
  ) => {
    if (formGroup == null)
      console.error('Spiderly: You need to instantiate the form group.')

    Object.keys(modelConstructor).forEach((formControlName) => {
      let formControl: SpiderlyFormControl;
      
      const formControlValue = modelConstructor[formControlName];
      
      if (updateOnChangeControls?.includes(formControlName as keyof T) ||
        (formControlName.endsWith('Id') && formControlName.length > 2)
      ){
        formControl = new SpiderlyFormControl(formControlValue, { updateOn: 'change' });
      }
      else{
        formControl = new SpiderlyFormControl(formControlValue, { updateOn: 'blur' });
      }

      formControl.label = formControlName;
      formControl.labelForDisplay = this.getTranslatedLabel(formControlName);
      formControl.parentClassName = modelConstructor.typeName;

      formGroup.setControl(formControlName, formControl); // Use setControl because it will update formControl if it already exists

      this.validatorService.setValidator(formControl, modelConstructor.typeName);
    });

    return formGroup;
  }

  getTranslatedLabel(formControlName: string): string {
    if (formControlName.endsWith('Id') && formControlName.length > 2) {
      formControlName = formControlName.substring(0, formControlName.length - 2);
    } 
    else if (formControlName.endsWith('DisplayName')) {
      formControlName = formControlName.replace('DisplayName', '');
    } 

    return this.translateLabelsService.translate(formControlName);
  }

  getFormArrayGroups<T>(formArray: SpiderlyFormArray<T>): SpiderlyFormGroup<T>[]{
    return formArray.controls as SpiderlyFormGroup<T>[]
  }

  addNewFormGroupToFormArray<T>(
    formArray: SpiderlyFormArray<T>, 
    modelConstructor: T & BaseEntity,
    index: number,
  ) : SpiderlyFormGroup {
    let helperFormGroup = new SpiderlyFormGroup({});
    this.initFormGroup(helperFormGroup, modelConstructor);
    
    if (index == null) {
      formArray.push(helperFormGroup);
    }else{
      formArray.insert(index, helperFormGroup);
    }

    return helperFormGroup;
  }

  initFormArray<T>(
    parentFormGroup: SpiderlyFormGroup, 
    modelList: (T & BaseEntity)[], 
    modelConstructor: T & BaseEntity, 
    formArraySaveBodyName: string, 
    formArrayTranslationKey: string, 
    required: boolean = false)
  {
    if (modelList == null)
      return null;

    let formArray = new SpiderlyFormArray<T>([]);
    formArray.required = required;
    formArray.modelConstructor = modelConstructor;
    formArray.translationKey = formArrayTranslationKey;

    modelList.forEach(model => {
      Object.assign(modelConstructor, model);
      let helperFormGroup: SpiderlyFormGroup = new SpiderlyFormGroup({});
      this.initFormGroup(helperFormGroup, formArray.modelConstructor);
      formArray.push(helperFormGroup);
    });

    parentFormGroup.setControl(formArraySaveBodyName, formArray); // Use setControl because it will update formArray if it already exists

    return formArray;
  }

  disableAllFormControls<T>(formArray: SpiderlyFormArray<T>){
    formArray.controls.forEach((segmentationItemFormGroup: SpiderlyFormGroup) => {
        Object.keys(segmentationItemFormGroup.controls).forEach(key => {
            segmentationItemFormGroup.controls[key].disable();
        });
    });
  }

  enableAllFormControls<T>(formArray: SpiderlyFormArray<T>){
    formArray.controls.forEach((segmentationItemFormGroup: SpiderlyFormGroup) => {
        Object.keys(segmentationItemFormGroup.controls).forEach(key => {
            segmentationItemFormGroup.controls[key].enable();
        });
    });
  }

  //#region Helpers

  // If you want to call single method
  checkFormGroupValidity = <T>(formGroup: SpiderlyFormGroup<T>): boolean => {
    if (formGroup.invalid) {
      Object.keys(formGroup.controls).forEach(key => {
        formGroup.controls[key].markAsDirty(); // this.formGroup.markAsDirty(); // For some reason this doesnt work
      });

      this.showInvalidFieldsMessage();

      return false;
    }
    
    return true;
  }

  showInvalidFieldsMessage = () => {
    this.messageService.warningMessage(
      this.translocoService.translate('YouHaveSomeInvalidFieldsDescription'),
      this.translocoService.translate('YouHaveSomeInvalidFieldsTitle'), 
    );
  }

  generateNewNegativeId<T extends BaseEntity>(formArray: SpiderlyFormArray<T>){
    return -formArray.getRawValue().filter(x => x.id < 0).length - 1;
  }

  //#endregion

}
