using System;
using System.IO;

namespace Spiderly.SourceGenerators.Shared
{
    /// <summary>
    /// The only write path for the Angular generators' <c>.generated.ts</c> files. Each file opens with a
    /// header that names its generator and the backend source to change instead. Without it, a reader in
    /// the Frontend folder sees no sign that the next backend build overwrites the file.
    /// </summary>
    public static class SpiderlyTypeScriptEmission
    {
        /// <param name="backendSource">The C# a reader changes to change this file. It follows "change its
        /// backend source: ", for example "the [SpiderlyEntity] and [SpiderlyDTO] classes".</param>
        public static void WriteSpiderlyTypeScriptFile(string path, string body, string generatorName, string backendSource)
        {
            string header = $$"""
// Spiderly's {{generatorName}} writes this file. Do not edit it, because the next backend build overwrites it.
// To change this file, change its backend source: {{backendSource}}.


""";

            WriteIfChanged(header + body.TrimStart('\r', '\n'), path);
        }

        private static void WriteIfChanged(string data, string path)
        {
            data = data.Replace("\r\n", "\n").Replace("\n", Environment.NewLine);

            if (File.Exists(path) && File.ReadAllText(path) == data)
                return;

            using StreamWriter sw = new StreamWriter(path, false);
            sw.Write(data);
        }
    }
}
