import { BaseFormService } from './../../services/base-form.service';
import {
  ChangeDetectorRef,
  Component,
  KeyValueDiffer,
  KeyValueDiffers,
  OnInit,
} from '@angular/core';
import { SpiderlyFormArray, SpiderlyFormControl, SpiderlyFormGroup } from '../spiderly-form-control/spiderly-form-control';
import { HttpClient } from '@angular/common/http';
import { SpiderlyMessageService } from '../../services/spiderly-message.service';
import { ActivatedRoute, Router } from '@angular/router';
import { MenuItem } from 'primeng/api';
import { getControl, getParentUrl } from '../../services/helper-functions';
import { TranslocoService } from '@jsverse/transloco';
import { BaseEntity } from '../../entities/base-entity';
import { LastMenuIconIndexClicked } from '../../entities/last-menu-icon-index-clicked';

@Component({
    selector: 'base-form',
    template: '',
    styles: [],
    standalone: false
})
export class BaseFormCopy implements OnInit { 
  formGroup: SpiderlyFormGroup = new SpiderlyFormGroup({});
  saveBody: any;
  successfulSaveToastDescription: string = this.translocoService.translate('SuccessfulSaveToastDescription');
  loading: boolean = true;

  private modelDiffer: KeyValueDiffer<string, any>;

  constructor(
    protected differs: KeyValueDiffers, 
    protected http: HttpClient, 
    protected messageService: SpiderlyMessageService, 
    protected changeDetectorRef: ChangeDetectorRef,
    protected router: Router, 
    protected route: ActivatedRoute,
    protected translocoService: TranslocoService,
    protected baseFormService: BaseFormService,
  ) {
  }

  ngOnInit(){
  }

  //#region Model

  initFormGroup = <T>(
    formGroup: SpiderlyFormGroup<T>, 
    parentFormGroup: SpiderlyFormGroup, 
    modelConstructor: any, 
    propertyNameInSaveBody: string,
    updateOnChangeControls?: (keyof T)[]
  ) => {
    return this.baseFormService.addFormGroup(
      formGroup, parentFormGroup, modelConstructor, propertyNameInSaveBody, updateOnChangeControls
    );
  }

  createFormGroup = <T>(
    formGroup: SpiderlyFormGroup<T>, 
    modelConstructor: T & BaseEntity, 
    updateOnChangeControls?: (keyof T)[]
  ) => {
    return this.baseFormService.initFormGroup(
      formGroup, modelConstructor, updateOnChangeControls
    );
  }

  control = <T extends BaseEntity>(formControlName: string & keyof T, formGroup: SpiderlyFormGroup<T>) => {
    return getControl(formControlName, formGroup);
  }

  onSave = (reroute: boolean = true) => {
    this.saveBody = this.formGroup.initSaveBody();
    this.onBeforeSave(this.saveBody);

    this.saveBody = this.saveBody ?? this.formGroup.getRawValue();

    let isValid: boolean = this.areFormGroupsFromParentFormGroupValid();
    let isFormArrayValid: boolean = this.areFormArraysValid();

    if(isValid && isFormArrayValid){
      this.formGroup.saveObservableMethod(this.saveBody).subscribe(res => {
        this.messageService.successMessage(this.successfulSaveToastDescription);

        Object.keys(res).forEach((key) => {
          const formControl = this.formGroup.get(key);
          
          if (formControl) {
            if (formControl instanceof SpiderlyFormArray) {
              const formArray = formControl as SpiderlyFormArray;
              if (res[key].length !== 0) {
                formArray.clear();
              }
              else{
                // FT: This is okay because when we have M2M association with additional fields, we will not give back the list because we are not checking version on the server.
                // console.error(`You returned empty array for control: ${formArray.translationKey}.`);
              }

              res[key].forEach((model: any) => {
                if (typeof model === 'object' && model !== null) {
                  Object.assign(formArray.modelConstructor, model);
                  let helperFormGroup = new SpiderlyFormGroup({});
                  this.baseFormService.initFormGroup(helperFormGroup, formArray.modelConstructor)
                  formArray.push(helperFormGroup);
                } else {
                  console.error('Can not add primitive form control inside form array.');
                }
              });

            } else {
              formControl.patchValue(res[key]);
            }
          }else{
            // FT: It's okay to do this.
            // console.error('You returned something that is not in the save DTO.');
          }
        });

        if (reroute) {
          const savedObjectId = (res as any)[this.formGroup.mainDTOName]?.id;
          this.rerouteToSavedObject(savedObjectId); // You always need to have id, because of id == 0 and version change
        }
        
        this.onAfterSave();
      });
      
      this.onAfterSaveRequest();
    }else{
      this.baseFormService.showInvalidFieldsMessage();
    }
  }

