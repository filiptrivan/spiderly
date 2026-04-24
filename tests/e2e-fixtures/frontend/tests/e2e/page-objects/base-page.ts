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

  async clearTableFilters() {
    // Spiderly's ClearFilters translocoKey renders as "Clear all filters" in en.json.
    await this.page.locator('.table-header button', { hasText: /Clear/i }).click();
  }

  async sortByColumn(columnLabel: string) {
    // Case-insensitive: Spiderly translation may render "Id" as "ID" in en.json.
    const pattern = new RegExp(columnLabel, 'i');
    await this.page.locator('thead th').filter({ hasText: pattern }).first().click();
  }

  async gotoTablePage(pageNumber: number) {
    await this.page.locator('.p-paginator-page', { hasText: String(pageNumber) }).click();
  }

  async getSessionStorageEntry<T = unknown>(key: string): Promise<T | null> {
    return await this.page.evaluate((k) => {
      const raw = sessionStorage.getItem(k);
      return raw ? (JSON.parse(raw) as unknown) : null;
    }, key) as T | null;
  }
}
