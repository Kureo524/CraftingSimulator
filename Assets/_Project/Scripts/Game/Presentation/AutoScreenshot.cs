using System.Collections;
using System.IO;
using UnityEngine;

namespace Nucleaire.Game
{
    /// <summary>Outil de vérification : lancé avec « -autoshot dossier », le jeu enregistre quelques captures
    /// de scénarios types puis se ferme. Sans cet argument, ce composant ne fait rien.</summary>
    public sealed class AutoScreenshot : MonoBehaviour
    {
        public System.Func<string, IEnumerator> Scenario { get; set; }

        private IEnumerator Start()
        {
            string dir = Arg("-autoshot");
            if (dir == null) yield break;
            Directory.CreateDirectory(dir);
            string[] scenes = (Arg("-autoshot-scenes") ?? "overview").Split(',');
            yield return new WaitForSecondsRealtime(1.5f);
            foreach (var s in scenes)
            {
                if (Scenario != null) yield return Scenario(s);
                yield return new WaitForSecondsRealtime(0.8f);
                yield return new WaitForEndOfFrame();
                ScreenCapture.CaptureScreenshot(Path.Combine(dir, s + ".png"));
                yield return new WaitForSecondsRealtime(0.5f);
            }
            Application.Quit();
        }

        private static string Arg(string name)
        {
            var args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++) if (args[i] == name) return args[i + 1];
            return null;
        }
    }
}
