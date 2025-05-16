using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spiderly.Shared.Attributes
{
    /// <summary>
    /// <b>Usage:</b> Indicates that the loading spinner should be skipped for the decorated controller method. <br/> <br/>
    /// 
    /// <b>Use when:</b>
    /// - The operation is very quick and doesn't need a loading indicator <br/>
    /// - You want to implement custom loading behavior <br/>
    /// - The operation runs in the background <br/> <br/>
    /// 
    /// <b>Example:</b>
    /// <code>
    /// [HttpGet]
    /// [SkipSpinner]
    /// public async Task SendNotificationEmail(long notificationId)
    /// {
    ///     await SendNotificationEmail(notificationId);
    /// }
    /// </code>
    /// </summary>
    public class SkipSpinnerAttribute : Attribute
    {
    }
}
