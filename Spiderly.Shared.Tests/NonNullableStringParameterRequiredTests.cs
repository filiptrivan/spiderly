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
    /// Why the rule exists: <see cref="Helpers.RequiredStringParameterMetadataProvider"/>.
    /// <para>
    /// It shipped: a consumer's nullable pass changed <c>characters?.Trim()</c> to
    /// <c>characters.Trim()</c> behind <c>Search(string q)</c>. The storefront's search box asks for
    /// <c>?q=</c> on open. So every search box said "search is not available" (2026-11-17).
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
        [InlineData("/parameter-probe/oblivious?q=")]
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

#nullable disable
        [HttpGet]
        public string Oblivious(string q) => q ?? "";
#nullable enable

        [HttpPost]
        public string Body(ParameterProbeBody body) => body.Name ?? "";
    }

    public class ParameterProbeBody
    {
        public string Name { get; set; } = null!;
    }
}
