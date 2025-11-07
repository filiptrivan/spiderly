import { Injectable } from '@angular/core';
import { TranslocoService } from '@jsverse/transloco';
import { SpiderlyFormArray, SpiderlyFormControl, SpiderlyFormGroup } from '../components/spiderly-form-control/spiderly-form-control';
import { BaseEntity, SchemaAwareConstructor } from '../entities/base-entity';
import { SpiderlyError } from '../errors/spiderly-error';
import { capitalizeFirstChar } from './helper-functions';
import { SpiderlyMessageService } from './spiderly-message.service';
import { TranslateLabelsAbstractService } from './translate-labels-abstract.service';
import { ValidatorAbstractService } from './validator-abstract.service';

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

  initFormGroup = <T extends BaseEntity>(
    formGroup: SpiderlyFormGroup<T>, 
    targetClass: SchemaAwareConstructor<T>,
    initialValues?: T, 
    updateOnChangeControls?: (keyof T)[]
  ) => {
    if (!formGroup)
      throw new SpiderlyError('You need to instantiate the form group.')

    if (!targetClass)
      throw new SpiderlyError('You need to pass targetClass.')

    if (!initialValues)
      initialValues = {} as T;

    Object.keys(targetClass.schema).forEach((formControlName) => {
      let control: SpiderlyFormControl | SpiderlyFormGroup<any> | SpiderlyFormArray<any>;
      
      const propSchema = targetClass.schema[formControlName];
      const propInitialValue = initialValues[formControlName];
      
      if (
        propSchema.type.endsWith('[]') &&
        propSchema.nestedConstructor &&
        propSchema.type !== 'Namebook[]'
      ) {
        const formArray = this.initFormArray(propSchema.nestedConstructor, propInitialValue)
        control = formArray;

        control.label = formControlName;
        control.labelForDisplay = this.getTranslatedLabel(formControlName);
      }
      else if (
        propSchema.nestedConstructor &&
        propSchema.type !== 'Namebook[]'
      ) {
        control = new SpiderlyFormGroup({});
        this.initFormGroup(control, propSchema.nestedConstructor, propInitialValue)
      }
      else{
        if (updateOnChangeControls?.includes(formControlName as keyof T) ||
          (formControlName.endsWith('Id') && formControlName.length > 2) ||
          propSchema.type === 'Date' ||
          propSchema.type === 'Namebook[]'
        ){
          control = new SpiderlyFormControl(propInitialValue, { updateOn: 'change' });
        }
        else{
          control = new SpiderlyFormControl(propInitialValue, { updateOn: 'blur' });
        }
  
        control.label = formControlName;
        control.labelForDisplay = this.getTranslatedLabel(formControlName);
        control.parentClassName = targetClass.typeName;

        this.validatorService.setValidator(control, targetClass.typeName);
      }

      formGroup.setControl(formControlName, control); // Use setControl because it will update formControl if it already exists
    });

    formGroup.targetClass = targetClass;

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

  addNewFormGroupToFormArray<T extends BaseEntity>(
    formArray: SpiderlyFormArray<T>, 
    targetClass: SchemaAwareConstructor<T>,
    initialValues: T,
    index: number,
  ) : SpiderlyFormGroup {
    let helperFormGroup = new SpiderlyFormGroup({});
    this.initFormGroup(helperFormGroup, targetClass, initialValues);
    
    if (index == null) {
      formArray.push(helperFormGroup);
    }else{
      formArray.insert(index, helperFormGroup);
    }

    return helperFormGroup;
  }

  removeFormControlFromTheFormArray(formArray: SpiderlyFormArray, index: number) {
    if(index == null)
      throw new SpiderlyError('Can not pass null index.');

    formArray.removeAt(index);
  }

  initFormArray<T extends BaseEntity>(
    targetClass: SchemaAwareConstructor<T>,
    initialValues: T[],
  )
  {
    if (!targetClass)
      throw new SpiderlyError('You did not initialize targetClass');  
    
    if (!initialValues)
      initialValues = [];

    let formArray = new SpiderlyFormArray<T>([], this.translocoService, this);

    // formArray.required = required;
    formArray.formGroupInitialValues = {}; // When we need we can pass formGroupInitialValues to this method instead of assigning it to empty object
    formArray.targetClass = targetClass;

    initialValues.forEach(model => {
      let helperFormGroup: SpiderlyFormGroup = new SpiderlyFormGroup({});
      this.initFormGroup(helperFormGroup, targetClass, model);
      formArray.push(helperFormGroup);
    });

    return formArray;
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

  getSaveBodyMainDTOKey = (saveBodyClass: SchemaAwareConstructor<any>) => {
    const schema = saveBodyClass.schema;
    return Object.keys(schema).find(k => schema[k].isSaveBodyMainDTO === true);
  }

  mapMainUIFormToSaveBody = <T extends BaseEntity>(mainUIFormClass: SchemaAwareConstructor<T>, mainUIFormValues: T) => {
    let saveBody = {};

    Object.keys(mainUIFormClass.schema).forEach(propName => {
      const property = mainUIFormClass.schema[propName];
      const value = mainUIFormValues[propName];
      
      // Handle ordered one-to-many (e.g., "orderedItemsMainUIFormDTO" -> "orderedItemsSaveBodyDTO")
      if (propName.startsWith('Ordered') && propName.endsWith('MainUIFormDTO')) {
        const newKey = propName.replace('MainUIFormDTO', 'SaveBodyDTO');
        // Recursively map nested DTOs
        const relatedEntity = property.nestedConstructor;
        saveBody[newKey] = value?.map(item => 
          this.mapMainUIFormToSaveBody(relatedEntity, item)
        ) ?? [];
      }
      // Handle multi-select (e.g., "itemsIds" -> "selectedItemsIds")
      else if (propName.endsWith('Ids')) {
        saveBody[`selected${capitalizeFirstChar(propName)}`] = value ?? [];
      }
      // Handle multi-autocomplete (e.g., "itemsNamebookDTOList" -> "selectedItemsIds")
      else if (propName.endsWith('NamebookDTOList')) {
        const propertyName = propName.replace('NamebookDTOList', '');
        // Extract IDs from NamebookDTO objects
        const ids = value?.map(item => item.id) ?? [];
        saveBody[`selected${capitalizeFirstChar(propertyName)}Ids`] = ids;
      }
      // Handle the main DTO object (e.g., "entityDTO")
      else{
        saveBody[propName] = value;
      }
    });

    return saveBody;
  }

  //#endregion

}
