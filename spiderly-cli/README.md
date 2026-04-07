# spiderly-cli

`spiderly-cli` is a reserved npm package for the future Spiderly CLI.

This package is intentionally a placeholder today:

- it does not provide a working `spiderly` executable
- it does not replace the current `Spiderly.CLI` NuGet global tool
- it exists so Spiderly can manage the npm package in-repo and automate releases

For the current CLI experience, install the NuGet tool:

```bash
dotnet tool install -g Spiderly.CLI
```

For the Angular library, use the `spiderly` npm package.
