import { Component, Input, OnInit } from '@angular/core';

@Component({
    selector: 'spiderly-card',
    templateUrl: './spiderly-card.component.html',
    styles: [],
})
export class SpiderlyCardComponent implements OnInit {
  @Input() icon: string = 'pi pi-file-edit';
  @Input() title: string;

  constructor() { }

  ngOnInit(): void {
  }
}