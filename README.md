<div align="right">
  <img src="https://github.com/filiptrivan/spiderly/blob/main/spiderly-logo.svg" alt="Spiderly Logo" width="60"/>
</div>

# Spiderly

Spiderly is a free, open-source .NET (C#) code generator that transforms an EF Core model into a fully customizable .NET (C#) + Angular web application, automatically updating all boilerplate code as your model evolves.

## Key Generation Features

- **CRUD Generator**  
  For each EF Core entity, the generator creates:
  - CRUD UI
  - Angular API client
  - .NET controllers
  - Service methods to interact with the database

- **CRUD UI Generator**  
  For each EF Core entity, the generator creates:
  - A table view page — displays records with sorting, filtering, and pagination
  - An admin page — a form for creating and editing records

- **API Client Generator**  
  Generates an Angular service class with methods that match your .NET controllers. Each method corresponds to a controller action and includes strongly typed parameters and responses based on your DTO classes.

- **Shared .NET and Angular Validations**  
  Generates .NET FluentValidation rules and matching Angular reactive form validators. Both sides stay in sync while allowing separate customization if needed.

- **C# DTO and TypeScript Classes**  
  Generates C# partial DTO classes and matching Angular TypeScript classes with strongly typed constructors.

- **.NET + Angular App Starter**  
  Sets up the .NET (C#) and Angular app template with built-in support for: authentication (including Google Sign-In), authorization, emailing, logging, global error handling, and more.

## Getting Started

Follow this quick start guide to see which prerequisites you need to install and how to initialize your Spiderly app. For the full guide, visit the [official getting started page](https://www.spiderly.dev/docs/getting-started).

### Install Prerequisites

Before getting started with Spiderly, make sure you have the following prerequisites installed:

- [Visual Studio Code](https://code.visualstudio.com/)
- [.NET 9.0](https://dotnet.microsoft.com/)
- [PostgreSQL](https://www.postgresql.org/) or [SQL Server](https://www.microsoft.com/en-us/sql-server)
- [Node.js](https://nodejs.org/)
- [C# Dev Kit](https://marketplace.visualstudio.com/items?itemName=ms-dotnettools.csdevkit) (VS Code extension)

Run the Spiderly CLI installation command from any terminal location:

```
dotnet tool install -g Spiderly.CLI
```

### Initialize the App

Open a terminal in the folder **where you want your app to be created** and run:

```
spiderly init
```

This will create a new folder with your app name containing the full Spiderly project structure.

### Open the Project

Navigate into your newly created app folder and open it in VS Code:

```
cd your-app-name
code .
```

> If the `code .` command doesn't work, open your newly created app folder manually in VS Code.

### Start the App

Press `F5` to start the app.

### Register the User

Use the UI of your generated app to register a new user via email.

## Examples

With the [Interactive Demo](https://www.spiderly.dev/#interactive-demo), you can run the `spiderly init` command and add dummy properties to see how Spiderly works. Here is the first example to get you started:

```csharp
public class User
{
    [Required]
    public long Id { get; set; }

    [DisplayName]
    [Required]
    public string Name { get; set; }

    [UIControlWidth("col-12")]
    public Gender Gender { get; set; }

    [UIControlType("File")]
    public string Logo { get; set; }
}
```

```csharp
public class Gender
{
    [Required]
    public long Id { get; set; }

    [DisplayName]
    [Required]
    public string Name { get; set; }
}
```

These two classes alone will generate app like this:

<div>
  <img src="https://github.com/filiptrivan/spiderly/blob/main/spiderly-app-demo.png" alt="Spiderly Getting Started App Demo"/>
</div>

## Documentation

For detailed documentation, please visit the [official documentation page](https://www.spiderly.dev/docs) on our website.

### Spiderly.CLI

By using the [Spiderly.CLI](https://github.com/filiptrivan/spiderly/tree/main/Spiderly.CLI), you properly initialize the app, allowing all other Spiderly libraries to function.

### Spiderly.SourceGenerators

[Spiderly.SourceGenerators](https://github.com/filiptrivan/spiderly/tree/main/Spiderly.SourceGenerators) generates a lot of features for both .NET and Angular apps by using attributes on EF Core entities. Its goal is to let developers focus solely on writing specific logic, without worrying about boilerplate code.

### Spiderly.Security

[Spiderly.Security](https://github.com/filiptrivan/spiderly/tree/main/Spiderly.Security) provides authentication and authorization features with JWT.

### Spiderly.Infrastructure

[Spiderly.Infrastructure](https://github.com/filiptrivan/spiderly/tree/main/Spiderly.Infrastructure) is built on EF Core and offers features such as optimistic concurrency control, customizable table and column naming, and extensions for simplified database configuration.

### Spiderly.Shared

[Spiderly.Shared](https://github.com/filiptrivan/spiderly/tree/main/Spiderly.Shared) provides shared features that can be used by all other Spiderly libraries.

## Claude Code Plugin

Spiderly includes a [Claude Code](https://claude.ai/code) plugin that gives Claude framework-specific knowledge — entity attributes, lifecycle hooks, migration commands, and filtering patterns.

**Install:**

```bash
claude plugin add filiptrivan/spiderly
```

**What's included:**

| Type | Name | Description |
|------|------|-------------|
| Skill | `entity-design` | Correct attributes, relationships, UI mappings |
| Skill | `backend-hooks` | Lifecycle hook signatures, execution order, MARS pitfall |
| Skill | `migration-workflow` | Spiderly CLI commands, what needs migrations |
| Skill | `filtering-patterns` | FilterDTO, paginated list overrides, AdditionalFilterId |
| Command | `/spiderly:add-entity` | Guided end-to-end entity scaffolding |

## Contributing

We welcome contributions from the community! Whether you have ideas, found a bug, or want to add a new feature — feel free to get involved. You can:

- [Open an issue](https://github.com/filiptrivan/spiderly/issues) to report bugs or suggest enhancements
- [Submit a pull request](https://github.com/filiptrivan/spiderly/pulls) with your proposed changes
- [Start a discussion](https://github.com/filiptrivan/spiderly/discussions) to explore ideas or ask questions

Every contribution is appreciated and helps make this project better for everyone.

### Getting Started as a Contributor

To set up your development environment for contributing to Spiderly, follow these steps:

1. **Make Sure All Prerequisites Are Installed**
   - You can find the full list of prerequisites in the "Install Prerequisites" section of the official [Spiderly getting started guide](https://www.spiderly.dev/docs/getting-started). Make sure everything is installed before moving on.
2. **Choose a Working Directory**
   - Select a location on your local machine where you want to store the project files. For example, you might choose: `C:\Users\your-name\Documents`
3. **Clone the Spiderly Repository**
   - Open your terminal or Git Bash and run:
     ```bash
     git clone https://github.com/filiptrivan/spiderly.git
     ```
4. **Install Angular Dependencies**
   - Navigate to the Angular project folder:
     ```bash
     cd spiderly/Angular
     ```
   - Install the required npm packages:
     ```bash
     npm install
     ```
5. **Install the Global Spiderly CLI Tool**
   - Run the `spiderly\Spiderly.CLI\cli-local-pack.ps1` PowerShell script.
6. **Initialize a New Spiderly Test App in Development Mode**
   - This step creates a new test application that will serve as a sandbox environment for testing and developing the Spiderly library. It allows you to see changes in real time as you work on the core library.
   - Run the following command in your working directory (e.g. `C:\Users\your-name\Documents`):
     ```bash
     spiderly init --dev
     ```
7. **Finish Setting Up the Spiderly Test App**
   - Start from the "Start the App" section of the official [Spiderly getting started guide](https://www.spiderly.dev/docs/getting-started), as the previous steps have already been completed.

Any changes made to the Spiderly source code will now be reflected in your newly created Spiderly test app. You shouldn't manually build or start the Spiderly library—changes will automatically reflect in the test app each time you save a file.

You’re all set! If you run into any issues during setup, feel free to [open a GitHub issue](https://github.com/filiptrivan/spiderly/issues/new). A [maintainer](https://github.com/filiptrivan) will respond as soon as possible, your feedback helps improve the experience for future contributors!

#### Developing and Testing `Spiderly.CLI`

If you want to make changes to the `Spiderly.CLI` project and test them immediately, run the PowerShell script `cli-local-pack.ps1` located in that project. You’ll need to execute this script each time you want to test your changes.

### Good First Issues

To help you get your feet wet and get you familiar with our contribution process, we have a [list of good first issues](https://github.com/filiptrivan/spiderly/issues?q=is%3Aissue%20state%3Aopen%20label%3A"good%20first%20issue"), this is a great place to get started.

### License

Spiderly is [MIT licensed](https://github.com/filiptrivan/spiderly/blob/main/LICENSE).
