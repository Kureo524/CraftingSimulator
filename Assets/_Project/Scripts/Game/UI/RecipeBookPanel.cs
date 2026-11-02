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
        private static readonly string[] Filters = { "Tout", "Matières", "Fluides", "Machines", "Améliorations" };
        private static readonly string[] FicheTabs = { "Obtenir", "Utilisé dans", "Procédés" };

        private readonly GameContext ctx;
        private readonly VisualElement layer, book;
        private readonly VisualElement list, fiche;
        private readonly TextField search;
        private readonly SegmentedControl filter;
        private readonly Stack<ResourceDef> history = new();
        private ResourceDef current;
        private int ficheTab;

        public RecipeBookPanel(GameContext ctx, VisualElement root)
        {
            this.ctx = ctx;
            layer = root.Q("book-layer");
            book = root.Q("book");
            layer.RegisterCallback<PointerDownEvent>(evt => { if (evt.target == layer) Close(); });

            var left = Ui.El("glass book__left", book);
            var head = Ui.El("row", left);
            Ui.Text("Livre de recettes", "sheet-title grow", head);
            search = new TextField { value = "" };
            search.AddToClassList("book__search");
            search.textEdition.placeholder = "Rechercher…";
            search.RegisterValueChangedCallback(_ => RebuildList());
            left.Add(search);
            filter = new SegmentedControl(Filters);
            filter.AddToClassList("book__filter");
            filter.Changed += _ => RebuildList();
            left.Add(filter);
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.AddToClassList("grow");
            left.Add(scroll);
            list = scroll.contentContainer;

            var right = Ui.El("book__right", book);
            var rscroll = new ScrollView(ScrollViewMode.Vertical);
            rscroll.AddToClassList("grow");
            right.Add(rscroll);
            fiche = rscroll.contentContainer;

            ctx.RecipeBookRequested += Open;
            Ui.SetVisible(layer, false);
        }

        public bool IsOpen => !layer.ClassListContains("hidden");
        public bool IsTyping => search.focusController?.focusedElement == search;

        public void Open(ResourceDef r, int tab)
        {
            Ui.SetVisible(layer, true);
            if (r == null) r = current ?? ctx.Session.Objectives.Current?.Target.Resource ?? ctx.Db.Resources.FirstOrDefault();
            Navigate(r, pushHistory: current != null && current != r);
            ficheTab = tab;
            RebuildFiche();
            RebuildList();
        }

        public void Close()
        {
            Ui.SetVisible(layer, false);
            search.Blur();
        }

        private void Navigate(ResourceDef r, bool pushHistory = true)
        {
            if (r == null) return;
            if (pushHistory && current != null && current != r) history.Push(current);
            if (current != r) ficheTab = 0;
            current = r;
            RebuildFiche();
            RebuildList();
        }

        private void Back()
        {
            if (history.Count == 0) return;
            current = history.Pop();
            ficheTab = 0;
            RebuildFiche();
            RebuildList();
        }

        // ---------------- Liste ----------------

        private int FilterOf(ResourceDef r)
        {
            if (ctx.Db.PlaceableForItem(r) != null) return 3;
            if (ctx.Db.UpgradeForItem(r) != null) return 4;
            if (r.Kind is ResourceKind.Fluid or ResourceKind.Gas or ResourceKind.Energy) return 2;
            return 1;
        }

        private void RebuildList()
        {
            list.Clear();
            string q = search.value?.Trim().ToLowerInvariant() ?? "";
            int f = filter.Selected;
            var items = ctx.Db.Resources
                .Where(r => r != null && (f == 0 || FilterOf(r) == f))
                .Where(r => q.Length == 0 || r.DisplayName.ToLowerInvariant().Contains(q))
                .OrderBy(FilterOf).ThenBy(r => r.DisplayName);
            foreach (var r in items)
            {
                var row = Ui.El("list-row", list);
                row.EnableInClassList("list-row--selected", r == current);
                Ui.Swatch(Palette.Muted(r.Color), row);
                Ui.Text(r.DisplayName, "grow", row);
                if (r.IsBaseResource) Ui.Text("vault", "meta", row);
                row.RegisterCallback<ClickEvent>(_ => Navigate(r));
            }
            if (list.childCount == 0) Ui.System("Aucun résultat.", list);
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
            Ui.Swatch(Palette.Muted(r.Color), top, large: true);
            var titles = Ui.El("col grow", top);
            Ui.Text(r.DisplayName, "sheet-title", titles);
            var badges = Ui.El("row", titles);
            Ui.Badge(r.Kind.DisplayName(), null, badges);
            if (r.IsBaseResource) Ui.Badge("Ressource de base · vault", "accent", badges).style.marginLeft = 6;
            var objective = ctx.Session.Objectives.Objectives.FirstOrDefault(o => o.Target.Resource == r);
            if (objective != null) Ui.Badge($"Objectif : {objective.Title}", ctx.Session.Objectives.IsCompleted(objective) ? "success" : "accent", badges).style.marginLeft = 6;
            Ui.Button("Fermer", Close, "btn btn--small", top);

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
            Ui.Swatch(Palette.Muted(e.Machine != null ? e.Machine.Color : Color.gray), machineChip);
            Ui.Text(machineName, "bold", machineChip);
            if (e.Machine?.Item != null)
            {
                machineChip.RegisterCallback<ClickEvent>(_ => Navigate(e.Machine.Item));
                Tooltips.Register(machineChip, machineName, "Clic : comment fabriquer cette machine.");
            }
            Ui.El("spacer", head);
            if (e.Source == RecipeSource.Process)
            {
                float seconds = e.DurationTicks / (float)ctx.World.Settings.ticksPerSecond;
                Ui.Text($"{seconds:0.#} s" + (e.EnergyPerTick > 0 ? $" · {e.EnergyPerTick} J/tick" : ""), "meta", head);
            }

            var flow = Ui.El("row recipe-flow", card);
            foreach (var i in e.Inputs)
                Ui.Chip(i.Resource, Ui.Amount(i.Resource, i.Amount), () => Navigate(i.Resource), flow, i.Resource == focus ? "ok" : null);
            if (e.Inputs.Count > 0) Ui.Text("→", "text-3 arrow", flow);
            foreach (var o in e.Outputs)
                Ui.Chip(o.Resource, Ui.Amount(o.Resource, o.Amount), () => Navigate(o.Resource), flow, o.Resource == focus ? "ok" : null);

            if (!string.IsNullOrEmpty(e.Note)) Ui.System(e.Note, card).style.marginTop = 4;
            if (e.Origin is ProcessRecipe minP && minP.MinPowerRatio > 0f)
                Ui.System($"Exige {minP.MinPowerRatio:P0} de sa puissance à chaque tick : sinon, elle ne progresse pas du tout.", card).style.marginTop = 4;
            if (e.Source == RecipeSource.Process && e.Origin is ProcessRecipe p && p.Outputs.Count > 1)
                Ui.System("Plusieurs produits : chacun doit être évacué, sinon la machine se bloque.", card).style.marginTop = 4;
        }
    }
}
