using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Nucleaire.EditorTools
{
    /// <summary>Builds en ligne de commande (-executeMethod Nucleaire.EditorTools.DevBuild.&lt;Méthode&gt;) :
    /// <c>Linux</c> (développement, pour les captures automatiques), <c>ReleaseLinux</c> et <c>ReleaseWindows</c> (rendu).
    /// La variable d'environnement NUC_BUILD remplace le chemin de sortie.</summary>
    public static class DevBuild
    {
        private const string Scene = "Assets/_Project/Scenes/Workshop.unity";

        public static void Linux() =>
            Build(BuildTarget.StandaloneLinux64, "Build/Linux/AtelierNucleaire.x86_64", BuildOptions.Development);

        public static void ReleaseLinux() =>
            Build(BuildTarget.StandaloneLinux64, "Build/Release/Linux/AtelierNucleaire.x86_64", BuildOptions.None);

        public static void ReleaseWindows() =>
            Build(BuildTarget.StandaloneWindows64, "Build/Release/Windows/AtelierNucleaire.exe", BuildOptions.None);

        private static void Build(BuildTarget target, string defaultPath, BuildOptions options)
        {
            string path = System.Environment.GetEnvironmentVariable("NUC_BUILD") ?? defaultPath;
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { Scene },
                locationPathName = path,
                target = target,
                options = options,
            });
            Debug.Log($"[DevBuild] {target} : {report.summary.result}, {report.summary.totalErrors} erreur(s) → {path}");
            if (report.summary.result != BuildResult.Succeeded) EditorApplication.Exit(1);
        }
    }
}
