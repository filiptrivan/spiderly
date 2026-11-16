using Spiderly.Shared.Classes;
using Spiderly.Shared.Enums;
using static Spiderly.Shared.Helpers.Scaffold.TemplateComments;

namespace Spiderly.Shared.Helpers.Scaffold
{
    /// <summary>
    /// The <c>Frontend/</c> folder: the Angular workspace's own files (its project, package, TypeScript and tool
    /// configuration), with its <c>src/</c> and <c>tests/</c> folders.
    /// </summary>
    internal static class FrontendTemplate
    {
        internal static SpiderlyFolder Folder(ProjectGenerationOptions options)
        {
            string appName = options.AppName;
            string spiderlyVersion = options.SpiderlyVersion;
            bool isRunningFromNuget = options.IsRunningFromNuget;
            PackageManagerCodes packageManager = options.PackageManager;

            return new SpiderlyFolder
            {
                Name = "Frontend",
                ChildFolders =
                {
                    E2ETestsTemplate.Folder(),
                    AngularSourceTemplate.Folder(appName, isRunningFromNuget),
                },
                Files =
                {
                    new SpiderlyFile { Name = ".editorconfig", Data = GetEditOrConfigData() },
                    new SpiderlyFile { Name = ".postcssrc.json", Data = GetPostCssRcJsonData() },
                    new SpiderlyFile { Name = ".prettierrc", Data = GetPrettierRcData() },
                    new SpiderlyFile { Name = "angular.json", Data = GetAngularJsonData(appName) },
                    new SpiderlyFile { Name = "package.json", Data = PackageJson(appName, spiderlyVersion, isRunningFromNuget) },
                    new SpiderlyFile { Name = "playwright.config.ts", Data = GetPlaywrightConfigData(packageManager) },
                    new SpiderlyFile { Name = "README.md", Data = GetFrontendREADMEData(appName, spiderlyVersion) },
                    new SpiderlyFile { Name = "tsconfig.app.json", Data = GetTsConfigAppJsonData() },
                    new SpiderlyFile { Name = "tsconfig.json", Data = GetTsConfigJsonData(isRunningFromNuget) },
                    new SpiderlyFile { Name = "tsconfig.spec.json", Data = GetTsConfigSpecJsonData() },
                    new SpiderlyFile { Name = "vercel.json", Data = GetVercelJsonData(appName) },
                }
            };
        }

        private static string GetVercelJsonData(string appName)
        {
            return $$"""
{
    "rewrites": [{ "source": "/(.*)", "destination": "/src/index.html" }],
    "outputDirectory": "dist/{{appName}}/browser"
}
""";
        }

        private static string GetTsConfigSpecJsonData()
        {
            return $$"""
/* To learn more about this file see: https://angular.io/config/tsconfig. */
{
  "extends": "./tsconfig.json",
  "compilerOptions": {
    "outDir": "./out-tsc/spec",
    "types": [
      "jasmine",
    ]
  },
  "include": [
    "src/**/*.spec.ts",
    "src/**/*.d.ts"
  ]
}
""";
        }

        private static string GetTsConfigJsonData(bool isRunningFromNuget)
        {
            return $$"""
/* To learn more about this file see: https://angular.io/config/tsconfig. */
{
  "compileOnSave": false,
  "compilerOptions": {
    "baseUrl": "./",
    "paths": {
{{SlashCommented($$"""
        "spiderly": ["../../spiderly/Angular/projects/spiderly/src/public-api"]
""", isRunningFromNuget)}} 
    },
    "outDir": "./dist/out-tsc",
    "esModuleInterop": true,
    "forceConsistentCasingInFileNames": true,
    "strict": false,
    "noImplicitOverride": true,
    "noPropertyAccessFromIndexSignature": true,
    "noImplicitReturns": true,
    "noFallthroughCasesInSwitch": true,
    "sourceMap": true,
    "declaration": false,
    "importHelpers": true,
    "module": "ES2022",
    "moduleResolution": "node",
    "experimentalDecorators": true,
    "target": "ES2022",
    "resolveJsonModule": true,
    "useDefineForClassFields": false,
    "lib": [
      "ES2022",
      "dom"
    ]
  },
  "exclude": ["node_modules", "**/node_modules/*"],
  "angularCompilerOptions": {
    "preserveSymlinks": true,
    "enableI18nLegacyMessageIdFormat": false,
    "fullTemplateTypeCheck": true,
    "strictInjectionParameters": true,
    "strictInputAccessModifiers": true,
    "strictTemplates": true,
    "strictInputTypes": true
  }
}

""";
        }

