using Microsoft.AspNetCore.Mvc;
using Spiderly.Shared.Attributes;
using Spiderly.Shared.Helpers;

namespace __APP_NAME__.WebAPI.Controllers
{
    /// <summary>
    /// Its [Route] holds two literal segments and no [controller]. So the backend serves /api/Echo/Route/&lt;Action&gt;,
    /// and not /api/RouteEcho/&lt;Action&gt;. route-echo.spec.ts checks that the generated API service calls the URL
    /// that the backend serves (NgControllersGenerator.GetControllerUrlPath).
    /// </summary>
    [SpiderlyController]
    [ApiController]
    [Route("/api/Echo/Route/[action]")]
    public class RouteEchoController : SpiderlyBaseController
    {
        [HttpGet]
        public string EchoControllerRoute() => "served";
    }
}
