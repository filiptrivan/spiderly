namespace Spiderly.SourceGenerators.Shared
{
    /// <summary>
    /// The single write path for the Angular generators' <c>.generated.ts</c> files. Each file opens with a
    /// header that names its generator and the backend source to change instead. A reader who opens the
    /// file in the Frontend folder sees no other sign that the next backend build overwrites it.
    /// </summary>
    public static class SpiderlyTypeScriptEmission
    {
        /// <param name="backendSource">The C# a reader changes to change this file, worded to follow
        /// "change its backend source: ", for example "the [SpiderlyEntity] and [SpiderlyDTO] classes".</param>
        public static void WriteSpiderlyTypeScriptFile(string path, string body, string generatorName, string backendSource)
        {
            string header = $$"""
// Spiderly's {{generatorName}} writes this file. Do not edit it, because the next backend build overwrites a hand edit.
// To change this file, change its backend source: {{backendSource}}.


""";

            Helpers.WriteToTheFile(header + body.TrimStart('\r', '\n'), path);
        }
    }
}
