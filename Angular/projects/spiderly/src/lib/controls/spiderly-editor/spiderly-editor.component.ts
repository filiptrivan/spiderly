import { Component, Input, OnInit, ViewChild } from '@angular/core';
import { BaseControl } from '../base-control';
import { ReactiveFormsModule, FormsModule } from '@angular/forms';
import { RequiredComponent } from '../../components/required/required.component';
import { CommonModule } from '@angular/common';
import { TranslocoService } from '@jsverse/transloco';
import { Editor, EditorModule, EditorInitEvent } from 'primeng/editor';
import { Tooltip, TooltipModule } from 'primeng/tooltip';
import { Observable } from 'rxjs';

@Component({
  selector: 'spiderly-editor',
  templateUrl: './spiderly-editor.component.html',
  styles: [],
  imports: [
    CommonModule,
    ReactiveFormsModule,
    FormsModule,
    EditorModule,
    TooltipModule,
    RequiredComponent,
  ],
})
export class SpiderlyEditorComponent extends BaseControl implements OnInit {
  @ViewChild(Editor) editor: Editor;
  @ViewChild(Tooltip) tooltip: Tooltip;

  @Input() uploadImageMethod: (formData: FormData) => Observable<string>;
  @Input() objectId: number = 0;

  constructor(protected override translocoService: TranslocoService) {
    super(translocoService);
  }

  override ngOnInit() {
    super.ngOnInit();
  }

  onEditorInit(event: EditorInitEvent) {
    const quill = event.editor;

    if (this.uploadImageMethod) {
      const toolbar = quill.getModule('toolbar');
      toolbar.addHandler('image', () => this.imageHandler(quill));
    }
  }

  private imageHandler(quill: any) {
    const input = document.createElement('input');
    input.setAttribute('type', 'file');
    input.setAttribute('accept', 'image/*');
    input.click();

    input.onchange = async () => {
      const file = input.files[0];
      if (file) {
        const formData = new FormData();
        formData.append('file', file, `${this.objectId}-${file.name}`);

        this.uploadImageMethod(formData).subscribe((imageUrl: string) => {
          const range = quill.getSelection(true);
          quill.insertEmbed(range.index, 'image', imageUrl);
          quill.setSelection(range.index + 1);
        });
      }
    };
  }

  onClick() {
    let editableArea: HTMLElement =
      this.editor.el.nativeElement.querySelector('.ql-editor');

    editableArea.onblur = () => {
      this.control.markAsDirty();
      this.tooltip.deactivate();
    };

    editableArea.onfocus = () => {
      if (this.errorMessageTooltipEvent == 'focus') {
        this.tooltip.activate();
      }
    };

    editableArea.onmouseover = () => {
      if (this.errorMessageTooltipEvent == 'hover') {
        this.tooltip.activate();
      }
    };
  }
}
