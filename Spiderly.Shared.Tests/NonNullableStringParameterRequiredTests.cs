using System.Net;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Spiderly.Shared.Extensions;

namespace Spiderly.Shared.Tests
{
    /// <summary>
    /// Pins that an action's non-nullable <c>string</c> parameter is required at the HTTP boundary,
    /// through the real MVC pipeline <see cref="StartupExtensions.SpiderlyAddControllers"/> builds.
    /// <para>
    /// <see cref="ImplicitRequiredSuppressionTests"/> turns MVC's implicit <c>[Required]</c> off, so that a
    /// null field of a request body reaches the generated FluentValidation rules and their 422. That
    /// switch also covers action parameters. Then a <c>string q</c> parameter arrives as null when the
    /// query has no <c>q</c>, or has <c>?q=</c>, since MVC reads an empty value as null. The compiler
    /// trusts the annotation, so the action dereferences a null.
    /// </para>
    /// <para>
    /// It shipped: a consumer's nullable pass changed <c>characters?.Trim()</c> to
    /// <c>characters.Trim()</c> behind <c>Search(string q)</c>. The storefront's search box asks for
    /// <c>?q=</c> on open, so every search box said "search is not available" (2026-11-17).
    /// </para>
    /// <para>
    /// So a parameter's annotation is what the boundary enforces: a non-nullable <c>string</c> without a
    /// default answers 400 when it is missing or empty, and an optional one is <c>string?</c>, whose null
    /// the compiler makes each reader handle. A body property keeps the 422 path.
    /// </para>
    /// </summary>
    public class NonNullableStringParameterRequiredTests
    {
        private static TestServer BuildServer()
        {
            IWebHostBuilder builder = new WebHostBuilder()
                .ConfigureServices(services =>
                {
                    services.SpiderlyAddControllers();
                    services.AddControllers().AddApplicationPart(typeof(ParameterProbeController).Assembly);
                })
                .Configure(app =>
                {
                    app.UseRouting();
                    app.UseEndpoints(endpoints => endpoints.MapControllers());
                });

            return new TestServer(builder);
        }

        private static async Task<HttpStatusCode> Get(string path)
        {
            using TestServer server = BuildServer();
            HttpResponseMessage response = await server.CreateClient().GetAsync(path);
            return response.StatusCode;
        }

        [Theory]
        [InlineData("/parameter-probe/required")]
        [InlineData("/parameter-probe/required?q=")]
        [InlineData("/parameter-probe/required?q=%20")]
        public async Task A_non_nullable_string_parameter_missing_or_empty_answers_400(string path)
        {
            Assert.Equal(HttpStatusCode.BadRequest, await Get(path));
        }

        [Theory]
        [InlineData("/parameter-probe/required?q=drill")]
        [InlineData("/parameter-probe/optional")]
        [InlineData("/parameter-probe/optional?q=")]
        [InlineData("/parameter-probe/defaulted")]
        public async Task A_given_value_an_optional_parameter_and_a_defaulted_one_answer_200(string path)
        {
            Assert.Equal(HttpStatusCode.OK, await Get(path));
        }

        [Fact]
        public async Task A_body_property_sent_null_still_reaches_the_action()
        {
            using TestServer server = BuildServer();
            StringContent body = new("""{"name":null}""", Encoding.UTF8, "application/json");

            HttpResponseMessage response = await server.CreateClient().PostAsync("/parameter-probe/body", body);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }

    [ApiController]
    [Route("parameter-probe/[action]")]
    public class ParameterProbeController : ControllerBase
    {
        [HttpGet]
        public string Required(string q) => q.Trim();

        [HttpGet]
        public string Optional(string? q) => q ?? "";

        [HttpGet]
        public string Defaulted(string q = "all") => q;

        [HttpPost]
        public string Body(ParameterProbeBody body) => body.Name ?? "";
    }

    public class ParameterProbeBody
    {
        public string Name { get; set; } = null!;
    }
}
