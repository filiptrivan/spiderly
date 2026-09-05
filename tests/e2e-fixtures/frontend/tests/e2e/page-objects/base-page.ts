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
  // The filter surface is the chip bar above the table (spiderly-filter-bar). Its DOM is
  // spiderly-authored and addressed by data-testid:
  //   add-filter          — the "+ Filter" button (opens a searchable popover of filters)
  //   add-filter-option   — one filter in that popover, matched by its label
  //   filter-editor       — the editor row that opens under the chips
  //   filter-editor-value — the editor's text/number input (bar-drawn controls only)
  //   filter-editor-apply — the commit button; every kind commits through it
  //   filter-chip         — one applied constraint
  //   filter-bar-clear    — Clear filters (renders only while something is applied)
  // Operator labels are the translocoService output (en.json): 'MoreThan' key → 'More than'.
  // PrimeNG bits that remain: .p-datatable-mask (loading overlay),
  // .p-paginator-page-selected (selected pager page).

  private columnHeader(columnLabel: string) {
    const pattern = new RegExp(`^\\s*${columnLabel}\\s*$`, 'i');
    return this.page.locator('thead th').filter({ has: this.page.locator('span', { hasText: pattern }) });
  }

  // PrimeNG v19 renders <div class="p-datatable-mask p-overlay-mask"> over the
  // ENTIRE table (including thead) at z-index 3 while [loading]="true". The mask
  // intercepts pointer events, so a click on a header times out on the
  // visibility/stability gate until the lazy data load resolves. `toBeHidden`
  // passes both when the mask is hidden AND when it never appeared, so it stays
  // correct on tables that load synchronously.
  private async waitForTableLoad() {
    await expect(this.page.locator('.p-datatable-mask')).toBeHidden({ timeout: 15000 });
  }

  // Opens the bar's editor for the named filter via "+ Filter". The popover teleports to
  // document.body, so its options are addressed from the page, not the bar.
  private async openFilterEditor(filterLabel: string) {
    await this.waitForTableLoad();
    await this.page.getByTestId('add-filter').click();
    const pattern = new RegExp(`^\\s*${filterLabel}\\s*$`, 'i');
    await this.page.getByTestId('add-filter-option').filter({ hasText: pattern }).first().click();
    await expect(this.page.getByTestId('filter-editor')).toBeVisible();
  }

  // Every kind commits through the editor's Apply, and a commit re-queries — awaited to its
  // own list fetch because the table does not sequence concurrent lazy loads: overlapping
  // this filter's load with the next helper's would be a race authored into the suite.
  private async applyFilterEditor() {
    const applied = this.page.waitForResponse((r) => /\/GetPaginated\w+List$/.test(new URL(r.url()).pathname));
    await this.page.getByTestId('filter-editor-apply').click();
    await applied;
    await expect(this.page.getByTestId('filter-editor')).toBeHidden();
  }

  async applyTextFilter(filterLabel: string, value: string) {
    await this.openFilterEditor(filterLabel);
    await this.page.getByTestId('filter-editor-value').fill(value);
    await this.applyFilterEditor();
  }

  async applyNumericFilter(filterLabel: string, value: number, matchMode: 'equals' | 'lessThan' | 'greaterThan') {
    const matchModeLabels = { equals: 'Equals', lessThan: 'Less than', greaterThan: 'More than' } as const;
    await this.openFilterEditor(filterLabel);
    const editor = this.page.getByTestId('filter-editor');
    await editor.locator('p-select').first().click();
    await this.page.locator('.p-select-overlay .p-select-option', { hasText: matchModeLabels[matchMode] }).first().click();
    await this.page.getByTestId('filter-editor-value').fill(String(value));
    await this.applyFilterEditor();
  }

  async applyBooleanFilter(filterLabel: string, value: boolean) {
    await this.openFilterEditor(filterLabel);
    // The editor draws a BINARY p-checkbox that starts unchecked: one click drafts true,
    // a second drafts false. Drafts reach nothing until Apply commits them.
    const clicks = value ? 1 : 2;
    const box = this.page.getByTestId('filter-editor').locator('.p-checkbox-box').first();
    for (let i = 0; i < clicks; i++) {
      await box.click();
    }
    await this.applyFilterEditor();
  }

  async clearTableFilters() {
    await this.page.getByTestId('filter-bar-clear').click();
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