        private static string GetTsConfigAppJsonData()
        {
            return $$"""
/* To learn more about this file see: https://angular.io/config/tsconfig. */
{
  "extends": "./tsconfig.json",
  "compilerOptions": {
    "outDir": "./out-tsc/app",
    "types": [
    ]
  },
  "files": [
    "src/main.ts"
  ],
  "include": [
    "src/**/*.d.ts"
  ]
}

""";
        }

        /// <summary>The tests read it through <see cref="NetAndAngularFilesGenerator.GetPackageJsonData"/>.</summary>
        internal static string PackageJson(string appName, string version, bool isRunningFromNuget)
        {
            // The library's peers, with the packages the app itself imports beside them.
            SortedDictionary<string, string> dependencies = new(AngularLibraryPackage.PeerDependencyVersions(), StringComparer.Ordinal)
            {
                ["@primeng/themes"] = "19.1.3",
                ["tslib"] = "2.3.0",
            };
            if (isRunningFromNuget)
                dependencies["spiderly"] = version;

            // The peerDependencies of @angular/compiler-cli name the exact @angular/compiler of its own version.
            // So the toolchain takes the workspace's pins, as the peers do.
            // A toolchain pinned apart from them makes npm refuse the install (ERESOLVE).
            return $$"""
{
    "name": "{{appName}}",
    "version": "0.0.0",
    "scripts": {
        "ng": "ng",
        "start": "ng serve --port=4200 --open --configuration=development",
        "build": "ng build",
        "watch": "ng build --watch --configuration development",
        "test": "ng test",
        "test:e2e": "playwright test",
        "test:e2e:ui": "playwright test --ui",
        "test:e2e:headed": "playwright test --headed",
        "test:e2e:debug": "playwright test --debug",
        "test:e2e:report": "playwright show-report",
        "i18n:extract": "transloco-keys-manager extract --langs en",
        "i18n:find": "transloco-keys-manager find"
    },
    "private": true,
    "dependencies": {
{{string.Join(",\n", dependencies.Select(dependency => $"        \"{dependency.Key}\": \"{dependency.Value}\""))}}
    },
    "devDependencies": {
        "@angular-devkit/build-angular": "{{AngularLibraryPackage.WorkspaceDevDependencyVersion("@angular-devkit/build-angular")}}",
        "@angular/cli": "{{AngularLibraryPackage.WorkspaceDevDependencyVersion("@angular/cli")}}",
        "@angular/compiler-cli": "{{AngularLibraryPackage.WorkspaceDevDependencyVersion("@angular/compiler-cli")}}",
        "@jsverse/transloco-keys-manager": "6.2.2",
        "@playwright/test": "1.60.0",
        "@types/jasmine": "5.1.0",
        "@types/node": "22.10.5",
        "jasmine-core": "5.1.0",
        "karma": "6.4.0",
        "karma-chrome-launcher": "3.2.0",
        "karma-coverage": "2.2.0",
        "karma-jasmine": "5.1.0",
        "karma-jasmine-html-reporter": "2.1.0",
        "tailwindcss": "^4.0.0",
        "@tailwindcss/postcss": "^4.0.0",
        "tailwindcss-primeui": "0.6.1",
        "typescript": "5.5.4"
    }
}
""";
        }

