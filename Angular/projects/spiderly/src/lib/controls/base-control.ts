import { Component, Input } from '@angular/core';
import { TranslocoService } from '@jsverse/transloco';
import { SpiderlyFormControl } from '../components/spiderly-form-control/spiderly-form-control';

@Component({
  selector: 'base-control',
  template: '',
  styles: [],
  standalone: false,
})
export class BaseControl {
  @Input() control: SpiderlyFormControl<any>; // If you name it formControl: https://stackoverflow.com/a/54755671/21209982
  @Input() disabled: boolean = false;
  /** Whether the field's label is rendered. Defaults to `true`. */
  @Input() showLabel: boolean = true;
  /** Whether the required (*) indicator is shown next to the label. Defaults to `true`. */
  @Input() showRequired: boolean = true;
  @Input() label: string = null; // NgModel/Want custom translation
  @Input() controlValid: boolean = true; // NgModel
  @Input() placeholder: string = '';
  /** Whether the info tooltip icon is shown next to the label. Defaults to `false`. */
  @Input() showTooltip: boolean = false;
  @Input() tooltipText: string = null;
  @Input() tooltipIcon: string = 'pi pi-info-circle';
  constructor(protected translocoService: TranslocoService) {}

  ngOnInit() {
    if (this.control != null && this.disabled == true) this.control.disable();
  }

  ngAfterViewInit() {}

  /**
   * Every control's TEMPLATE renders without a control (`*ngIf="control"`, `control?.errors`,
   * spiderly-file's no-control custom uploads), and this was the one shared read that did not —
   * so an absent control took the whole page down through the label rather than rendering
   * label-less: an unhandled TypeError per change-detection pass, which the global ErrorHandler
   * turns into the generic error toast.
   *
   * That fixes the shared path only. `spiderly-autocomplete`, `spiderly-colorpicker` and
   * `spiderly-checkbox` (with `initializeToFalse`) still dereference `control` in their own
   * `ngOnInit`, so those three are not yet safe to hand an absent control.
   */
  getTranslatedLabel(): string {
    return this.label ?? this.control?.labelForDisplay;
  }

  getValidationErrrorMessages() {
    if (this.control?.errors && this.control?.dirty) {
      // It should always be one error message for single form control,
      // also i don't need to reassign it to null because it will be shown only when control.valid == false
      return this.control.errors['_'];
    }

    return null;
  }
}
