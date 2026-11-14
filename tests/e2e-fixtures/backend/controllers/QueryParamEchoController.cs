using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Spiderly.Shared.Attributes;
using Spiderly.Shared.Helpers;

namespace __APP_NAME__.WebAPI.Controllers
{
    /// <summary>
    /// Answers with the value the model binder read from a query param, so query-param-echo.spec.ts can
    /// compare it with what the generated API service sent.
    /// </summary>
    [SpiderlyController]
    [ApiController]
    [Route("/api/[controller]/[action]")]
    public class QueryParamEchoController : SpiderlyBaseController
    {
        [HttpGet]
        public string EchoDateTimeQueryParam(DateTime at)
        {
            return at.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
        }
    }
}
