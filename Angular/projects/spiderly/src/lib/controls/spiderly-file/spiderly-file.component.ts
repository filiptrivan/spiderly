import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, OnInit, Output } from '@angular/core';
import { FormsModule, ReactiveFormsModule } from '@angular/forms';
import { TranslocoDirective, TranslocoService } from '@jsverse/transloco';
import { FileSelectEvent, FileUploadModule } from 'primeng/fileupload';
import { RequiredComponent } from '../../components/required/required.component';
import { SpiderlyButtonComponent } from '../../components/spiderly-buttons/spiderly-button/spiderly-button.component';
import { BaseEntity } from '../../entities/base-entity';
import {
  getMimeTypeForFileName,
  isExcelFileType,
  isImageFileType,
} from '../../services/helper-functions';
import { SpiderlyMessageService } from '../../services/spiderly-message.service';
import { ValidatorAbstractService } from '../../services/validator-abstract.service';
import { BaseControl } from '../base-control';

@Component({
  selector: 'spiderly-file',
  templateUrl: './spiderly-file.component.html',
  styles: [],
  imports: [
    CommonModule,
    ReactiveFormsModule,
    FormsModule,
    FileUploadModule,
    RequiredComponent,
    SpiderlyButtonComponent,
    TranslocoDirective,
  ],
})
export class SpiderlyFileComponent extends BaseControl implements OnInit {
  @Output() onFileSelected = new EventEmitter<SpiderlyFileSelectEvent>();
  @Output() onFileRemoved = new EventEmitter<null>();
  @Input() objectId: number;
  @Input() fileData: string;
  @Input() acceptedFileTypes: Array<
    | 'image/*'
    | 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet'
    | 'application/vnd.ms-excel'
    | '.xlsx'
    | '.xls'
  > = ['image/*'];
  @Input() required: boolean; // It's okay for this control, because for the custom uploads where we are not initializing the control from the backend, there is no need for formControl.
  @Input() multiple: boolean = false;
  @Input() isCloudinaryFileData: boolean = true;
  @Input() imageWidth: number = 0;
  @Input() imageHeight: number = 0;

  acceptedFileTypesCommaSeparated: string;
  @Input() files: File[] = [];

  constructor(
    protected override translocoService: TranslocoService,
    private messageService: SpiderlyMessageService,
    private validatorService: ValidatorAbstractService,
  ) {
    super(translocoService);
  }

  override ngOnInit() {
    if (this.control?.value != null && this.fileData != null) {
      if (this.isCloudinaryFileData) {
        this.pushFileFromCloudinaryUrl(this.fileData);
      } else {
        const file = this.getFileFromBase64(this.fileData);
        this.files.push(file);
      }
    }

    if (!this.objectId) {
      this.objectId = 0;
    }

    this.acceptedFileTypesCommaSeparated = this.acceptedFileTypes.join(',');

    super.ngOnInit();
  }

  filesSelected(event: FileSelectEvent) {
    const file = event.files[0];

    if (
      this.isImageFileType(file.type) &&
      this.hasImageDimensionConstraints()
    ) {
      this.files = [];
      this.validatorService
        .validateImageDimensions(file, this.imageWidth, this.imageHeight)
        .then((result) => {
          if (result.isValid) {
            this.files = [file];
            this.emitFileSelected(file);
          } else {
            this.messageService.errorMessage(result.errors.join('\n'));
          }
        });
    } else {
      this.emitFileSelected(file);
    }
  }

  private emitFileSelected(file: File): void {
    const formData = new FormData();
    formData.append('file', file, `${this.objectId}-${file.name}`);

    this.onFileSelected.next(
      new SpiderlyFileSelectEvent({ file: file, formData: formData }),
    );
  }

  private hasImageDimensionConstraints(): boolean {
    return this.imageWidth > 0 || this.imageHeight > 0;
  }

  choose(event, chooseCallback) {
    chooseCallback();
  }

  fileRemoved(removeFileCallback, index: number) {
    removeFileCallback(index);
    this.control?.setValue(null);
    this.onFileRemoved.next(null);
  }

  // Put inside global functions if you need it
  async pushFileFromCloudinaryUrl(cloudinaryUrl: string) {
    const response = await fetch(cloudinaryUrl);

    if (!response.ok) {
      throw new Error(
        `Failed to fetch file from Cloudinary: ${response.statusText}`,
      );
    }

    const blob = await response.blob();

    const urlParts = cloudinaryUrl.split('/');
    const lastPart = urlParts[urlParts.length - 1];
    const fileName = lastPart.split('?')[0];

    const file = new File([blob], fileName, { type: blob.type });

    this.files = [...this.files, file]; // this.files.push(file); doesn't work

    return file;
  }

  // Put inside global functions if you need it
  getFileFromBase64(base64String: string) {
    const [header, base64Content] = base64String.split(';base64,');
    const fileName = header.split('=')[1];
    const mimeType = getMimeTypeForFileName(fileName);

    const byteCharacters = atob(base64Content);
    const byteNumbers = new Uint8Array(byteCharacters.length);

    for (let i = 0; i < byteCharacters.length; i++) {
      byteNumbers[i] = byteCharacters.charCodeAt(i);
    }

    const blob = new Blob([byteNumbers], { type: mimeType });
    const file = new File([blob], fileName, { type: mimeType });

    return file;
  }

  isImageFileType(mimeType: string): boolean {
    return isImageFileType(mimeType);
  }

  isExcelFileType(mimeType: string): boolean {
    return isExcelFileType(mimeType);
  }
}

export class SpiderlyFileSelectEvent extends BaseEntity {
  file?: File;
  formData?: FormData;

  constructor({
    file,
    formData,
  }: {
    file?: File;
    formData?: FormData;
  } = {}) {
    super();

    this.file = file;
    this.formData = formData;
  }

  static readonly typeName = 'SpiderlyFileSelectEvent' as const;
}
