import { provideHttpClient } from '@angular/common/http';
import { Component } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { TranslocoTestingModule } from '@jsverse/transloco';
import { FileUpload } from 'primeng/fileupload';

import { SpiderlyMessageService } from '../../services/spiderly-message.service';
import { ValidatorAbstractService } from '../../services/validator-abstract.service';
import { translocoTesting } from '../../testing/spec-support.spec';
import {
  SpiderlyFileComponent,
  SpiderlyFileSelectEvent,
} from './spiderly-file.component';

@Component({
  template: `<spiderly-file
    label="Document"
    [acceptedFileTypes]="['application/pdf']"
    [maxFileSize]="1000"
    [multiple]="multiple"
    (onFileSelected)="selected.push($event)"
  />`,
  imports: [SpiderlyFileComponent],
})
class HostComponent {
  multiple = false;
  selected: SpiderlyFileSelectEvent[] = [];
}

const pdf = (name: string, size: number): File =>
  new File([new Uint8Array(size)], name, { type: 'application/pdf' });

// The generated upload sends what onFileSelected emits, so a file PrimeNG refuses must not be
// emitted: why and how at SpiderlyFileComponent.filesSelected.
describe('SpiderlyFileComponent: a file PrimeNG refuses is not emitted', () => {
  let fixture: ComponentFixture<HostComponent>;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [HostComponent, TranslocoTestingModule.forRoot(translocoTesting())],
      providers: [
        provideNoopAnimations(),
        provideHttpClient(),
        { provide: ValidatorAbstractService, useValue: {} },
        { provide: SpiderlyMessageService, useValue: {} },
      ],
    });
  });

  async function render(multiple = false): Promise<void> {
    fixture = TestBed.createComponent(HostComponent);
    fixture.componentInstance.multiple = multiple;
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
  }

  // Drives PrimeNG's own onFileSelect, so its validate() decides what currentFiles holds.
  async function pick(...files: File[]): Promise<void> {
    fixture.debugElement
      .query(By.directive(FileUpload))
      .injector.get(FileUpload)
      .onFileSelect({ type: 'change', target: { files } });
    await fixture.whenStable();
  }

  const emittedNames = (): string[] =>
    fixture.componentInstance.selected.map((e) => e.file?.name);

  it('emits nothing for a file over maxFileSize', async () => {
    await render();
    await pick(pdf('big.pdf', 1001));
    expect(emittedNames()).toEqual([]);
  });

  it('emits nothing for a file outside acceptedFileTypes', async () => {
    await render();
    await pick(new File([new Uint8Array(10)], 'notes.txt', { type: 'text/plain' }));
    expect(emittedNames()).toEqual([]);
  });

  it('emits a file inside both limits once', async () => {
    await render();
    await pick(pdf('ok.pdf', 1000));
    expect(emittedNames()).toEqual(['ok.pdf']);
  });

  it('with multiple, emits the first valid file of this pick, never an earlier one', async () => {
    await render(true);
    await pick(pdf('first.pdf', 10));
    await pick(pdf('big.pdf', 1001), pdf('second.pdf', 20));
    expect(emittedNames()).toEqual(['first.pdf', 'second.pdf']);
  });

  it('with multiple, emits nothing when every file of this pick is refused', async () => {
    await render(true);
    await pick(pdf('first.pdf', 10));
    await pick(pdf('big.pdf', 1001));
    expect(emittedNames()).toEqual(['first.pdf']);
  });
});
