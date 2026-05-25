using System.Reflection;
using Spiderly.Shared.Interfaces;

namespace Spiderly.Shared.Notifications
{
    /// <summary>
    /// Maps a notification's stable <see cref="NotificationCodeAttribute.Code"/> ↔ its CLR type, so a persisted
    /// delivery row can be rebuilt into the right notification class. Built once at startup and registered as a
    /// singleton; duplicate codes fail loud at build time.
    /// </summary>
    public class NotificationTypeRegistry
    {
        private readonly Dictionary<string, Type> _byCode = new();
        private readonly Dictionary<Type, string> _byType = new();

        /// <summary>Builds the registry from the given notification types (each must carry <see cref="NotificationCodeAttribute"/>).</summary>
        public NotificationTypeRegistry(IEnumerable<Type> notificationTypes)
        {
            foreach (Type type in notificationTypes)
            {
                NotificationCodeAttribute attribute = type.GetCustomAttribute<NotificationCodeAttribute>();
                if (attribute == null)
                    continue;

                if (_byCode.TryGetValue(attribute.Code, out Type existing))
                    throw new InvalidOperationException(
                        $"Duplicate [NotificationCode(\"{attribute.Code}\")] on {existing.Name} and {type.Name}. Codes must be unique.");

                _byCode.Add(attribute.Code, type);
                _byType.Add(type, attribute.Code);
            }
        }

        /// <summary>Scans the given assemblies for <see cref="INotification"/> types carrying <see cref="NotificationCodeAttribute"/>.</summary>
        public static NotificationTypeRegistry Discover(IEnumerable<Assembly> assemblies)
        {
            List<Type> types = new();
            foreach (Assembly assembly in assemblies)
            {
                Type[] assemblyTypes;
                try { assemblyTypes = assembly.GetTypes(); }
                catch (ReflectionTypeLoadException ex) { assemblyTypes = ex.Types.Where(t => t != null).ToArray(); }

                foreach (Type type in assemblyTypes)
                {
                    if (typeof(INotification).IsAssignableFrom(type)
                        && type.GetCustomAttribute<NotificationCodeAttribute>() != null)
                    {
                        types.Add(type);
                    }
                }
            }
            return new NotificationTypeRegistry(types);
        }

        /// <summary>Returns the code for a notification type, or throws if it has no <see cref="NotificationCodeAttribute"/>.</summary>
        public string GetCode(Type notificationType)
        {
            if (_byType.TryGetValue(notificationType, out string code))
                return code;

            throw new InvalidOperationException(
                $"Notification {notificationType.Name} is missing a [NotificationCode(\"...\")] attribute (required to deliver it via the outbox or asynchronously).");
        }

        /// <summary>Returns the notification type for a code, or throws if no notification is registered for it.</summary>
        public Type GetNotificationType(string code)
        {
            if (_byCode.TryGetValue(code, out Type type))
                return type;

            throw new InvalidOperationException(
                $"No notification registered for code '{code}'. A [NotificationCode] may have been renamed/removed while a row referencing it is still pending.");
        }
    }
}
