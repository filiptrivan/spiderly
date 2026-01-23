import { Component, EventEmitter, Input, Output } from '@angular/core';
import { BaseControl } from './base-control';
import { PrimengOption } from '../entities/primeng-option';
import { TranslocoService } from '@jsverse/transloco';
import { Namebook } from '../entities/namebook';

@Component({
  selector: 'base-dropdown-control',
  template: '',
  styles: [],
  standalone: false,
})
export class BaseDropdownControl extends BaseControl {
  @Input() options: Namebook[];
  @Input() showAddon: boolean = false;
  @Input() addonIcon: string = 'pi pi-ellipsis-h';
  @Input() override placeholder: string =
    this.translocoService.translate('SelectFromTheList');
  @Output() onButtonClick: EventEmitter<null> = new EventEmitter();

  constructor(protected override translocoService: TranslocoService) {
    super(translocoService);
  }

  dropdownMarkAsDirty() {
    this.control.markAsDirty();
  }

  addonClick() {
    this.onButtonClick.next(null);
  }
}
