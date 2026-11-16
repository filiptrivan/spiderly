using Spiderly.Shared.Classes;
using Spiderly.Shared.Helpers.Scaffold;

namespace Spiderly.Shared.Helpers
{
    /// <summary>
    /// The templates of the Spiderly CLI: the project <c>spiderly init</c> scaffolds, and the pages
    /// <c>spiderly add-new-entity</c> writes. The templates are in <c>Helpers/Scaffold/</c>, and
    /// <see cref="ProjectRootTemplate"/> builds the project from them. A folder's class builds that folder and calls
    /// the classes of the folders in it. So the layout of a folder stays beside its templates.
    /// </summary>
    public static class NetAndAngularFilesGenerator
    {
        /// <summary>
        /// The entity source files <c>spiderly init</c> scaffolds into
        /// <c>Backend/{AppName}.Business/Entities/</c>.
        /// <para>
        /// Internal (not public) so a test can compile the REAL template entities rather than keep a copy that
        /// drifts, without that need widening a shipped package's public API — see the
        /// <c>InternalsVisibleTo</c> in <c>Spiderly.Shared.csproj</c>. They
        /// carry relational shapes no other fixture has — <c>UserExternalLogin</c> is the only
        /// <c>[Required]</c> navigation beside an explicit non-nullable foreign-key scalar in the whole
        /// codebase, and the reason an explicit-FK generator bug was caught at all was that
        /// <c>spiderly init</c> happens to scaffold it. A second copy of these in a test project drifts. The
        /// e2e fixture's full copy of the DbContext did, and broke CI with <c>CS1729</c> when its constructor
        /// changed.
        /// </para>
        /// </summary>
        internal static List<SpiderlyFile> GetEntityFiles(string appName) => EntitiesTemplate.Files(appName);

        /// <summary>
        /// Generates the starter project template for a Spiderly application, including both backend and frontend components.
        /// </summary>
        public static void Generate(string outputPath, ProjectGenerationOptions options)
        {
            GenerateProjectStructure(ProjectRootTemplate.Folder(options), outputPath);
        }

        private static void GenerateProjectStructure(SpiderlyFolder appStructure, string path)
        {
            string newPath = GenerateFolder(appStructure, path);

            foreach (SpiderlyFile file in appStructure.Files)
                GenerateFile(file, newPath);

            foreach (SpiderlyFolder folder in appStructure.ChildFolders)
                GenerateProjectStructure(folder, newPath);
        }

        private static string GenerateFolder(SpiderlyFolder appStructure, string path)
        {
            if (string.IsNullOrEmpty(appStructure.Name))
                return path;

            Helper.MakeFolder(path, appStructure.Name);

            return Path.Combine(path, appStructure.Name);
        }

        private static void GenerateFile(SpiderlyFile file, string path)
        {
            string filePath = Path.Combine(path, file.Name);

            Helper.FileOverrideCheck(filePath);

            Helper.WriteToFile(file.Data, filePath);
        }

        /// <summary>The TypeScript of an entity's details page.</summary>
        public static string GetSpiderlyAngularDetailsTsTemplate(string entityName) => EntityPagesTemplate.DetailsTs(entityName);

        /// <summary>The HTML of an entity's details page.</summary>
        public static string GetSpiderlyAngularDetailsHtmlTemplate(string entityName) => EntityPagesTemplate.DetailsHtml(entityName);

        /// <summary>The TypeScript of an entity's list page, as a data table.</summary>
        public static string GetSpiderlyAngularTableTsTemplate(string entityName) => EntityPagesTemplate.TableTs(entityName);

        /// <summary>The HTML of an entity's list page, as a data table.</summary>
        public static string GetSpiderlyAngularTableHtmlTemplate(string entityName) => EntityPagesTemplate.TableHtml(entityName);

        /// <summary>The TypeScript of an entity's list page, as a data view of cards.</summary>
        public static string GetSpiderlyAngularDataViewTsTemplate(string entityName) => EntityPagesTemplate.DataViewTs(entityName);

        /// <summary>The HTML of an entity's list page, as a data view of cards.</summary>
        public static string GetSpiderlyAngularDataViewHtmlTemplate(string entityName) => EntityPagesTemplate.DataViewHtml(entityName);

        /// <summary>
        /// The Frontend's <c>package.json</c>. Internal so that <c>InitTemplatePackageJsonTests</c> and <c>AngularPinTests</c> read the real template.
        /// </summary>
        internal static string GetPackageJsonData(string appName, string version, bool isRunningFromNuget) =>
            FrontendTemplate.PackageJson(appName, version, isRunningFromNuget);
    }
}
