using System.Collections.Concurrent;
using System.Reflection;

namespace Spiderly.Shared.Outbox
{
    /// <summary>
    /// Assigns a stable code to an outbox-deliverable type — a notification
    /// (<see cref="Spiderly.Shared.Interfaces.INotification"/>) or an integration event
    /// (<see cref="Spiderly.Shared.Interfaces.IIntegrationEvent"/>) — so it can be persisted to an outbox row and rebuilt
    /// later regardless of class renames or moves. The code travels in the row; the producer reads it off the type via
    /// <see cref="OutboxCode.Of"/>, and delivery resolves it back to the type via <see cref="CodeTypeRegistry{TMarker}"/>.
    /// Codes must be unique per kind.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public sealed class OutboxCodeAttribute : Attribute
    {
        /// <summary>Assigns the stable code.</summary>
        /// <param name="code">A short, stable identifier (e.g. <c>"OrderCreated"</c>). Must be unique across types of the same kind.</param>
        public OutboxCodeAttribute(string code)
        {
            Code = code;
        }

        /// <summary>The stable code.</summary>
        public string Code { get; }
    }

    /// <summary>
    /// Producer-side helper: reads a type's stable <see cref="OutboxCodeAttribute"/> code. Used when staging a fact to the
    /// outbox (harvest / publish / notify), so the producer never needs a registry — only delivery does.
    /// </summary>
    public static class OutboxCode
    {
        // Memoized per Type — the attribute is immutable and Of() runs on the harvest/dispatch paths.
        private static readonly ConcurrentDictionary<Type, string> _codes = new();

        /// <summary>Returns the type's <see cref="OutboxCodeAttribute.Code"/> (memoized per type), or throws if it carries none.</summary>
        public static string Of(Type type) =>
            _codes.GetOrAdd(type, static t =>
                t.GetCustomAttribute<OutboxCodeAttribute>()?.Code
                ?? throw new InvalidOperationException(
                    $"{t.Name} is missing [OutboxCode(\"...\")] (required to deliver it via the outbox)."));
    }
}
