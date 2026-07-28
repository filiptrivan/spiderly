using System.Text.Json;

namespace Spiderly.Shared.Outbox
{
    /// <summary>
    /// Delivery-side code↔type resolver for outbox-deliverable types of one kind (<typeparamref name="TMarker"/> =
    /// <c>INotification</c> or <c>IIntegrationEvent</c>). Built once at startup from the kind's <b>explicitly-declared</b>
    /// types — notifications: the routing-map keys; events: the registered event types — never by assembly scanning, and
    /// registered as a singleton; duplicate codes fail loud at build time. Producers read codes off the type directly
    /// (<see cref="OutboxCode.Of"/>); this registry is consulted only at delivery, to rebuild a fact from its row.
    /// </summary>
    /// <typeparam name="TMarker">The kind's marker interface (e.g. <c>INotification</c>, <c>IIntegrationEvent</c>).</typeparam>
    public sealed class CodeTypeRegistry<TMarker>
        where TMarker : class
    {
        private readonly Dictionary<string, Type> _byCode = new();

        /// <summary>Builds the registry from the kind's explicitly-declared types (each must carry <see cref="OutboxCodeAttribute"/>).</summary>
        public CodeTypeRegistry(IEnumerable<Type> types)
        {
            foreach (Type type in types)
            {
                if (!typeof(TMarker).IsAssignableFrom(type))
                    throw new InvalidOperationException(
                        $"{type.Name} is registered as a {typeof(TMarker).Name} but does not implement it.");

                string code = OutboxCode.Of(type);
                if (!_byCode.TryAdd(code, type))
                    throw new InvalidOperationException(
                        $"Duplicate [OutboxCode(\"{code}\")] on {_byCode[code].Name} and {type.Name} " +
                        $"(both {typeof(TMarker).Name}). Codes must be unique per kind.");
            }
        }

        /// <summary>Returns the type for a code, or throws if none is registered for it.</summary>
        public Type ResolveType(string code) =>
            _byCode.TryGetValue(code, out Type? type) ? type
            : throw new InvalidOperationException(
                $"No {typeof(TMarker).Name} registered for outbox code '{code}'. " +
                $"An [OutboxCode] may have been renamed/removed while a row referencing it is still pending.");

        /// <summary>True if a type is registered for <paramref name="code"/>. Producer-side preflight so an unregistered fact can fail before it's staged (rather than dead-lettering at delivery).</summary>
        public bool IsRegistered(string code) => _byCode.ContainsKey(code);

        /// <summary>Rebuilds a fact from its code + JSON data (deserialize against the resolved type).</summary>
        public TMarker Rebuild(string code, string data) =>
            (TMarker?)JsonSerializer.Deserialize(data, ResolveType(code))
            ?? throw new InvalidOperationException($"Outbox fact '{code}' deserialized to null.");
    }
}
