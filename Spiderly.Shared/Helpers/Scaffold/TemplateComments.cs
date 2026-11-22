namespace Spiderly.Shared.Helpers.Scaffold
{
    /// <summary>
    /// Comments a block of a template out when a switch is on. A project built from the local
    /// Spiderly sources and one built from the NuGet packages comment out each other's references.
    /// </summary>
    internal static class TemplateComments
    {
        internal static string XmlCommented(string input, bool shouldComment)
        {
            if (shouldComment)
            {
                return $"<!-- {input} -->";
            }

            return input;
        }

        internal static string SlashCommented(string input, bool shouldComment)
        {
            if (shouldComment)
            {
                return $"/* {input} */";
            }

            return input;
        }
    }
}
