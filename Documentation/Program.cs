namespace Documentation
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            await Docfx.Dotnet.DotnetApiCatalog.GenerateManagedReferenceYamlFiles("../../../../docfx.json");
            await Docfx.Docset.Build("../../../../docfx.json");
        }
    }
}
