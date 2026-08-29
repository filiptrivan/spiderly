import { Page, expect } from '@playwright/test';

export class BasePage {
  constructor(protected page: Page) {}

  async navigate(path: string) {
    await this.page.goto(path);
  }

  async waitForNavigation() {
    await this.page.waitForLoadState('networkidle');
  }

  async clickButton(text: string) {
    await this.page.getByRole('button', { name: text }).click();
  }

  async fillInput(label: string, value: string) {
    await this.page.getByLabel(label).fill(value);
  }

  async getTableRowCount() {
    return await this.page.locator('tbody tr').count();
  }

  async fillNumber(label: string, value: number) {
    const input = this.page.locator(`spiderly-inputnumber[label="${label}"] input`);
    await input.click();
    await input.fill(String(value));
  }

  async selectDropdown(label: string, option: string) {
    const dropdown = this.page.locator(`spiderly-dropdown[label="${label}"]`);
    await dropdown.locator('.p-dropdown').click();
    await this.page.locator('.p-dropdown-items .p-dropdown-item', { hasText: option }).click();
  }

  async selectAutocomplete(label: string, searchTerm: string) {
    const autocomplete = this.page.locator(`spiderly-autocomplete[label="${label}"]`);
    const input = autocomplete.locator('input');
    await input.fill(searchTerm);
    await this.page.locator('.p-autocomplete-items .p-autocomplete-item').first().click();
  }

  async toggleCheckbox(label: string) {
    const checkbox = this.page.locator(`spiderly-checkbox[label="${label}"]`);
    await checkbox.locator('.p-checkbox-box').click();
  }

  async selectCalendarDate(label: string, day: number) {
    const calendar = this.page.locator(`spiderly-calendar[label="${label}"]`);
    await calendar.locator('input').click();
    await this.page.locator('.p-datepicker-calendar td:not(.p-datepicker-other-month) span', { hasText: new RegExp(`^${day}$`) }).first().click();
  }

  async fillEditor(label: string, text: string) {
    const editor = this.page.locator(`spiderly-editor[label="${label}"] .ql-editor`);
    await editor.click();
    await editor.fill(text);
  }

  async clickSave() {
    await this.clickButton('Save');
  }

  async expectSaveSuccess() {
    await expect(this.page.locator('.p-toast-message-success')).toBeVisible({ timeout: 10000 });
  }

  async expectTableToContain(text: string) {
    await expect(this.page.locator('tbody tr', { hasText: text })).toBeVisible({ timeout: 10000 });
  }

  async deleteRowByText(text: string) {
    const row = this.page.locator('tbody tr', { hasText: text });
    await row.locator('button.p-button-danger').click();
    await this.page.locator('.p-confirmdialog .p-confirm-dialog-accept').click();
  }

  // --- Spiderly data-table helpers ---
  // PrimeNG v19 DOM (verified from primeng-table source + live CI trace):
  //   .p-datatable-mask                  — loading overlay (z-index 3, covers headers too)
  //   .p-datatable-column-filter-button  — filter icon in the column header
  //   .p-datatable-filter-overlay        — popup containing the filter form
  //   .p-paginator-page-selected         — selected pager page
  // The Apply/Clear buttons in the popup footer are <p-button> elements without
  // any identifying class (the pcFilterApplyButton entry in PrimeNG's classes
  // table is unused at render time), so we match them by accessible name.
  // Spiderly's matchModeNumberOptions render labels 'Equals', 'LessThan', 'MoreThan'.

  private columnHeader(columnLabel: string) {
    const pattern = new RegExp(`^\\s*${columnLabel}\\s*$`, 'i');
    return this.page.locator('thead th').filter({ has: this.page.locator('span', { hasText: pattern }) });
  }

  // PrimeNG v19 renders <div class="p-datatable-mask p-overlay-mask"> over the
  // ENTIRE table (including thead) at z-index 3 while [loading]="true". The mask
  // intercepts pointer events, so a click on the filter button times out on the
  // visibility/stability gate until the lazy data load resolves. `toBeHidden`
  // passes both when the mask is hidden AND when it never appeared, so it stays
  // correct on tables that load synchronously.
  private async waitForTableLoad() {
    await expect(this.page.locator('.p-datatable-mask')).toBeHidden({ timeout: 15000 });
  }

