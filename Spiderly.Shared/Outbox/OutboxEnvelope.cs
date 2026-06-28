using System.Text.Json;

namespace Spiderly.Shared.Outbox
{
    /// <summary>
    /// The serialized body of an outbox row for a typed fact: its stable <see cref="OutboxCodeAttribute"/> code plus the
    /// fact serialized to JSON — enough to rebuild it at delivery via <see cref="CodeTypeRegistry{TMarker}"/>. Integration
    /// events use this as-is; notifications extend it with channel/recipient fields. Mirrors a messaging "envelope"
    /// (type identity + body).
    /// </summary>
    public class OutboxEnvelope
    {
        /// <summary>The fact's stable code (see <see cref="OutboxCodeAttribute"/>).</summary>
        public string Code { get; set; }

        /// <summary>The fact serialized to JSON.</summary>
        public string Data { get; set; }

        /// <summary>Builds an envelope for a fact: reads its <see cref="OutboxCodeAttribute"/> code and serializes it by runtime type.</summary>
        public static OutboxEnvelope For(object fact) => new()
        {
            Code = OutboxCode.Of(fact.GetType()),
            Data = JsonSerializer.Serialize(fact, fact.GetType()),
        };
    }
}
