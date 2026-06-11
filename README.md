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
