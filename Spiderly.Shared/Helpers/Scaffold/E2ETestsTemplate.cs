using Spiderly.Shared.Classes;

namespace Spiderly.Shared.Helpers.Scaffold
{
    /// <summary>
    /// The Angular app's <c>tests/</c> folder: the Playwright specs and the page objects they drive.
    /// </summary>
    internal static class E2ETestsTemplate
    {
        internal static SpiderlyFolder Folder() => new()
        {
            Name = "tests",
            ChildFolders =
            {
                new SpiderlyFolder
                {
                    Name = "e2e",
                    ChildFolders =
                    {
                        new SpiderlyFolder
                        {
                            Name = "specs",
                            Files =
                            {
                                new SpiderlyFile { Name = "auth.spec.ts", Data = GetAuthSpecData() },
                                new SpiderlyFile { Name = "user-crud.spec.ts", Data = GetUserCrudSpecData() },
                            }
                        },
                        new SpiderlyFolder
                        {
                            Name = "page-objects",
                            Files =
                            {
                                new SpiderlyFile { Name = "base-page.ts", Data = GetBasePageObjectData() },
                                new SpiderlyFile { Name = "login-page.ts", Data = GetLoginPageObjectData() },
                                new SpiderlyFile { Name = "user-list-page.ts", Data = GetUserListPageObjectData() },
                            }
                        }
                    },
                    Files =
                    {
                        new SpiderlyFile { Name = ".gitignore", Data = GetE2EGitignoreData() }
                    }
                }
            }
        };

        private static string GetE2EGitignoreData()
        {
            return """
test-results/
playwright-report/
playwright/.cache/
""";
        }

        private static string GetBasePageObjectData()
        {
            return """
import { Page } from '@playwright/test';

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
}
""";
        }

        private static string GetLoginPageObjectData()
        {
            return """
import { Page } from '@playwright/test';
import { BasePage } from './base-page';

export class LoginPage extends BasePage {
  constructor(page: Page) {
    super(page);
  }

  async goto() {
    await this.navigate('/');
  }

  async login(email: string, password: string) {
    await this.page.getByLabel('Email').fill(email);
    await this.page.getByLabel('Password').fill(password);
    await this.page.getByRole('button', { name: 'Login' }).click();
    await this.waitForNavigation();
  }

  async isLoggedIn() {
    return await this.page.locator('[data-testid="user-menu"]').isVisible();
  }

  async logout() {
    await this.page.locator('[data-testid="user-menu"]').click();
    await this.page.getByRole('menuitem', { name: 'Logout' }).click();
  }
}
""";
        }

        private static string GetUserListPageObjectData()
        {
            return """
import { Page } from '@playwright/test';
import { BasePage } from './base-page';

export class UserListPage extends BasePage {
  constructor(page: Page) {
    super(page);
  }

  async goto() {
    await this.navigate('/administration/user');
  }

  async clickAddNew() {
    await this.clickButton('Add New');
  }

  async searchUser(searchTerm: string) {
    await this.page.getByPlaceholder('Search').fill(searchTerm);
    await this.waitForNavigation();
  }

  async deleteUser(userName: string) {
    const row = this.page.locator('tr', { hasText: userName });
    await row.getByRole('button', { name: 'Delete' }).click();
    await this.page.getByRole('button', { name: 'Confirm' }).click();
  }

  async editUser(userName: string) {
    const row = this.page.locator('tr', { hasText: userName });
    await row.getByRole('button', { name: 'Edit' }).click();
  }
}
""";
        }

        private static string GetAuthSpecData()
        {
            return """
import { test, expect } from '@playwright/test';
import { LoginPage } from '../page-objects/login-page';

test.describe('Authentication', () => {
  let loginPage: LoginPage;

  test.beforeEach(async ({ page }) => {
    loginPage = new LoginPage(page);
    await loginPage.goto();
  });

  test('should display login page', async ({ page }) => {
    await expect(page.getByRole('heading', { name: 'Login' })).toBeVisible();
  });

  test('should login with valid credentials', async ({ page }) => {
    await loginPage.login('admin@example.com', 'Admin123!');
    await expect(page).toHaveURL(/.*homepage/);
    const isLoggedIn = await loginPage.isLoggedIn();
    expect(isLoggedIn).toBe(true);
  });

  test('should show error with invalid credentials', async ({ page }) => {
    await loginPage.login('invalid@example.com', 'wrongpassword');
    await expect(page.getByText(/invalid credentials/i)).toBeVisible();
  });

  test('should logout successfully', async ({ page }) => {
    await loginPage.login('admin@example.com', 'Admin123!');
    await loginPage.logout();
    await expect(page).toHaveURL('/');
  });
});
""";
        }

        private static string GetUserCrudSpecData()
        {
            return """
import { test, expect } from '@playwright/test';
import { LoginPage } from '../page-objects/login-page';
import { UserListPage } from '../page-objects/user-list-page';

test.describe('User CRUD Operations', () => {
  let loginPage: LoginPage;
  let userListPage: UserListPage;

  test.beforeEach(async ({ page }) => {
    loginPage = new LoginPage(page);
    userListPage = new UserListPage(page);

    await loginPage.goto();
    await loginPage.login('admin@example.com', 'Admin123!');
  });

  test('should display users list', async ({ page }) => {
    await userListPage.goto();
    await expect(page.getByRole('heading', { name: 'Users' })).toBeVisible();
  });

  test('should create a new user', async ({ page }) => {
    await userListPage.goto();
    await userListPage.clickAddNew();

    await page.getByLabel('First Name').fill('John');
    await page.getByLabel('Last Name').fill('Doe');
    await page.getByLabel('Email').fill('john.doe@example.com');
    await page.getByLabel('Password').fill('Password123!');

    await page.getByRole('button', { name: 'Save' }).click();

    await expect(page.getByText('User created successfully')).toBeVisible();
    await expect(page.locator('tr', { hasText: 'john.doe@example.com' })).toBeVisible();
  });

  test('should edit an existing user', async ({ page }) => {
    await userListPage.goto();
    await userListPage.editUser('john.doe@example.com');

    await page.getByLabel('First Name').clear();
    await page.getByLabel('First Name').fill('Jane');

    await page.getByRole('button', { name: 'Save' }).click();

    await expect(page.getByText('User updated successfully')).toBeVisible();
    await expect(page.locator('tr', { hasText: 'Jane' })).toBeVisible();
  });

  test('should delete a user', async ({ page }) => {
    await userListPage.goto();
    const initialCount = await userListPage.getTableRowCount();

    await userListPage.deleteUser('jane.doe@example.com');

    await expect(page.getByText('User deleted successfully')).toBeVisible();

    const newCount = await userListPage.getTableRowCount();
    expect(newCount).toBe(initialCount - 1);
  });

  test('should search for users', async ({ page }) => {
    await userListPage.goto();
    await userListPage.searchUser('admin');

    await expect(page.locator('tr', { hasText: 'admin' })).toBeVisible();
  });
});
""";
        }
    }
}
