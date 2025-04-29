<div align="right">
  <img src="https://www.spiderly.dev/assets/spiderly-logo.svg" alt="Spiderly Logo" width="60"/>
</div>

# Spiderly
Spiderly is a .NET (C#) library that turns your plain C# classes into a complete .NET + Angular web apps. Automatically updating all the boilerplate code as your classes evolve. You're free to add your own logic and change anything in the generated app exactly how you want.

<ul>
  <li><b>Speed</b>: With CRUD operations, Backend/Frontend architecture, Auth, Logging, and the best Libraries already set up for you, we save you a significant amount of time so you can focus on your specific business logic.</li>
  <li><b>Accuracy</b>: Even if the generated code is boilerplate, copy-pasting without focus inevitably leads to mistakes. Spiderly eliminates this subconscious burden, freeing your mind for more important tasks.</li>
  <li><b>Customizability</b>: If you don't like any of Spiderly's functionalities (CRUD operations, backend/frontend architecture, auth, logging), you can disable any of them and implement your own.</li>
</ul>

## Installation
Follow [Get Started](https://www.spiderly.dev/#get-started) to start using Spiderly.

## Examples
With the [Playground](https://www.spiderly.dev/playground) on the Spiderly Website, you can create your own examples, live. Here is the first one to get you started:
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
  <img src="https://www.spiderly.dev/assets/spiderly-app-demo.png" alt="Spiderly Get Started App Demo"/>
</div>

## Documentation

### Spiderly.SourceGenerators
[Spiderly.SourceGenerators](https://github.com/filiptrivan/spiderly/tree/main/Spiderly.SourceGenerators) generates a lot of features in a .NET application, as well as a lot of features for Angular application, it works based on attributes on EF Core entities. The idea of ​​the generator is that programmers only have to write specific logic, and don't think about anything else.

### Spiderly.Security
[Spiderly.Security](https://github.com/filiptrivan/spiderly/tree/main/Spiderly.Security) library provides Authentication and Authorization features with JWT.

### Spiderly.Infrastructure
[Spiderly.Infrastructure](https://github.com/filiptrivan/spiderly/tree/main/Spiderly.Infrastructure) works on the basis of EF Core, provides features of optimistic version control, naming tables/attributes within the database, has extensions for easy database definition through EF Core.

### Spiderly.Shared
[Spiderly.Shared](https://github.com/filiptrivan/spiderly/tree/main/Spiderly.Shared) library provides shared features that can be used by all projects.

## Contributing
If you want to participate in this cool open source project be free to open an issue, start a discussion or make pull request.

### License
Spiderly is [MIT licensed](https://github.com/filiptrivan/spiderly/blob/main/LICENSE).
