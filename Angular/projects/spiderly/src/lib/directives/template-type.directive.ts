import { Directive, Input } from '@angular/core';

@Directive({
  selector: '[templateType]',
})
export class SpiderlyTemplateTypeDirective<T> {
  @Input() templateType?: T;

  static ngTemplateContextGuard<T>(
    dir: SpiderlyTemplateTypeDirective<T>,
    ctx: unknown
  ): ctx is T {
    return true;
  }
}