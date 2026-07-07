import { Component, Input, OnInit, ViewChild } from '@angular/core';
import { BaseControl } from '../base-control';
import { ReactiveFormsModule, FormsModule } from '@angular/forms';
import { RequiredComponent } from '../../components/required/required.component';
import { CommonModule } from '@angular/common';
import { TranslocoService } from '@jsverse/transloco';
import { Editor, EditorModule, EditorInitEvent } from 'primeng/editor';
import { Observable } from 'rxjs';
import { EditorImageUploadResult } from '../../entities/editor-image-upload-result';

@Component({
  selector: 'spiderly-editor',
  templateUrl: './spiderly-editor.component.html',
  styles: [],
  imports: [
    CommonModule,
    ReactiveFormsModule,
    FormsModule,
    EditorModule,
    RequiredComponent,
  ],
})
export class SpiderlyEditorComponent extends BaseControl implements OnInit {
  @ViewChild(Editor) editor: Editor;

  @Input() uploadImageMethod: (formData: FormData) => Observable<EditorImageUploadResult>;
  @Input() objectId: number = 0;
  /** Mirrors the entity property's [AcceptedFileTypes] so the picker matches what the server validates. */
  @Input() acceptedFileTypes: string[];

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
    input.setAttribute('accept', this.acceptedFileTypes?.join(',') ?? 'image/*');
    input.click();

    input.onchange = async () => {
      const file = input.files[0];
      if (file) {
        const formData = new FormData();
        formData.append('file', file, `${this.objectId}-${file.name}`);

        this.uploadImageMethod(formData).subscribe((result: EditorImageUploadResult) => {
          const range = quill.getSelection(true);
          quill.insertEmbed(range.index, 'image', result.url);
          // Quill 2's built-in Image blot recognizes width/height as native attributes,
          // so formatText writes them directly onto the rendered <img>. Storefront uses
          // these to size the image up-front and prevent CLS. (0, 0) means the server
          // couldn't determine an intrinsic size (e.g. an SVG without width/viewBox) —
          // omit the attributes entirely rather than render an invisible 0×0 image.
          if (result.width && result.height) {
            quill.formatText(range.index, 1, { width: result.width, height: result.height });
          }
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
    };
  }
}
