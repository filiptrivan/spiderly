<div align="right">
  <img src="https://github.com/filiptrivan/spiderly/blob/main/spiderly-logo.svg" alt="Spiderly Logo" width="60"/>
</div>

# Spiderly
Spiderly is a .NET (C#) code generation library that transforms an EF Core model into a fully customizable .NET (C#) + Angular web application, automatically updating all repetitive boilerplate code as your model evolves.

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

## Installation
Follow [Getting Started guide](https://www.spiderly.dev/docs/getting-started) to start using Spiderly.

## Examples
With the [Playground](https://www.spiderly.dev/playground) on the [Spiderly Website](https://www.spiderly.dev), you can create your own examples, live. Here is the first one to get you started:
```csharp
[TranslatePluralEn("Users")]
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
[TranslatePluralEn("Genders")]
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
For detailed documentation, please visit the [official documentation page](https://www.spiderly.dev/docs/getting-started) on our website.

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

## Contributing
We welcome contributions from the community! Whether you have ideas, found a bug, or want to add a new feature — feel free to get involved. You can:
- [Open an issue](https://github.com/filiptrivan/spiderly/issues) to report bugs or suggest enhancements
- [Submit a pull request](https://github.com/filiptrivan/spiderly/pulls) with your proposed changes
- [Start a discussion](https://github.com/filiptrivan/spiderly/discussions) to explore ideas or ask questions

Every contribution is appreciated and helps make this project better for everyone.

### Getting Started as a Contributor
To set up your development environment for contributing to Spiderly, follow these steps:
1. **Choose a Working Directory**
   - Select a location on your local machine where you want to store the project files. For example, you might choose: `C:\Users\your-name\Documents`
2. **Clone the Spiderly Repository**
   - Open a terminal or Git Bash, navigate to your chosen directory, and clone the repository:
  ```bash
  git clone https://github.com/your-org/spiderly.git
  ```
3. **Create a New Project Directory**
   - Inside your chosen working directory, create a folder named `SpiderlyProjects`.
4. **Generate a New App**
   - Make sure to create the new app inside the `SpiderlyProjects` directory you just made by following the official [Spiderly Getting Started Guide](https://www.spiderly.dev/docs/getting-started).
5. **Configure the Backend Project (Visual Studio)**
   - Open the backend project of the app you generated in step 4 using Visual Studio.
   - Locate all `.csproj` files.
   - Delete any NuGet package references to Spiderly components.
   - Uncomment the lines that reference local Spiderly projects using project references (typically `<ProjectReference>` tags).
6. **Configure the Frontend Project (Visual Studio Code)**
   - Open the frontend project of the app you generated in step 4 using Visual Studio Code.
   - Locate the `tsconfig.json` file.
   - Uncomment the 7th line that sets up the path mapping to your local Spiderly Angular library.
   - This enables your app to reference the local version of Spiderly Angular instead of the published package.

#### Developing and Testing `Spiderly.CLI`
If you want to make changes to the `Spiderly.CLI` project and test them immediately, run the PowerShell script `cli-local-pack.ps1` located in that project. You’ll need to execute this script each time you want to test your changes.

### Good First Issues
To help you get your feet wet and get you familiar with our contribution process, we have a [list of good first issues](https://github.com/filiptrivan/spiderly/issues?q=is%3Aissue%20state%3Aopen%20label%3A"good%20first%20issue"), this is a great place to get started.

### License
Spiderly is [MIT licensed](https://github.com/filiptrivan/spiderly/blob/main/LICENSE).
