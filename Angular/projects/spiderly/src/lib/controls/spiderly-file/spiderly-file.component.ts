import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, OnInit, Output } from '@angular/core';
import { FormsModule, ReactiveFormsModule } from '@angular/forms';
import { TranslocoDirective, TranslocoService } from '@jsverse/transloco';
import { FileSelectEvent, FileUploadModule } from 'primeng/fileupload';
import { RequiredComponent } from '../../components/required/required.component';
import { SpiderlyButtonComponent } from '../../components/spiderly-buttons/spiderly-button/spiderly-button.component';
import { BaseEntity } from '../../entities/base-entity';
import {
  getImageDimensions,
  getMimeTypeForFileName,
  isExcelFileType,
  isFileImageType,
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
    | 'image/png'
    | 'image/jpeg'
    | 'image/webp'
    | 'image/gif'
    | 'image/avif'
    | 'image/svg+xml'
    | '.png'
    | '.jpg'
    | '.jpeg'
    | '.webp'
    | '.gif'
    | '.avif'
    | '.svg'
    | 'application/pdf'
    | '.pdf'
    | 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet'
    | 'application/vnd.ms-excel'
    | '.xlsx'
    | '.xls'
    | 'video/*'
    | 'video/mp4'
    | '.mp4'
    | 'audio/*'
    | 'audio/mpeg'
    | '.mp3'
    | 'application/zip'
    | '.zip'
    | 'text/csv'
    | '.csv'
  > = ['image/*'];
  @Input() required: boolean; // It's okay for this control, because for the custom uploads where we are not initializing the control from the backend, there is no need for formControl.
  @Input() multiple: boolean = false;
  @Input() isUrlFileData: boolean = true;
  @Input() imageWidth: number = 0;
  @Input() imageHeight: number = 0;
  @Input() maxFileSize: number = 20_000_000;

  acceptedFileTypesCommaSeparated: string;
  existingFileUrl: string | null = null;
  existingFileIsImage: boolean = false;
  existingFileName: string = '';
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
      if (this.isUrlFileData) {
        this.setExistingFileUrl(this.fileData);
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
    this.existingFileUrl = null;

    if (
      this.isFileImageType(file.type) &&
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

  private async emitFileSelected(file: File): Promise<void> {
    const formData = new FormData();
    formData.append('file', file, `${this.objectId}-${file.name}`);

    let width: number | undefined;
    let height: number | undefined;

    if (this.isFileImageType(file.type)) {
      const dimensions = await getImageDimensions(file);
      width = dimensions.width;
      height = dimensions.height;
    }

    this.onFileSelected.next(
      new SpiderlyFileSelectEvent({ file, formData, width, height }),
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
    this.clearFile();
  }

  removeExistingFile() {
    this.existingFileUrl = null;
    this.clearFile();
  }

  private clearFile() {
    this.control?.setValue(null);
    this.onFileRemoved.next(null);
  }

  private setExistingFileUrl(url: string) {
    this.existingFileUrl = url;
    this.existingFileName = url.split('/').pop()?.split('?')[0] ?? '';
    const mimeType = getMimeTypeForFileName(this.existingFileName);
    this.existingFileIsImage = isFileImageType(mimeType);
  }

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

  isFileImageType(mimeType: string): boolean {
    return isFileImageType(mimeType);
  }

  isExcelFileType(mimeType: string): boolean {
    return isExcelFileType(mimeType);
  }
}

export class SpiderlyFileSelectEvent extends BaseEntity {
  file?: File;
  formData?: FormData;
  width?: number;
  height?: number;

  constructor({
    file,
    formData,
    width,
    height,
  }: {
    file?: File;
    formData?: FormData;
    width?: number;
    height?: number;
  } = {}) {
    super();

    this.file = file;
    this.formData = formData;
    this.width = width;
    this.height = height;
  }

  static readonly typeName = 'SpiderlyFileSelectEvent' as const;
}