        private static string GetAngularJsonData(string appName)
        {
            return $$"""
{
    "$schema": "./node_modules/@angular/cli/lib/config/schema.json",
    "version": 1,
    "newProjectRoot": "projects",
    "projects": {
        "{{appName}}": {
            "projectType": "application",
            "schematics": {
                "@schematics/angular:component": {
                    "style": "scss",
                    "standalone": false
                },
                "@schematics/angular:directive": {
                    "standalone": false
                },
                "@schematics/angular:pipe": {
                    "standalone": false
                }
            },
            "root": "",
            "sourceRoot": "src",
            "prefix": "app",
            "architect": {
                "build": {
                    "builder": "@angular-devkit/build-angular:application",
                    "options": {
                        "preserveSymlinks": true,
                        "outputPath": "dist/{{appName}}",
                        "index": "src/index.html",
                        "browser": "src/main.ts",
                        "polyfills": [
                            "zone.js"
                        ],
                        "tsConfig": "tsconfig.app.json",
                        "inlineStyleLanguage": "scss",
                        "assets": [
                            "src/favicon.ico",
                            { "glob": "**/*", "input": "src/assets", "output": "assets", "ignore": ["i18n/**"] },
                            "src/robots.txt"
                        ],
                        "styles": [
                            "src/assets/tailwind.css",
                            "src/assets/styles.scss"
                        ],
                        "scripts": [],
                        "stylePreprocessorOptions": {
                            "sass": {
                                "silenceDeprecations": ["global-builtin", "import"]
                            }
                        }
                    },
                    "configurations": {
                        "production": {
                            "budgets": [
                                {
                                    "type": "initial",
                                    "maximumWarning": "1mb",
                                    "maximumError": "3mb"
                                },
                                {
                                    "type": "anyComponentStyle",
                                    "maximumWarning": "2kb",
                                    "maximumError": "4kb"
                                }
                            ],
                            "outputHashing": "all",
                            "fileReplacements": [
                                {
                                    "replace": "src/environments/environment.ts",
                                    "with": "src/environments/environment.prod.ts"
                                }
                            ]
                        },
                        "development": {
                            "optimization": false,
                            "extractLicenses": false,
                            "sourceMap": true,
                            "outputHashing": "all",
                            "namedChunks": true,
                            "aot": false
                        }
                    },
                    "defaultConfiguration": "production"
                },
                "serve": {
                    "builder": "@angular-devkit/build-angular:dev-server",
                    "configurations": {
                        "production": {
                            "buildTarget": "{{appName}}:build:production"
                        },
                        "development": {
                            "buildTarget": "{{appName}}:build:development"
                        }
                    },
                    "defaultConfiguration": "development"
                },
                "extract-i18n": {
                    "builder": "@angular-devkit/build-angular:extract-i18n",
                    "options": {
                        "buildTarget": "{{appName}}:build"
                    }
                },
                "test": {
                    "builder": "@angular-devkit/build-angular:karma",
                    "options": {
                        "polyfills": [
                            "zone.js",
                            "zone.js/testing"
                        ],
                        "tsConfig": "tsconfig.spec.json",
                        "inlineStyleLanguage": "scss",
                        "assets": [
                            "src/assets"
                        ],
                        "styles": [
                            "src/assets/tailwind.css",
                            "src/assets/styles.scss"
                        ],
                        "scripts": []
                    }
                }
            }
        }
    },
    "cli": {
        "analytics": false
    }
}
""";
        }

        private static string GetEditOrConfigData()
        {
            return $$"""
# Editor configuration, see https://editorconfig.org
root = true

[*]
charset = utf-8
indent_style = space
indent_size = 4
insert_final_newline = true
trim_trailing_whitespace = true

[*.ts]
quote_type = single

[*.md]
max_line_length = off
trim_trailing_whitespace = false
""";
        }

        private static string GetPrettierRcData()
        {
            return """
{
    "printWidth": 120
}
""";
        }

        private static string GetPostCssRcJsonData()
        {
            return """
{
    "plugins": {
        "@tailwindcss/postcss": {}
    }
}
""";
        }

        private static string GetFrontendREADMEData(string appName, string spiderlyVersion)
        {
            return $$"""
# {{appName}}
This project was generated with [Spiderly CLI](https://github.com/filiptrivan/spiderly/tree/main/Spiderly.CLI) version {{spiderlyVersion}}.

## Development server
Run `ng serve` for a dev server. Navigate to http://localhost:4200/. The app will automatically reload if you change any of the source files.

## Code scaffolding
Run `ng generate component component-name` to generate a new component. You can also use `ng generate directive|pipe|service|class|module`.

### Further help
To get more help on the Angular CLI use `ng help` or go check out the [Angular CLI README](https://github.com/angular/angular-cli/blob/main/README.md).
""";
        }

        private static string GetPlaywrightConfigData(PackageManagerCodes packageManager)
        {
            string pmCommand = packageManager.GetCommandName();

            return $$"""
import { defineConfig, devices } from '@playwright/test';

export default defineConfig({
  testDir: './tests/e2e/specs',
  fullyParallel: true,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 2 : 0,
  workers: process.env.CI ? 1 : undefined,
  reporter: 'html',
  use: {
    baseURL: 'http://localhost:4200',
    trace: 'on-first-retry',
    screenshot: 'only-on-failure',
  },
  projects: [
    {
      name: 'chromium',
      use: { ...devices['Desktop Chrome'] },
    },
    {
      name: 'firefox',
      use: { ...devices['Desktop Firefox'] },
    },
    {
      name: 'webkit',
      use: { ...devices['Desktop Safari'] },
    },
  ],
  webServer: {
    command: '{{pmCommand}} start',
    url: 'http://localhost:4200',
    reuseExistingServer: !process.env.CI,
  },
});
""";
        }
    }
}
