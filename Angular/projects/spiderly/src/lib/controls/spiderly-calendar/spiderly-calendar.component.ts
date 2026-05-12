import { Component, Input, OnDestroy, OnInit } from '@angular/core';
import { BaseControl } from '../base-control';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormsModule, FormControl } from '@angular/forms';
import { RequiredComponent } from '../../components/required/required.component';
import { TranslocoService } from '@jsverse/transloco';
import { DatePickerModule } from 'primeng/datepicker';
import { TooltipModule } from 'primeng/tooltip';
import { Subscription } from 'rxjs';
import { parseDateOnlyLocal } from '../../services/helper-functions';

@Component({
  selector: 'spiderly-calendar',
  templateUrl: './spiderly-calendar.component.html',
  styles: [],
  imports: [
    CommonModule,
    ReactiveFormsModule,
    FormsModule,
    DatePickerModule,
    TooltipModule,
    RequiredComponent,
  ],
})
export class SpiderlyCalendarComponent extends BaseControl implements OnInit, OnDestroy {
  @Input() showTime: boolean = false;
  @Input() dateOnly: boolean = false;
  @Input() timeOnly: boolean = false;

  internalControl = new FormControl<Date | null>(null);

  private syncing = false;
  private subscriptions: Subscription[] = [];

  constructor(protected override translocoService: TranslocoService) {
    super(translocoService);
  }

  override ngOnInit() {
    super.ngOnInit();

    if (!this.usesStringProxy() || !this.control) return;

    this.applyFromOuter(this.control.value);
    if (this.control.disabled) this.internalControl.disable({ emitEvent: false });

    this.subscriptions.push(
      this.control.valueChanges.subscribe((v) => {
        if (!this.syncing) this.applyFromOuter(v);
      })
    );
    this.subscriptions.push(
      this.control.statusChanges.subscribe((status) => {
        if (status === 'DISABLED' && this.internalControl.enabled)
          this.internalControl.disable({ emitEvent: false });
        else if (status !== 'DISABLED' && this.internalControl.disabled)
          this.internalControl.enable({ emitEvent: false });
      })
    );
    this.subscriptions.push(
      this.internalControl.valueChanges.subscribe((d) => {
        if (!this.syncing) this.applyToOuter(d);
      })
    );
  }

  ngOnDestroy() {
    this.subscriptions.forEach((s) => s.unsubscribe());
  }

  setDate(event: Date) {}

  usesStringProxy(): boolean {
    return this.dateOnly || this.timeOnly;
  }

  private applyFromOuter(v: unknown): void {
    this.syncing = true;
    try {
      if (v == null) {
        this.internalControl.setValue(null);
      } else if (this.dateOnly && typeof v === 'string') {
        this.internalControl.setValue(parseDateOnlyLocal(v));
      } else if (this.timeOnly && typeof v === 'string') {
        this.internalControl.setValue(this.parseTimeOnly(v));
      } else if (v instanceof Date) {
        this.internalControl.setValue(v);
      } else {
        this.internalControl.setValue(null);
      }
    } finally {
      this.syncing = false;
    }
  }

  private applyToOuter(d: Date | null): void {
    this.syncing = true;
    try {
      if (d == null) {
        this.control.setValue(null);
      } else if (this.dateOnly) {
        this.control.setValue(this.formatDateOnly(d));
      } else if (this.timeOnly) {
        this.control.setValue(this.formatTimeOnly(d));
      } else {
        this.control.setValue(d);
      }
    } finally {
      this.syncing = false;
    }
  }

  private parseTimeOnly(s: string): Date | null {
    const m = /^(\d{2}):(\d{2})(?::(\d{2}))?/.exec(s);
    if (!m) return null;
    const d = new Date();
    d.setHours(+m[1], +m[2], +(m[3] ?? 0), 0);
    return d;
  }

  private formatDateOnly(d: Date): string {
    return `${d.getFullYear()}-${this.pad(d.getMonth() + 1)}-${this.pad(d.getDate())}`;
  }

  private formatTimeOnly(d: Date): string {
    return `${this.pad(d.getHours())}:${this.pad(d.getMinutes())}:${this.pad(d.getSeconds())}`;
  }

  private pad(n: number): string {
    return String(n).padStart(2, '0');
  }
}