  private async openColumnFilter(columnLabel: string) {
    await this.waitForTableLoad();
    await this.columnHeader(columnLabel).locator('.p-datatable-column-filter-button').first().click();
    await expect(this.page.locator('.p-datatable-filter-overlay')).toBeVisible();
  }

  private async applyColumnFilter() {
    await this.page.locator('.p-datatable-filter-overlay').getByRole('button', { name: 'Apply' }).first().click();
    await expect(this.page.locator('.p-datatable-filter-overlay')).toBeHidden();
  }

  async applyTextFilter(columnLabel: string, value: string) {
    await this.openColumnFilter(columnLabel);
    await this.page.locator('.p-datatable-filter-overlay input[type="text"]').first().fill(value);
    await this.applyColumnFilter();
  }

  async applyNumericFilter(columnLabel: string, value: number, matchMode: 'equals' | 'lessThan' | 'greaterThan') {
    // Match mode option labels are the translocoService output (en.json):
    // 'MoreThan' key → 'More than' rendered text. Spiderly's column must have
    // showMatchModes:true for PrimeNG to render the match-mode <p-select>.
    const matchModeLabels = { equals: 'Equals', lessThan: 'Less than', greaterThan: 'More than' } as const;
    await this.openColumnFilter(columnLabel);
    const overlay = this.page.locator('.p-datatable-filter-overlay');
    await overlay.locator('p-select').first().click();
    await this.page.locator('.p-select-overlay .p-select-option', { hasText: matchModeLabels[matchMode] }).first().click();
    await overlay.locator('p-inputnumber input').first().fill(String(value));
    await this.applyColumnFilter();
  }

  // Boolean is an AUTO-APPLYING filter type, so this menu has no Apply button to press and
  // does not close itself on commit — the contract is the library's
  // spiderly-data-table/CLAUDE.md § 'Filter menu Apply button — hidden for auto-applying types'.
  // PrimeNG's ColumnFilterFormElement.onModelChange calls dt._filter() on every checkbox
  // change, so the click IS the commit and dismissal is ours (Esc, handled by the overlay's
  // own keydown.escape; focus sits on the checkbox input inside it after the click).
  // Each click is awaited to its own list fetch because the table does not sequence
  // concurrent lazy loads — overlapping this filter's load with the next helper's would be a
  // race authored into the suite.
  async applyBooleanFilter(columnLabel: string, value: boolean) {
    await this.openColumnFilter(columnLabel);
    // PrimeNG renders <p-checkbox binary indeterminate> with click cycle null → true → false → null.
    // Force click bypasses stability check: when the column sits near the viewport
    // edge (e.g. last column), PrimeNG keeps repositioning the overlay so .p-checkbox-box
    // never settles long enough for a normal click.
    const clicks = value ? 1 : 2;
    const box = this.page.locator('.p-datatable-filter-overlay .p-checkbox-box').first();
    for (let i = 0; i < clicks; i++) {
      const applied = this.page.waitForResponse((r) => /\/GetPaginated\w+List$/.test(new URL(r.url()).pathname));
      await box.click({ force: true });
      await applied;
    }
    await this.page.keyboard.press('Escape');
    await expect(this.page.locator('.p-datatable-filter-overlay')).toBeHidden();
  }

  async clearTableFilters() {
    // Spiderly's t('ClearFilters') renders as "Clear all filters" in en.json.
    await this.page.locator('.table-header button', { hasText: /Clear/i }).click();
  }

  async sortByColumn(columnLabel: string, opts: { multi?: boolean } = {}) {
    // Same loading-mask block as openColumnFilter — each prior filter/sort
    // triggers a reload, and the mask covers headers until it resolves.
    await this.waitForTableLoad();
    const header = this.columnHeader(columnLabel).first();
    await (opts.multi ? header.click({ modifiers: ['Control'] }) : header.click());
  }

  async gotoTablePage(pageNumber: number) {
    await this.waitForTableLoad();
    await this.page.locator('.p-paginator-page', { hasText: String(pageNumber) }).click();
  }

  async getSessionStorageEntry<T = unknown>(key: string): Promise<T | null> {
    return await this.page.evaluate((k) => {
      const raw = sessionStorage.getItem(k);
      return raw ? (JSON.parse(raw) as unknown) : null;
    }, key) as T | null;
  }
}
