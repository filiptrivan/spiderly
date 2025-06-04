import { Component, Input, OnInit, ViewChild } from '@angular/core';
import { BaseControl } from '../base-control';
import { ReactiveFormsModule, FormsModule } from '@angular/forms';
import { RequiredComponent } from '../../components/required/required.component';
import { CommonModule } from '@angular/common';
import { TranslocoService } from '@jsverse/transloco';
import { Editor, EditorModule } from 'primeng/editor';
import { Tooltip, TooltipModule } from 'primeng/tooltip';

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
        RequiredComponent
    ]
})
export class SpiderlyEditorComponent extends BaseControl implements OnInit {
    @ViewChild(Editor) editor: Editor;
    @ViewChild(Tooltip) tooltip: Tooltip;

    constructor(
        protected override translocoService: TranslocoService,
    ) { 
        super(translocoService);
    }

    override ngOnInit(){
        super.ngOnInit();
    }

    onClick(){
        let editableArea: HTMLElement = this.editor.el.nativeElement.querySelector('.ql-editor');
        
        editableArea.onblur = () => {
            this.control.markAsDirty();
            this.tooltip.deactivate();
        };

        editableArea.onfocus = () => {
            if (this.errorMessageTooltipEvent == 'focus' ) {
                this.tooltip.activate();
            }
        };

        editableArea.onmouseover = () => {
            if (this.errorMessageTooltipEvent == 'hover' ) {
                this.tooltip.activate();
            }
        }
    }

}
