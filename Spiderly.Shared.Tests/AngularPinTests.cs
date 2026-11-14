using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Spiderly.Shared.Helpers;

namespace Spiderly.Shared.Tests
{
    /// <summary>
    /// Every runtime dependency in <c>Angular/package.json</c> is pinned exact, so the lock regenerates the same
    /// way on every install. A caret or a tilde floats to a newer patch on a fresh install, and that install dies
    /// on an ERESOLVE peer conflict with the pinned compiler. <c>spiderly init</c> also gives a new app each
    /// library peer at the version pinned here (<c>AngularLibraryPackage</c>), so a range floats there too. A
    /// runtime <c>@angular/*</c> the init template does not emit is refused: the library's peer dependencies
    /// list every one, so each consumer app needs it.
    /// </summary>
    public class AngularPinTests
    {
        [Fact]
        public void Library_pins_every_runtime_angular_package_at_the_init_template_version()
        {
            string library = RepoFiles.Read("Angular/package.json");

            Assert.NotEmpty(RuntimeAngularPins(library));
            Assert.Empty(PinViolations(library, TemplatePackageJson()));
        }

        [Theory]
        [InlineData("^19.2.13")]
        [InlineData("~19.2.13")]
        [InlineData("19.2.x")]
        [InlineData("0.0.1")]
        public void A_floating_pin_or_a_version_the_init_template_does_not_emit_is_refused(string corePin)
        {
            JsonNode library = JsonNode.Parse(RepoFiles.Read("Angular/package.json"))!;
            library["dependencies"]!["@angular/core"] = corePin;

            string violation = Assert.Single(PinViolations(library.ToJsonString(), TemplatePackageJson()));
            Assert.StartsWith("@angular/core ", violation);
        }

        [Fact]
        public void A_runtime_angular_package_the_init_template_does_not_emit_is_refused()
        {
            JsonNode library = JsonNode.Parse(RepoFiles.Read("Angular/package.json"))!;
            library["dependencies"]!["@angular/cdk"] = "19.2.13";

            string violation = Assert.Single(PinViolations(library.ToJsonString(), TemplatePackageJson()));
            Assert.StartsWith("@angular/cdk ", violation);
        }

        [Fact]
        public void Library_pins_every_runtime_dependency_exact()
        {
            List<string> ranges = Ranges(RepoFiles.Read("Angular/package.json"));

            Assert.True(ranges.Count == 0,
                $"Pin each runtime dependency in Angular/package.json exact, then regenerate its lock: {string.Join("; ", ranges)}.");
        }

        [Fact]
        public void NegativeControl_a_range_on_a_runtime_dependency_outside_angular_is_reported()
        {
            JsonNode library = JsonNode.Parse(RepoFiles.Read("Angular/package.json"))!;
            library["dependencies"]!["marked"] = "^15.0.0";

            Assert.Equal(new List<string> { "marked is \"^15.0.0\", a range" }, Ranges(library.ToJsonString()));
        }

        private static readonly Regex ExactVersion = new(@"^\d+\.\d+\.\d+(-[0-9A-Za-z.-]+)?$");

        private static List<string> PinViolations(string libraryPackageJson, string templatePackageJson)
        {
            Dictionary<string, string> emitted = RuntimeAngularPins(templatePackageJson);
            List<string> violations = new();

            foreach ((string name, string pin) in RuntimeAngularPins(libraryPackageJson))
            {
                if (!ExactVersion.IsMatch(pin))
                    violations.Add($"{name} is \"{pin}\", a range. Pin it exact: spiderly init gives a new app the version pinned here.");
                else if (!emitted.TryGetValue(name, out string? templatePin))
                    violations.Add($"{name} is not in the init template. Add it to the peerDependencies of Angular/projects/spiderly/package.json: spiderly init gives a new app each peer.");
                else if (pin != templatePin)
                    violations.Add($"{name} is {pin}, and the init template emits {templatePin}. Rebuild Spiderly.Shared: the init template reads the pins from the copy of Angular/package.json that it embeds.");
            }

            return violations;
        }

        /// <summary>Each runtime dependency of <paramref name="packageJson"/> whose version is a range.</summary>
        private static List<string> Ranges(string packageJson) =>
            RuntimeDependencies(packageJson)
                .Where(dependency => !ExactVersion.IsMatch(dependency.Value))
                .Select(dependency => $"{dependency.Key} is \"{dependency.Value}\", a range")
                .ToList();

        private static Dictionary<string, string> RuntimeDependencies(string packageJson) =>
            JsonNode.Parse(packageJson)!["dependencies"]!.AsObject()
                .ToDictionary(dependency => dependency.Key, dependency => dependency.Value!.GetValue<string>(), StringComparer.Ordinal);

        private static Dictionary<string, string> RuntimeAngularPins(string packageJson) =>
            RuntimeDependencies(packageJson)
                .Where(dependency => dependency.Key.StartsWith("@angular/", StringComparison.Ordinal))
                .ToDictionary(dependency => dependency.Key, dependency => dependency.Value, StringComparer.Ordinal);

        private static string TemplatePackageJson() =>
            NetAndAngularFilesGenerator.GetPackageJsonData("App", "0.0.0", isRunningFromNuget: true);
    }
}
