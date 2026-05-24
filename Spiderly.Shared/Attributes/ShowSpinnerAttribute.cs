using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spiderly.Shared.Attributes
{
    /// <summary>
    /// <b>Usage:</b> Forces the global full-screen loading spinner ON for the decorated controller method,
    /// overriding the generator's auto-skip inference. The explicit attribute always wins over inference. <br/> <br/>
    ///
    /// <b>You rarely need this.</b> The spinner is shown by default; you only reach for this attribute to
    /// re-enable it on a call the generator would otherwise auto-skip — namely a deliberately slow
    /// <c>HttpGet</c> that returns a bare scalar (which is auto-skipped because such reads are normally
    /// instant). If your endpoint does real work, it is usually a <c>POST</c> — and POSTs keep the spinner
    /// without any attribute, so prefer the correct verb over this. <br/> <br/>
    ///
    /// Do <b>not</b> put this on the read-shaped responses (<c>NamebookDTO</c>, <c>CodebookDTO</c>,
    /// <c>PaginatedResultDTO</c>, <c>LazyLoadSelectedIdsResultDTO</c>): those power autocomplete, dropdowns
    /// and table pagination, where a full-screen blackout on every keystroke / page change is a regression.
    /// (The attribute still wins there if you insist — it just shouldn't be used that way.) <br/> <br/>
    ///
    /// <b>Example</b> (a slow scalar read where the blocking overlay is wanted):
    /// <code>
    /// [HttpGet]
    /// [ShowSpinner]
    /// public async Task&lt;int&gt; RecalculateScore() // expensive, user-triggered
    /// {
    ///     return await _businessService.RecalculateScore();
    /// }
    /// </code>
    /// </summary>
    public class ShowSpinnerAttribute : Attribute
    {
    }
}