  rerouteToSavedObject = (rerouteId: number | string): void => {
    if(rerouteId == null){
      // console.error('You do not have rerouteId in your DTO.')
      const currentUrl = this.router.url;
      const parentUrl: string = getParentUrl(currentUrl);
      this.router.navigateByUrl(parentUrl);
      return;
    }
      
    const segments = this.router.url.split('/');
    segments[segments.length - 1] = rerouteId.toString();

    const newUrl = segments.join('/');
    this.router.navigateByUrl(newUrl);
  }

  onBeforeSave = (saveBody?: any) => {}
  onAfterSave = () => {}
  onAfterSaveRequest = () => {}

  areFormGroupsFromParentFormGroupValid(): boolean {
    if(this.formGroup.controls == null)
      return true;

    let invalid: boolean = false;

    Object.keys(this.formGroup.controls).forEach(key => {
      const formGroupOrControl = this.formGroup.controls[key];

      if (formGroupOrControl instanceof SpiderlyFormGroup){
        Object.keys(formGroupOrControl.controls).forEach(key => {
          const formControl = formGroupOrControl.controls[key] as SpiderlyFormControl; // this.formArray.markAsDirty(); // FT: For some reason this doesnt work

          if (formGroupOrControl.controlNamesFromHtml.includes(formControl.label) && formControl.invalid) {
            formControl.markAsDirty();
            invalid = true;
          }
        });
      }
      else if (formGroupOrControl instanceof SpiderlyFormControl){
        if (formGroupOrControl.invalid) {
          formGroupOrControl.markAsDirty();
          invalid = true;
        }
      }

    });

    if (invalid) {
      return false;
    }

    return true;
  }

  //#endregion

  //#region Model List
  
  getFormArrayControlByIndex<T>(formControlName: keyof T & string, formArray: SpiderlyFormArray<T>, index: number, filter?: (formGroups: SpiderlyFormGroup<T>[]) => SpiderlyFormGroup<T>[]): SpiderlyFormControl {
    if(formArray.controlNamesFromHtml.findIndex(x => x === formControlName) === -1)
      formArray.controlNamesFromHtml.push(formControlName);

    let filteredFormGroups: SpiderlyFormGroup<T>[];

    if (filter) {
      filteredFormGroups = filter(formArray.controls as SpiderlyFormGroup<T>[]);
    }
    else{
      return (formArray.controls[index] as SpiderlyFormGroup<T>).controls[formControlName] as SpiderlyFormControl;
    }

    return filteredFormGroups[index]?.controls[formControlName] as SpiderlyFormControl; // FT: Don't change this. It's always possible that change detection occurs before something.
  }

  getFormArrayControls<T>(formControlName: keyof T & string, formArray: SpiderlyFormArray<T>, filter?: (formGroups: SpiderlyFormGroup<T>[]) => SpiderlyFormGroup<T>[]): SpiderlyFormControl[] {
    if(formArray.controlNamesFromHtml.findIndex(x => x === formControlName) === -1)
      formArray.controlNamesFromHtml.push(formControlName);

    let filteredFormGroups: SpiderlyFormGroup<T>[];

    if (filter) {
      filteredFormGroups = filter(formArray.controls as SpiderlyFormGroup<T>[]);
    }
    else{
      return (formArray.controls as SpiderlyFormGroup<T>[]).map(x => x.controls[formControlName] as SpiderlyFormControl);
    }

    return filteredFormGroups.map(x => x.controls[formControlName] as SpiderlyFormControl);
  }

  getFormArrayGroups<T>(formArray: SpiderlyFormArray<T>): SpiderlyFormGroup<T>[]{
    return this.baseFormService.getFormArrayGroups(formArray);
  }

  addNewFormGroupToFormArray<T>(
    formArray: SpiderlyFormArray<T>, 
    modelConstructor: T & BaseEntity,
    index: number,
  ) : SpiderlyFormGroup {
    return this.baseFormService.addNewFormGroupToFormArray(formArray, modelConstructor, index)
  }

