using System.Text.RegularExpressions;
using UnityEditor;

// Patches the generated .csproj for App UI MVVM sample assemblies to include
// MVVMSourceGenerators.dll as a Roslyn analyzer. Unity's compiler already applies
// this analyzer via the RoslynAnalyzer label, but the IDE-facing .csproj files
// omit it, causing false lint errors in VS Code / Rider.
public class AppUISamplesCSProjectFix : AssetPostprocessor
{
    static string OnGeneratedCSProject(string path, string content)
    {
        if (!path.Contains("Unity.AppUI.Samples.MVVM"))
            return content;

        const string generatorPath =
            @"Library\PackageCache\com.unity.dt.app-ui@a7abc66e20ae\Runtime\MVVM\SourceGenerators\netstandard2.0\MVVMSourceGenerators.dll";

        if (content.Contains("MVVMSourceGenerators.dll"))
            return content;

        // Insert after the last existing <Analyzer> entry in the first <ItemGroup> that has analyzers
        var insertion = $"    <Analyzer Include=\"{generatorPath}\" />\n";
        content = Regex.Replace(
            content,
            @"(<Analyzer Include=""[^""]*Unity\.UIToolkit\.SourceGenerator\.dll""[^/]*/>\s*\n)",
            m => m.Value + insertion,
            RegexOptions.Singleline
        );

        return content;
    }
}
