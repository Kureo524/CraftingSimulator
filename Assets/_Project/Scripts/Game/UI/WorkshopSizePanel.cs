using System.Linq;
using Nucleaire.Simulation;
using UnityEngine.UIElements;

namespace Nucleaire.Game.UI
{
    /// <summary>Taille de l'atelier (en bas à gauche, au-dessus du filtre d'affichage) et bouton « Agrandir » :
    /// chaque agrandissement ajoute des cases de chaque côté, contre des matériaux pris dans l'inventaire.</summary>
    public sealed class WorkshopSizePanel
    {
        private readonly GameContext ctx;
        private readonly Label size;
        private readonly Button expand;

        public WorkshopSizePanel(GameContext ctx, VisualElement root)
        {
            this.ctx = ctx;
            var host = root.Q("workshop-size");
            Ui.Text("Atelier", "meta workshop-size__label", host);
            size = Ui.Text("", "mono workshop-size__value", host);
            expand = Ui.Button("Agrandir", Expand, "btn workshop-size__button", host);
            Tooltips.Register(expand, () => ("Agrandir l'atelier", Describe()));
            Tooltips.Register(size, () => ("Taille de l'atelier", $"Surface constructible actuelle, sur {ctx.World.Settings.gridSize.x}×{ctx.World.Settings.gridSize.y} au maximum. Hauteur : {ctx.World.Settings.levels} étages."));
            ctx.World.AreaChanged += Refresh;
            Refresh();
        }

        private string Describe()
        {
            var w = ctx.World;
            if (!w.CanExpand) return "L'atelier a atteint sa taille maximale.";
            int next = w.Area.width + 2 * w.Settings.expansionStep;
            string text = $"Passe à {next}×{next} ({w.Expansions + 1}/{w.MaxExpansions}).";
            if (ctx.Creative) return text + "\nGratuit en mode créatif.";
            var inv = ctx.Session.Inventory;
            text += "\nCoût :" + string.Concat(w.NextExpansionCost().Select(c =>
                $"\n• {Ui.Stack(c)} (en stock : {Ui.Amount(c.Resource, inv.Count(c.Resource))})"));
            return text;
        }

        private void Expand()
        {
            if (ctx.Session.Actions.TryExpandWorkshop(out var error, free: ctx.Creative))
                ctx.Notify($"Atelier agrandi : {ctx.World.Area.width}×{ctx.World.Area.height}.", EventSeverity.Info);
            else ctx.Notify(error);
        }

        private void Refresh()
        {
            var a = ctx.World.Area;
            size.text = $"{a.width}×{a.height}";
            Ui.SetVisible(expand, ctx.World.CanExpand);
        }
    }
}
