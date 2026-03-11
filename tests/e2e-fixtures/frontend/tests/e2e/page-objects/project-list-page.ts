import { Page } from '@playwright/test';
import { BasePage } from './base-page';

export class ProjectListPage extends BasePage {
  constructor(page: Page) {
    super(page);
  }

  async goto() {
    await this.navigate('/administration/project');
  }

  async clickAddNew() {
    await this.clickButton('Add New');
  }

  async openProject(name: string) {
    const row = this.page.locator('tbody tr', { hasText: name });
    await row.click();
  }

  async searchProject(searchTerm: string) {
    await this.page.getByPlaceholder('Search').fill(searchTerm);
    await this.waitForNavigation();
  }
}
