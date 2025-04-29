<div align="right">
  <img src="https://github.com/filiptrivan/spiderly/blob/main/spiderly-logo.svg" alt="Spiderly Logo" width="60"/>
</div>

# Spiderly
Spiderly is a .NET (C#) library that turns your plain C# classes into a complete .NET + Angular web apps. Automatically updating all the boilerplate code as your classes evolve. You're free to add your own logic and change anything in the generated app exactly how you want.

<ul>
  <li><b>Speed</b>: With CRUD operations, backend/frontend architecture, authentication, authorization, logging, and the best libraries already set up for you, we save you a significant amount of time so you can focus on your specific business logic.</li>
  <li><b>Accuracy</b>: Even if the generated code is boilerplate, copy-pasting without focus inevitably leads to mistakes. Spiderly eliminates this subconscious burden, freeing your mind for more important tasks.</li>
  <li><b>Customizability</b>: If you don't like any of Spiderly's functionalities (CRUD operations, backend/frontend architecture, auth, logging), you can disable any of them and implement your own.</li>
</ul>

## Installation
Follow [Get Started](https://www.spiderly.dev/#get-started) to start using Spiderly.

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
  <img src="https://github.com/filiptrivan/spiderly/blob/main/spiderly-app-demo.png" alt="Spiderly Get Started App Demo"/>
</div>

## Documentation

### Spiderly.SourceGenerators
[Spiderly.SourceGenerators](https://github.com/filiptrivan/spiderly/tree/main/Spiderly.SourceGenerators) generates a lot of features for both .NET and Angular apps by using attributes on EF Core entities. Its goal is to let developers focus solely on writing specific logic, without worrying about boilerplate code.

### Spiderly.Security
[Spiderly.Security](https://github.com/filiptrivan/spiderly/tree/main/Spiderly.Security) provides authentication and authorization features with JWT.

### Spiderly.Infrastructure
[Spiderly.Infrastructure](https://github.com/filiptrivan/spiderly/tree/main/Spiderly.Infrastructure) is built on EF Core and offers features such as optimistic concurrency control, customizable table and column naming, and extensions for simplified database configuration.

### Spiderly.Shared
[Spiderly.Shared](https://github.com/filiptrivan/spiderly/tree/main/Spiderly.Shared) provides shared features that can be used by all other Spiderly libraries.

## Contributing
If you want to participate in this cool open source repo be free to open an issue, start a discussion or make pull request.

### License
Spiderly is [MIT licensed](https://github.com/filiptrivan/spiderly/blob/main/LICENSE).
