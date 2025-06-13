import { Directive, Input, OnInit, TemplateRef, ViewContainerRef } from "@angular/core";

interface ItemContext<T> {
  $implicit: T;
  item: T;
  index: number;
}

@Directive({
  selector: '[cardBody]',
})
export class MyItemListDirective<T> implements OnInit {
  @Input() items: T[] = [];

  constructor(
    private vcRef: ViewContainerRef,
    private templateRef: TemplateRef<ItemContext<T>>
  ) {}

  ngOnInit() {
    this.vcRef.clear();
    this.items.forEach((item, index) => {
      this.vcRef.createEmbeddedView(this.templateRef, {
        $implicit: item,
        item,
        index
      });
    });
  }
}