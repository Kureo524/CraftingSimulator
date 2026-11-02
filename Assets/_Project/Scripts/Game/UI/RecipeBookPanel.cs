using System.Collections.Generic;
using System.Linq;
using Nucleaire.Data;
using UnityEngine;
using UnityEngine.UIElements;

namespace Nucleaire.Game.UI
{
    /// <summary>Livre de recettes (à la JEI) : pour chaque ressource, comment l'obtenir et à quoi elle sert.
    /// Généré entièrement depuis la RecipeDatabase. Chaque ingrédient est cliquable pour remonter l'arbre.</summary>
    public sealed class RecipeBookPanel
    {
        private readonly GameContext ctx;
        private readonly VisualElement layer, book;
        private readonly VisualElement fiche;
        private readonly Stack<ResourceDef> history = new();
        private ResourceDef current;
        private int ficheTab;

        public RecipeBookPanel(GameContext ctx, VisualElement root)
        {
            this.ctx = ctx;
            layer = root.Q("book-layer");
            book = root.Q("book");
            layer.RegisterCallback<PointerDownEvent>(evt => { if (evt.target == layer) Close(); });

            var rscroll = new ScrollView(ScrollViewMode.Vertical);
            rscroll.AddToClassList("grow");
            book.Add(rscroll);
            fiche = rscroll.contentContainer;

            ctx.RecipeBookRequested += Open;
            Ui.SetVisible(layer, false);
        }

        public bool IsOpen => !layer.ClassListContains("hidden");
        public void Open(ResourceDef r, int tab)
        {
            Ui.SetVisible(layer, true);
            if (r == null) r = current ?? ctx.Session.Objectives.Current?.Target.Resource ?? ctx.Db.Resources.FirstOrDefault();
            Navigate(r, pushHistory: current != null && current != r);
            ficheTab = tab;
            RebuildFiche();
        }

        public void Close() => Ui.SetVisible(layer, false);

        private void Navigate(ResourceDef r, bool pushHistory = true)
        {
            if (r == null) return;
            if (pushHistory && current != null && current != r) history.Push(current);
            if (current != r) ficheTab = 0;
            current = r;
            RebuildFiche();
        }

        private void Back()
        {
            if (history.Count == 0) return;
            current = history.Pop();
            ficheTab = 0;
            RebuildFiche();
        }

        // ---------------- Fiche ----------------

        private void RebuildFiche()
        {
            fiche.Clear();
            var r = current;
            if (r == null) return;

            var header = Ui.El("glass glass--light book__header", fiche);
            var top = Ui.El("row", header);
            var back = Ui.Button("←", Back, "btn btn--ghost btn--icon", top);
            back.SetEnabled(history.Count > 0);
            Tooltips.Register(back, "Retour", "Revenir à la ressource précédente.");
            IconSlot.Create(r, top, classes: "rslot--hero");
            var titles = Ui.El("col grow", top);
            Ui.Text(r.DisplayName, "sheet-title", titles);
            var badges = Ui.El("row", titles);
            Ui.Badge(r.Kind.DisplayName(), null, badges);
            if (r.IsBaseResource) Ui.Badge("Ressource de base · vault", "accent", badges).style.marginLeft = 6;
            var objective = ctx.Session.Objectives.Objectives.FirstOrDefault(o => o.Target.Resource == r);
            if (objective != null) Ui.Badge($"Objectif : {objective.Title}", ctx.Session.Objectives.IsCompleted(objective) ? "success" : "accent", badges).style.marginLeft = 6;
            Ui.Button("Fermer", Close, "btn btn--small", top).name = "book-close";

            if (!string.IsNullOrEmpty(r.Description)) Ui.Text(r.Description, "text-2 wrap book__desc", header);
            long produced = ctx.World.Produced(r);
            if (produced > 0) Ui.Text($"Produit dans l'atelier : {Ui.Amount(r, produced)}", "meta", header);
            if (ctx.Session.Inventory.Count(r) > 0) Ui.Text($"Dans l'inventaire : ×{ctx.Session.Inventory.Count(r)}", "meta", header);

            var producing = ctx.Db.RecipesProducing(r);
            var usedIn = ctx.Db.RecipesUsing(r);
            var machine = ctx.Db.PlaceableForItem(r) as MachineDef;
            var processes = machine != null ? ctx.Db.ProcessesFor(machine) : null;

            var labels = new List<string> { $"Obtenir ({producing.Count})", $"Utilisé dans ({usedIn.Count})" };
            if (processes != null) labels.Add($"Procédés ({ProcessCount(machine)})");
            if (ficheTab >= labels.Count) ficheTab = 0;
            var tabs = new SegmentedControl(labels, ficheTab);
            tabs.AddToClassList("book__tabs");
            tabs.Changed += i => { ficheTab = i; RebuildFiche(); };
            fiche.Add(tabs);

            switch (ficheTab)
            {
                case 0:
                    if (producing.Count == 0) Ui.System("Aucune recette ne produit cette ressource.", fiche);
                    foreach (var e in producing) Card(e, r);
                    break;
                case 1:
                    if (usedIn.Count == 0) Ui.System(objective != null ? "C'est un objectif final : elle ne sert à rien d'autre." : "Cette ressource n'entre dans aucune recette.", fiche);
                    foreach (var e in usedIn) Card(e, r);
                    break;
                case 2:
                    foreach (var e in ctx.Db.AllEntries.Where(e => e.Machine == machine && e.Source is RecipeSource.Process or RecipeSource.Reactor)) Card(e, r);
                    if (machine.Behaviour == MachineBehaviour.Vault) Ui.System("Fournit à l'infini toutes les ressources de base.", fiche);
                    if (machine.Behaviour == MachineBehaviour.Workbench) Ui.System("Permet de fabriquer machines, transmetteurs, composants et améliorations.", fiche);
                    break;
            }
        }

