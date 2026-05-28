import {
  Component,
  EventEmitter,
  Input,
  OnInit,
  Output,
  ViewChild,
} from '@angular/core';
import { MenuItem } from 'primeng/api';
import { Menu } from 'primeng/menu';

@Component({
  selector: 'spiderly-panel',
  templateUrl: './spiderly-panel.component.html',
  styleUrl: './spiderly-panel.component.scss',
  standalone: false,
})
export class SpiderlyPanelComponent implements OnInit {
  @Input() isFirstMultiplePanel: boolean = false;
  @Input() isMiddleMultiplePanel: boolean = false;
  @Input() isLastMultiplePanel: boolean = false;
  @Input() toggleable: boolean = false;
  @Input() toggler: 'header' | 'icon' | null = 'icon';
  @Input() collapsed: boolean = false;
  @Input() crudMenu: MenuItem[];
  /** Whether the CRUD context-menu icon is shown. Defaults to `true`. */
  @Input() showCrudMenu: boolean = true;
  /** Whether a remove/delete icon is shown. Defaults to `false`. */
  @Input() showRemoveIcon: boolean = false;
  @Input() index: number;
  /** Whether the panel header is rendered. Defaults to `true`. */
  @Input() showPanelHeader: boolean = true;

  @Output() onMenuIconClick: EventEmitter<number> = new EventEmitter();
  @Output() onRemoveIconClick: EventEmitter<null> = new EventEmitter();

  @ViewChild('menu') menu: Menu;

  constructor() {}

  ngOnInit(): void {}

  menuItemClick(index: number, event) {
    this.menu.toggle(event);
    this.onMenuIconClick.next(index);
  }

  removeItemClick() {
    this.onRemoveIconClick.next(null);
  }
}
