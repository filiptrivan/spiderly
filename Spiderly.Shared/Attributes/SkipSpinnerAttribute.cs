using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spiderly.Shared.Attributes
{
    /// <summary>
    /// <b>Usage:</b> Indicates that the global full-screen loading spinner should be skipped for the
    /// decorated controller method. <br/> <br/>
    ///
    /// <b>You usually don't need this.</b> The generator already skips the spinner automatically for
    /// read-shaped responses (<c>NamebookDTO</c>, <c>CodebookDTO</c>, <c>PaginatedResultDTO</c>,
    /// <c>LazyLoadSelectedIdsResultDTO</c>) and for any <c>HttpGet</c> that returns a bare scalar
    /// (<c>int</c>, <c>long</c>, <c>bool</c>, <c>decimal</c>, <c>DateTime</c>, …). Reach for this attribute
    /// only when the inference can't see your intent. For the inverse — forcing the spinner back ON for a
    /// call the inference auto-skips — use <see cref="ShowSpinnerAttribute"/>. <br/> <br/>
    ///
    /// <b>Use when:</b>
    /// - A GET returns a full DTO but is polled / refreshed on a timer (e.g. a dashboard fetched every minute) <br/>
    /// - You want to implement custom loading behavior <br/>
    /// - The operation runs in the background <br/> <br/>
    ///
    /// <b>Example</b> (a polled DTO — a bare-scalar count GET like
    /// <c>GetUnreadNotificationsCountForCurrentUser</c> would be auto-skipped without this):
    /// <code>
    /// [HttpGet]
    /// [SkipSpinner]
    /// public async Task&lt;DashboardStatsDTO&gt; GetDashboardStats() // refreshed every 60s on the client
    /// {
    ///     return await _businessService.GetDashboardStats();
    /// }
    /// </code>
    /// </summary>
    public class SkipSpinnerAttribute : Attribute
    {
    }
}