        /// <summary>Un ingrédient : icône + quantité dessous. Clic : sa recette. R / U fonctionnent aussi au survol.</summary>
        private void Ingredient(ResourceDef r, long amount, ResourceDef focus, VisualElement parent)
        {
            var cell = Ui.El("recipe-ingredient", parent);
            var slot = IconSlot.Create(r, cell, r.Kind == ResourceKind.Item ? amount.ToString() : null,
                r == focus ? "rslot--focus" : null, () => Ui.Amount(r, amount) + " · clic : sa recette");
            slot.RegisterCallback<ClickEvent>(_ => Navigate(r));
            Ui.Text(r.Kind == ResourceKind.Item ? "" : Ui.Amount(r, amount), "mono recipe-ingredient__amount", cell);
        }

        private int ProcessCount(MachineDef m) => ctx.Db.AllEntries.Count(e => e.Machine == m && e.Source is RecipeSource.Process or RecipeSource.Reactor);

        private void Card(RecipeEntry e, ResourceDef focus)
        {
            var card = Ui.El("glass card recipe-card", fiche);
            var head = Ui.El("row", card);
            string machineName = e.Source switch
            {
                RecipeSource.Workbench => "Établi",
                _ => e.Machine != null ? e.Machine.DisplayName : "?",
            };
            var machineChip = Ui.El("chip machine-chip", head);
            if (e.Machine?.Item != null)
            {
                IconSlot.Create(e.Machine.Item, machineChip, classes: "rslot--small", extra: () => "Clic : comment fabriquer cette machine.");
                machineChip.RegisterCallback<ClickEvent>(_ => Navigate(e.Machine.Item));
            }
            Ui.Text(machineName, "bold", machineChip);
            Ui.El("spacer", head);
            if (e.Source == RecipeSource.Workbench && e.Outputs.Count > 0)
            {
                var result = e.Outputs[0].Resource;
                var go = Ui.Button("Fabriquer à l'établi", () => { Close(); ctx.OpenWorkbench(result); }, "btn btn--primary btn--small", head);
                go.name = "book-craft-" + result.Id;
                Tooltips.Register(go, "Fabriquer à l'établi", "Ouvre l'établi directement sur cette recette.");
            }
            if (e.Source == RecipeSource.Process)
            {
                float seconds = e.DurationTicks / (float)ctx.World.Settings.ticksPerSecond;
                Ui.Text($"{seconds:0.#} s" + (e.EnergyPerTick > 0 ? $" · {e.EnergyPerTick} J/tick" : ""), "meta", head);
            }

            var flow = Ui.El("row recipe-flow", card);
            foreach (var i in e.Inputs) Ingredient(i.Resource, i.Amount, focus, flow);
            if (e.Inputs.Count > 0) Ui.Text("→", "text-3 arrow recipe-arrow", flow);
            foreach (var o in e.Outputs) Ingredient(o.Resource, o.Amount, focus, flow);

            if (!string.IsNullOrEmpty(e.Note)) Ui.System(e.Note, card).style.marginTop = 4;
            if (e.Origin is ProcessRecipe minP && minP.MinPowerRatio > 0f)
                Ui.System($"Exige {minP.MinPowerRatio:P0} de sa puissance à chaque tick : sinon, elle ne progresse pas du tout.", card).style.marginTop = 4;
            if (e.Source == RecipeSource.Process && e.Origin is ProcessRecipe p && p.Outputs.Count > 1)
                Ui.System("Plusieurs produits : chacun doit être évacué, sinon la machine se bloque.", card).style.marginTop = 4;
        }
    }
}
