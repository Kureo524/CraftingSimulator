using UnityEditor;
using UnityEngine;

namespace Nucleaire.EditorTools
{
    /// <summary>Build Linux de développement en ligne de commande (-executeMethod Nucleaire.EditorTools.DevBuild.Linux).</summary>
    public static class DevBuild
{
    public static void Linux()
    {
        string path = System.Environment.GetEnvironmentVariable("NUC_BUILD") ?? "Build/Linux/AtelierNucleaire.x86_64";
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { "Assets/_Project/Scenes/Workshop.unity" },
            locationPathName = path,
            target = BuildTarget.StandaloneLinux64,
            options = BuildOptions.Development,
        });
        Debug.Log($"[DevBuild] {report.summary.result} {report.summary.totalErrors} erreurs");
        if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded) EditorApplication.Exit(1);
    }
}
}
