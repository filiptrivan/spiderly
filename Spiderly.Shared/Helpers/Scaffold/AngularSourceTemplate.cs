using Spiderly.Shared.Classes;

namespace Spiderly.Shared.Helpers.Scaffold
{
    /// <summary>
    /// The Angular app's <c>src/</c> folder: the host page, the bootstrap and the environment files, with the app
    /// and its assets.
    /// </summary>
    internal static class AngularSourceTemplate
    {
        internal static SpiderlyFolder Folder(string appName, bool isRunningFromNuget) => new()
        {
            Name = "src",
            ChildFolders =
            {
                AngularAppTemplate.Folder(appName),
                AssetsTemplate.Folder(isRunningFromNuget),
                new SpiderlyFolder
                {
                    Name = "environments",
                    Files =
                    {
                        new SpiderlyFile { Name = "environment.ts", Data = GetEnvironmentTsData(appName) },
                        new SpiderlyFile { Name = "environment.prod.ts", Data = GetEnvironmentProdTsData(appName) },
                    }
                }
            },
            Files =
            {
                new SpiderlyFile { Name = "index.html", Data = GetIndexHtmlData(appName) },
                new SpiderlyFile { Name = "main.ts", Data = GetMainTsData() },
            }
        };

        private static string GetMainTsData()
        {
            return $$"""
import { bootstrapApplication } from '@angular/platform-browser';
import { AppComponent } from './app/app.component';
import { appConfig } from './app/app.config';

bootstrapApplication(AppComponent, appConfig)
  .catch((err) => console.error(err));
""";
        }

        private static string GetIndexHtmlData(string appName)
        {
            return $$"""
<!doctype html>
<html lang="en">
<head>
  <meta charset="utf-8">
  <title>{{appName}}</title>
  <meta name="description" content="{{appName}}">
  <meta name="author" content="{{appName}}">
  <base href="/">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <!-- When you add your favicon the href should be: ./assets/images/logo/favicon.ico -->
  <link rel="icon" type="image/x-icon" href="data:image/x-icon;base64,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">
</head>
<body>
  <app-root></app-root>
</body>
</html>
""";
        }

        private static string GetEnvironmentTsData(string appName)
        {
            return $$"""
export const environment = {
  production: false,
  apiUrl: 'http://localhost:5000/api',
  frontendUrl: 'http://localhost:4200',
  companyName: '{{appName}}',
};
""";
        }

        private static string GetEnvironmentProdTsData(string appName)
        {
            return $$"""
export const environment = {
  production: true,
  apiUrl: 'https://your-production-api.com/api',
  frontendUrl: 'https://your-production-frontend.com',
  companyName: '{{appName}}',
};
""";
        }
    }
}
