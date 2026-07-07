import { Component, ElementRef, Input, OnInit, ViewChild } from '@angular/core';
import { BaseControl } from '../base-control';
import { ReactiveFormsModule, FormsModule } from '@angular/forms';
import { RequiredComponent } from '../../components/required/required.component';
import { CommonModule } from '@angular/common';
import { TranslocoDirective, TranslocoService } from '@jsverse/transloco';
import { TextareaModule } from 'primeng/textarea';
import { TabsModule } from 'primeng/tabs';
import { MarkdownComponent } from 'ngx-markdown';
import { Observable } from 'rxjs';
import { EditorImageUploadResult } from '../../entities/editor-image-upload-result';

/**
 * Markdown form control: a plain textarea (Write) with a rendered live preview (Preview),
 * arranged as tabs. The stored value is raw Markdown text.
 *
 * The preview is rendered with ngx-markdown (marked) and is intentionally APPROXIMATE — a
 * consuming storefront may render the same Markdown with a different engine/flavor.
 *
 * The <textarea> DOM is the source of truth for the text (the form control mirrors it, like
 * spiderly-editor mirrors Quill). We never splice control.value, because SpiderlyFormControl
 * defaults to updateOn:'blur' and would be stale relative to the focused textarea.
 *
 * When {@link uploadImageMethod} is provided (wired by the generator for properties with an
 * S3 public-storage attribute), pasting an image uploads it and, on success, inserts a
 * standard `![](url)` link at the caret via execCommand (which preserves the native undo
 * stack). Upload progress is shown out-of-band, not as a token in the text.
 */
@Component({
  selector: 'spiderly-markdown',
  templateUrl: './spiderly-markdown.component.html',
  imports: [
    CommonModule,
    ReactiveFormsModule,
    FormsModule,
    TextareaModule,
    TabsModule,
    MarkdownComponent,
    RequiredComponent,
    TranslocoDirective,
  ],
})
export class SpiderlyMarkdownComponent extends BaseControl implements OnInit {
  @ViewChild('textarea') textareaRef: ElementRef<HTMLTextAreaElement>;

  @Input() uploadImageMethod: (formData: FormData) => Observable<EditorImageUploadResult>;
  @Input() objectId: number = 0;
  /** Mirrors the entity property's [AcceptedFileTypes]. Markdown uploads are paste-only today, so this
   * only exists to accept the generated binding shared with spiderly-editor; the paste path takes any
   * pasted image and lets the server whitelist decide. */
  @Input() acceptedFileTypes: string[];

  pendingImageUploads: number = 0;
  imageUploadFailed: boolean = false;

  constructor(protected override translocoService: TranslocoService) {
    super(translocoService);
  }

  override ngOnInit() {
    super.ngOnInit();
  }

  onPaste(event: ClipboardEvent) {
    // Only intercept when image upload is wired; otherwise let the default paste happen.
    if (!this.uploadImageMethod || this.control?.disabled) return;

    const imageFile = this.getPastedImage(event);
    if (!imageFile) return;

    event.preventDefault();

    const formData = new FormData();
    formData.append('file', imageFile, `${this.objectId}-${imageFile.name || 'pasted-image.png'}`);

    this.imageUploadFailed = false;
    this.pendingImageUploads++;

    this.uploadImageMethod(formData).subscribe({
      next: (result: EditorImageUploadResult) => {
        this.pendingImageUploads--;
        this.insertImageMarkdown(result.url);
      },
      error: () => {
        this.pendingImageUploads--;
        this.imageUploadFailed = true;
      },
    });
  }

  private getPastedImage(event: ClipboardEvent): File | null {
    const items = event.clipboardData?.items;
    if (!items) return null;

    for (let i = 0; i < items.length; i++) {
      if (items[i].type.startsWith('image/')) {
        return items[i].getAsFile();
      }
    }

    return null;
  }

  private insertImageMarkdown(url: string) {
    const snippet = `![](${url})`;
    const textarea = this.textareaRef?.nativeElement;

    // Preferred path: the textarea is focused, so insert at the caret while preserving the
    // native undo stack. The resulting 'input' event syncs the control on blur, exactly like
    // the user typing — no manual setValue, no stale-model read.
    if (textarea && document.activeElement === textarea && document.execCommand('insertText', false, snippet)) {
      return;
    }

    // Fallback (textarea blurred/absent, or execCommand unsupported): read the LIVE textarea
    // value — never control.value, which is stale while focused. When blurred, the control is
    // already current, so appending can't drop uncommitted text.
    if (textarea) {
      const sep = textarea.value.length ? '\n' : '';
      textarea.value = `${textarea.value}${sep}${snippet}`;
      this.control.setValue(textarea.value);
    } else {
      const current = this.control.value ?? '';
      this.control.setValue(current.length ? `${current}\n${snippet}` : snippet);
    }
    this.control.markAsDirty();
  }
}
