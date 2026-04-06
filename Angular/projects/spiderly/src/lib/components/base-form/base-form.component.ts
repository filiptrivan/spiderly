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
import {
  SpiderlyFormArray,
  SpiderlyFormControl,
  SpiderlyFormGroup,
} from '../spiderly-form-control/spiderly-form-control';
import { BaseFormService } from './../../services/base-form.service';

@Component({
  selector: 'base-form',
  template: '',
  styles: [],
  standalone: false,
})
export class BaseFormComponent<
  TMainUIForm extends BaseEntity = any,
  TSaveBody extends BaseEntity = any,
> implements OnInit {
  /**
   * The root form group that holds all form controls, typed to `TSaveBody`.
   * Assign `saveObservableMethod` on it to define the HTTP call used for saving.
   * The form controls are built automatically from the `TSaveBody` schema when you call
   * `baseFormService.initFormGroup(this.parentFormGroup, saveBodyClass, saveBody)`.
   *
   * @example
   * ```ts
   * this.parentFormGroup.saveObservableMethod = this.apiService.saveProduct;
   *
   * this.baseFormService.initFormGroup(
   *   this.parentFormGroup,
   *   ProductSaveBody,
   *   saveBody,
   * );
   * ```
   */
  parentFormGroup = new SpiderlyFormGroup<TSaveBody>({} as any);

  /**
   * The class reference for the main UI form entity (`TMainUIForm`).
   * This represents the shape of the data **returned by the API** after a save.
   * Used internally by `mapMainUIFormToSaveBody` to transform the API response back into `TSaveBody`
   * for form re-initialization.
   *
   * @example
   * ```ts
   * this.mainUIFormClass = ProductMainUIForm;
   * ```
   */
  mainUIFormClass: SchemaAwareConstructor<TMainUIForm>;

  /**
   * The class reference for the save body entity (`TSaveBody`).
   * This represents the shape of the data **sent to the API** when saving.
   * Used internally to build form controls from the schema via `initFormGroup`,
   * and to locate the main DTO property (marked with `isSaveBodyMainDTO: true`) for extracting the saved entity's ID.
   *
   * @example
   * ```ts
   * this.saveBodyClass = ProductSaveBody;
   * ```
   */
  saveBodyClass: SchemaAwareConstructor<TSaveBody>;

  /**
   * The toast message displayed after a successful save.
   * Override this to customize the success notification text for a specific entity.
   * If you want to change the message for all entities, update the `SuccessfulSaveToastDescription` key
   * in your translation JSON file instead.
   *
   * @example
   * ```ts
   * this.successfulSaveToastDescription = 'Product saved successfully!';
   * ```
   */
  successfulSaveToastDescription: string = this.translocoService.translate(
    'SuccessfulSaveToastDescription',
  );

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
  ) {}

  ngOnInit() {}

  //#region Model

  /**
   * Executes the save flow for the form. The execution order is:
   * 1. Builds the save body from the form's raw value.
   * 2. Calls {@link onBeforeSave} — use this to modify the save body before validation.
   * 3. Validates the form. If invalid, shows an error message and stops.
   * 4. Sends the save HTTP request via `saveObservableMethod`.
   * 5. Calls {@link onAfterSaveRequest} — fires immediately after the request is sent, before the response arrives.
   * 6. On successful response: shows a success toast, reroutes, and calls {@link onAfterSave}. The form is re-initialized only when `rerouteToParentSlugAfterSave` is `false`.
   *
   * @param rerouteToParentSlugAfterSave - When `true` (default), navigates to the parent URL after save. When `false`, re-initializes the form and navigates to the saved object's URL.
   *
   * @example
   * ```html
   * <button (click)="onSave()">Save</button>
   * ```
   *
   * @example
   * ```html
   * <!-- Save and stay on the saved object's page -->
   * <button (click)="onSave(false)">Save and stay</button>
   * ```
   */
  // onSave method is here only because of the hooks, we should move everything except them to the BaseFromService
  onSave = (rerouteToParentSlugAfterSave: boolean = true) => {
    if (!this.saveBodyClass)
      throw new SpiderlyError('You did not initialize saveBodyClass');

    if (!this.mainUIFormClass)
      throw new SpiderlyError('You did not initialize mainUIFormClass');

    let saveBody = this.parentFormGroup.getRawValue();

    this.onBeforeSave(saveBody);

    const isValid = this.baseFormService.isControlValid(this.parentFormGroup);

    if (isValid) {
      this.parentFormGroup
        .saveObservableMethod(saveBody)
        .subscribe((res: TMainUIForm) => {
          this.messageService.successMessage(
            this.successfulSaveToastDescription,
          );

          if (rerouteToParentSlugAfterSave) {
            this.rerouteToSavedObject(undefined);
          } else {
            saveBody = this.baseFormService.mapMainUIFormToSaveBody(
              this.mainUIFormClass,
              res,
            );

            this.baseFormService.initFormGroup(
              this.parentFormGroup,
              this.saveBodyClass,
              saveBody,
            );

            const saveBodyMainDTOKey =
              this.baseFormService.getSaveBodyMainDTOKey(this.saveBodyClass);
            const savedObjectId = saveBody[saveBodyMainDTOKey]?.id;
            this.rerouteToSavedObject(savedObjectId); // You always need to have id, because of id == 0 and version change
          }

          this.onAfterSave();
        });

      this.onAfterSaveRequest();
    } else {
      this.baseFormService.showInvalidFieldsMessage();
    }
  };

  /**
   * Handles navigation after a successful save.
   * Override this to customize the post-save navigation behavior.
   * By default, navigates to the parent URL when `rerouteId` is not provided, or to the saved object's URL otherwise.
   *
   * @param rerouteId - The ID of the saved object, used to build the target URL. When not provided, navigates to the parent URL.
   *
   * @example
   * ```ts
   * // Override to navigate to a custom route after save
   * override rerouteToSavedObject(rerouteId: number | string): void {
   *   this.router.navigateByUrl(`/products/${rerouteId}/details`);
   * }
   * ```
   */
  rerouteToSavedObject(rerouteId: number | string): void {
    if (rerouteId == null) {
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

  /**
   * Hook that runs **before** form validation and the save request.
   * Use this to modify the save body or perform any pre-save logic (e.g., transforming data, setting computed fields).
   *
   * @param saveBody - The current save body built from the form's raw value. Mutate it directly to change what gets sent to the server.
   *
   * @example
   * ```ts
   * onBeforeSave = (saveBody?: ProductSaveBody) => {
   *   saveBody.productDTO.fullName = saveBody.productDTO.firstName + ' ' + saveBody.productDTO.lastName;
   * };
   * ```
   */
  onBeforeSave = (saveBody?: TSaveBody) => {};

  /**
   * Hook that runs **after** a successful save response is received.
   * Use this for post-save side effects (e.g., refreshing related data, showing additional notifications).
   *
   * @example
   * ```ts
   * onAfterSave = () => {
   *   this.loadRelatedProducts();
   * };
   * ```
   */
  onAfterSave = () => {};

  /**
   * Hook that runs immediately **after** the save HTTP request is sent, but **before** the response arrives.
   * Use this for side effects that should happen as soon as the request is dispatched (e.g., disabling UI elements, starting a loading indicator).
   *
   * @example
   * ```ts
   * onAfterSaveRequest = () => {
   *   this.isSaving = true;
   * };
   * ```
   */
  onAfterSaveRequest = () => {};

  //#endregion

  //#region Model List

  getFormArrayControlByIndex<T>(
    formControlName: keyof T & string,
    formArray: SpiderlyFormArray<T>,
    index: number,
    filter?: (formGroups: SpiderlyFormGroup<T>[]) => SpiderlyFormGroup<T>[],
  ): SpiderlyFormControl {
    // if(formArray.controlNamesFromHtml.findIndex(x => x === formControlName) === -1)
    //   formArray.controlNamesFromHtml.push(formControlName);

    let filteredFormGroups: SpiderlyFormGroup<T>[];

    if (filter) {
      filteredFormGroups = filter(formArray.controls as SpiderlyFormGroup<T>[]);
    } else {
      return (formArray.controls[index] as SpiderlyFormGroup<T>).controls[
        formControlName
      ] as SpiderlyFormControl;
    }

    return filteredFormGroups[index]?.controls[
      formControlName
    ] as SpiderlyFormControl; // FT: Don't change this. It's always possible that change detection occurs before something.
  }

  getFormArrayControls<T>(
    formControlName: keyof T & string,
    formArray: SpiderlyFormArray<T>,
    filter?: (formGroups: SpiderlyFormGroup<T>[]) => SpiderlyFormGroup<T>[],
  ): SpiderlyFormControl[] {
    // if(formArray.controlNamesFromHtml.findIndex(x => x === formControlName) === -1)
    //   formArray.controlNamesFromHtml.push(formControlName);

    let filteredFormGroups: SpiderlyFormGroup<T>[];

    if (filter) {
      filteredFormGroups = filter(formArray.controls as SpiderlyFormGroup<T>[]);
    } else {
      return (formArray.controls as SpiderlyFormGroup<T>[]).map(
        (x) => x.controls[formControlName] as SpiderlyFormControl,
      );
    }

    return filteredFormGroups.map(
      (x) => x.controls[formControlName] as SpiderlyFormControl,
    );
  }

  removeFormControlsFromTheFormArray(
    formArray: SpiderlyFormArray,
    indexes: number[],
  ) {
    // Sort indexes in descending order to avoid index shifts when removing controls
    const sortedIndexes = indexes.sort((a, b) => b - a);

    sortedIndexes.forEach((index) => {
      if (index >= 0 && index < formArray.length) {
        formArray.removeAt(index);
      }
    });
  }

  //#endregion
}
