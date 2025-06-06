import { Component, EventEmitter, Input, OnInit, Output } from '@angular/core';
import { BaseControl } from '../base-control';
import { ReactiveFormsModule, FormsModule } from '@angular/forms';
import { RequiredComponent } from '../../components/required/required.component';
import { CommonModule } from '@angular/common';
import { FileSelectEvent, FileUploadModule } from 'primeng/fileupload';
import { getMimeTypeForFileName, isExcelFileType, isImageFileType } from '../../services/helper-functions';
import { TranslocoDirective, TranslocoService } from '@jsverse/transloco';
import { BaseEntity } from '../../entities/base-entity';
import { SpiderlyButtonComponent } from '../../components/spiderly-buttons/spiderly-button/spiderly-button.component';

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
        TranslocoDirective
    ]
})
export class SpiderlyFileComponent extends BaseControl implements OnInit {
    @Output() onFileSelected = new EventEmitter<SpiderlyFileSelectEvent>();
    @Output() onFileRemoved = new EventEmitter<null>();
    @Input() objectId: number;
    @Input() fileData: string;
    @Input() acceptedFileTypes: Array<'image/*' | 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet' | 'application/vnd.ms-excel' | '.xlsx' | '.xls'> = ['image/*'];
    @Input() required: boolean; // It's okay for this control, because for the custom uploads where we are not initializing the control from the backend, there is no need for formControl.
    @Input() multiple: boolean = false;
    
    acceptedFileTypesCommaSeparated: string;
    @Input() files: File[] = [];

    constructor(
        protected override translocoService: TranslocoService,
    ) { 
        super(translocoService);
    }

    override ngOnInit(){
        if (this.control?.value != null && this.fileData != null) {
            const file = this.base64ToFile(this.fileData);
            this.files.push(file);
        }

        this.acceptedFileTypesCommaSeparated = this.acceptedFileTypes.join(',');

        super.ngOnInit();
    }

    filesSelected(event: FileSelectEvent){
        const file = event.files[0];

        const formData: FormData = new FormData();
        formData.append('file', file, `${this.objectId}-${file.name}`);
        
        this.onFileSelected.next(new SpiderlyFileSelectEvent({file: file, formData: formData}));
    }

    choose(event, chooseCallback){
        chooseCallback();
    }
    
    fileRemoved(removeFileCallback, index: number){
        removeFileCallback(index);
        this.control?.setValue(null);
        this.onFileRemoved.next(null);
    }

    // Put inside global functions if you need it
    base64ToFile(base64String: string){
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

export class SpiderlyFileSelectEvent extends BaseEntity
{
    file?: File;
    formData?: FormData;

    constructor(
    {
        file,
        formData,
    }:{
        file?: File;
        formData?: FormData;
    } = {}
    ) {
        super('SpiderlyFileSelectEvent'); 

        this.file = file;
        this.formData = formData;
    }
}