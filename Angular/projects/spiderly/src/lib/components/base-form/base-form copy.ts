import { HttpClient } from '@angular/common/http';
import {
  ChangeDetectorRef,
  Component,
  KeyValueDiffer,
  KeyValueDiffers,
  OnInit,
} from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { TranslocoService } from '@jsverse/transloco';
import { BaseEntity, SchemaAwareConstructor } from '../../entities/base-entity';
import { SpiderlyError } from '../../errors/spiderly-error';
import { getParentUrl } from '../../services/helper-functions';
import { SpiderlyMessageService } from '../../services/spiderly-message.service';
import { SpiderlyFormArray, SpiderlyFormControl, SpiderlyFormGroup } from '../spiderly-form-control/spiderly-form-control';
import { BaseFormService } from './../../services/base-form.service';

@Component({
    selector: 'base-form',
    template: '',
    styles: [],
    standalone: false
})
export class BaseFormCopy<T extends BaseEntity = any> implements OnInit { 
  parentFormGroup = new SpiderlyFormGroup<T>({} as any);
  mainUIFormClass: SchemaAwareConstructor<any>;
  saveBodyClass: SchemaAwareConstructor<any>;
  saveBody: any;
  successfulSaveToastDescription: string = this.translocoService.translate('SuccessfulSaveToastDescription');

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

  // onSave method is here only because of the hooks, we should move everything except them to the BaseFromService
  onSave = (reroute: boolean = true) => {
    if (!this.saveBodyClass)
      throw new SpiderlyError('You did not initialize saveBodyClass');

    if (!this.mainUIFormClass)
      throw new SpiderlyError('You did not initialize mainUIFormClass');

    this.saveBody = this.parentFormGroup.initSaveBody();
    
    this.onBeforeSave(this.saveBody);

    this.saveBody = this.saveBody ?? this.parentFormGroup.getRawValue();

    const isValid = this.baseFormService.isControlValid(this.parentFormGroup);

    if(isValid){
      this.parentFormGroup.saveObservableMethod(this.saveBody).subscribe(res => {
        this.messageService.successMessage(this.successfulSaveToastDescription);

        this.baseFormService.initFormGroup(this.parentFormGroup, this.mainUIFormClass, res);

        if (reroute) {
          const saveBodyMainDTOKey = this.baseFormService.getSaveBodyMainDTOKey(this.saveBodyClass);
          const savedObjectId = res[saveBodyMainDTOKey]?.id;
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

  //#endregion

  //#region Model List
  
  getFormArrayControlByIndex<T>(formControlName: keyof T & string, formArray: SpiderlyFormArray<T>, index: number, filter?: (formGroups: SpiderlyFormGroup<T>[]) => SpiderlyFormGroup<T>[]): SpiderlyFormControl {
    // if(formArray.controlNamesFromHtml.findIndex(x => x === formControlName) === -1)
    //   formArray.controlNamesFromHtml.push(formControlName);

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
    // if(formArray.controlNamesFromHtml.findIndex(x => x === formControlName) === -1)
    //   formArray.controlNamesFromHtml.push(formControlName);

    let filteredFormGroups: SpiderlyFormGroup<T>[];

    if (filter) {
      filteredFormGroups = filter(formArray.controls as SpiderlyFormGroup<T>[]);
    }
    else{
      return (formArray.controls as SpiderlyFormGroup<T>[]).map(x => x.controls[formControlName] as SpiderlyFormControl);
    }

    return filteredFormGroups.map(x => x.controls[formControlName] as SpiderlyFormControl);
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

  //#endregion

}
