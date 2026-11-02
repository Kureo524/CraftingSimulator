using UnityEngine.UIElements;

namespace Nucleaire.Game.UI
{
    /// <summary>Bascule Normal / Créatif en bas à droite. En créatif, le catalogue (à droite de l'inventaire) donne
    /// n'importe quel objet ; le reste du jeu ne change pas.</summary>
    public sealed class CreativeToggle
    {
        public CreativeToggle(GameContext ctx, VisualElement root)
        {
            var host = root.Q("creative-toggle");
            Ui.Text("Mode", "meta creative-toggle__label", host);
            var seg = new SegmentedControl(new[] { "Normal", "Créatif" });
            seg.Changed += i =>
            {
                ctx.Creative = i == 1;
                ctx.Notify(ctx.Creative ? "Mode créatif : ouvre l'inventaire (E), le catalogue à droite donne ce que tu veux." : "Mode normal.", Nucleaire.Simulation.EventSeverity.Info);
            };
            host.Add(seg);
            ctx.CreativeChanged += on => seg.Select(on ? 1 : 0, notify: false);
            Tooltips.Register(host, "Mode de jeu", "Créatif : le catalogue donne n'importe quel objet, pour essayer les recettes et les machines sans tout produire. Le reste est identique.");
        }
    }
}