  initFormArray<T>(
    parentFormGroup: SpiderlyFormGroup, 
    modelList: (T & BaseEntity)[], 
    modelConstructor: T & BaseEntity, 
    formArraySaveBodyName: string, 
    formArrayTranslationKey: string, 
    required: boolean = false)
  {
    return this.baseFormService.initFormArray<T>(
      parentFormGroup, modelList, modelConstructor, formArraySaveBodyName, formArrayTranslationKey, required
    );
  }

  removeFormControlFromTheFormArray(formArray: SpiderlyFormArray, index: number) {
    if(index == null)
      throw new Error('Can not pass null index.');

    formArray.removeAt(index);
  }

  removeFormControlsFromTheFormArray(formArray: SpiderlyFormArray, indexes: number[]) {
    // Sort indexes in descending order to avoid index shifts when removing controls
    const sortedIndexes = indexes.sort((a, b) => b - a);

    sortedIndexes.forEach(index => {
      if (index >= 0 && index < formArray.length) {
        formArray.removeAt(index);
      }
    });
  }

  areFormArraysValid(): boolean {
    if(this.formGroup.controls == null)
      return true;

    let invalid: boolean = false;

    Object.keys(this.formGroup.controls).forEach(key => {
      const formArray = this.formGroup.controls[key] as unknown as SpiderlyFormArray;
      
      if (formArray instanceof SpiderlyFormArray){
        (formArray.controls as SpiderlyFormGroup[]).forEach(formGroup => {
          Object.keys(formGroup.controls).forEach(key => {
            const formControl = formGroup.controls[key] as SpiderlyFormControl; // this.formArray.markAsDirty(); // FT: For some reason this doesn't work

            if (
              (formGroup.controlNamesFromHtml.includes(formControl.label) || formArray.controlNamesFromHtml.includes(formControl.label)) && 
              formControl.invalid
            ) {
              formControl.markAsDirty();
              invalid = true;
            }
          });
        });

        if (formArray.required == true && formArray.length == 0) {
          invalid = true;
          this.messageService.warningMessage(this.translocoService.translate('ListCanNotBeEmpty'));
        }
      }
    });

    if (invalid) {
      return false;
    }

    return true;
  }

  onBeforeSaveList(){}
  onAfterSaveList(){}
  onAfterSaveListRequest(){}

  // FT: Sending LastMenuIconIndexClicked class because of reference type
  getCrudMenuForOrderedData = (
    formArray: SpiderlyFormArray, 
    modelConstructor: BaseEntity, 
    lastMenuIconIndexClicked: LastMenuIconIndexClicked, 
    adjustFormArrayManually: boolean = false
  ): MenuItem[] => {
    let crudMenuForOrderedData: MenuItem[] = [
        {label: this.translocoService.translate('Remove'), icon: 'pi pi-minus', command: () => {
          this.onBeforeRemove(formArray, modelConstructor, lastMenuIconIndexClicked.index);
          if (adjustFormArrayManually === false) {
            this.removeFormControlFromTheFormArray(formArray, lastMenuIconIndexClicked.index);
          }
        }},
        {label: this.translocoService.translate('AddAbove'), icon: 'pi pi-arrow-up', command: () => {
          this.onBeforeAddAbove(formArray, modelConstructor, lastMenuIconIndexClicked.index);
          if (adjustFormArrayManually === false) {
            this.baseFormService.addNewFormGroupToFormArray(
              formArray, modelConstructor, lastMenuIconIndexClicked.index
            );
          }
        }},
        {label: this.translocoService.translate('AddBelow'), icon: 'pi pi-arrow-down', command: () => {
          this.onBeforeAddBelow(formArray, modelConstructor, lastMenuIconIndexClicked.index);
          if (adjustFormArrayManually === false) {
            this.baseFormService.addNewFormGroupToFormArray(
              formArray, modelConstructor, lastMenuIconIndexClicked.index + 1
            );
          }
        }},
    ];

    return crudMenuForOrderedData;
  }

  onBeforeRemove = (formArray: SpiderlyFormArray, modelConstructor: any, lastMenuIconIndexClicked: number) => {}

  onBeforeAddAbove = (formArray: SpiderlyFormArray, modelConstructor: any, lastMenuIconIndexClicked: number) => {}

  onBeforeAddBelow = (formArray: SpiderlyFormArray, modelConstructor: any, lastMenuIconIndexClicked: number) => {}

  //#endregion

}
