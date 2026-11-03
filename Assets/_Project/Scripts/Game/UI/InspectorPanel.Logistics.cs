using System;
using System.Collections.Generic;
using System.Linq;
using Nucleaire.Data;
using Nucleaire.Logistics;
using Nucleaire.Simulation;
using UnityEngine;
using UnityEngine.UIElements;

namespace Nucleaire.Game.UI
{
    /// <summary>Panneau de l'élément sélectionné : logistique (faces, vault, trieurs, réseaux de transmetteurs).</summary>
    public sealed partial class InspectorPanel
    {
        // ---------------- Faces ----------------

        private void BuildSides(Machine m)
        {
            var help = Ui.Section(null, content);
            Ui.Text("Faces de la machine", "section-title", help);
            Ui.Text(m is VaultMachine
                ? "Entrée : ce qu'on pousse dans le vault est rangé dans le stockage commun (ton inventaire). Pour en sortir une ressource, colle un trieur au vault."
                : "Entrée : reçoit depuis un câble ou une machine voisine (Entrée 1, 2… : seulement dans cet emplacement). " +
                  "Sortie : pousse tous les produits (Sortie 1, 2… : seulement cet emplacement). Pour trier, colle un trieur sur la face. Haut et Bas relient les étages.", "text-3 wrap", help);

            var cross = Ui.El("sides-cross", content);
            var rows = new[] { Ui.El("row sides-row", cross), Ui.El("row sides-row", cross), Ui.El("row sides-row", cross) };
            Ui.El("sides-cell sides-cell--empty", rows[0]);
            SideCell(m, Direction.North, rows[0]);
            SideCell(m, Direction.Up, rows[0]);
            SideCell(m, Direction.West, rows[1]);
            var center = Ui.El("sides-cell sides-center", rows[1]);
            Ui.Swatch(Palette.Muted(m.MachineDef.Color), center, large: true);
            SideCell(m, Direction.East, rows[1]);
            Ui.El("sides-cell sides-cell--empty", rows[2]);
            SideCell(m, Direction.South, rows[2]);
            SideCell(m, Direction.Down, rows[2]);
        }

        private void SideCell(Machine m, Direction d, VisualElement parent)
        {
            var setting = m.Sides[d];
            var cell = Ui.El("sides-cell glass" + (d.IsVertical() ? " sides-cell--vertical" : ""), parent);
            Ui.Text(d.DisplayName(), "meta", cell);
            var sorterHere = m.SorterAt(d);
            string label = sorterHere != null ? "Trieur" : m.SideLabel(d);
            var mode = Ui.Button(label, () =>
            {
                if (!ctx.Require(PlayerPermission.Sides)) return;
                if (sorterHere != null) { tabs?.Select(Array.IndexOf(tabNames, "Trieurs")); return; }
                m.CycleSide(d);
                RebuildTab();
            }, "btn btn--small side-mode side-mode--" + (sorterHere != null ? "output" : setting.Mode.ToString().ToLowerInvariant()), cell);
            mode.name = "side-" + d;
            Tooltips.Register(mode, $"Face {d.DisplayName()}", sorterHere != null ? "Un trieur occupe cette face : clic pour le régler."
                : string.Join(" → ", m.SideOptions().Select(o => m.SideLabel(o))) + ". Aussi en mode Modification (touche A deux fois), directement dans l'atelier.");

            var neighbour = ctx.World.At(m.Position + d.Offset());
            Ui.Text(neighbour == null ? "—" : neighbour.Def.DisplayName, "meta side-neighbour", cell);
        }

        // ---------------- Vault ----------------

        private ResourceDef vaultPick;

        private void BuildVault(VaultMachine v)
        {
            var info = Ui.El("status-window", content);
            info.style.marginBottom = 12;
            Ui.Text("[Ressources de base illimitées. Clique une ressource puis choisis la quantité. Pour automatiser, ouvre l'onglet Faces : mets une face en sortie et choisis la ressource.]", "system", info);

            var sec = Ui.Section("Prendre", content);
            var grid = Ui.El("islot-grid", sec);
            var picker = Ui.El("vault-picker", sec);
            if (vaultPick != null && !v.BaseResources.Contains(vaultPick)) vaultPick = null;
            foreach (var r in v.BaseResources)
            {
                var box = IconSlot.Create(r, grid, "∞", "rslot--vault", () => "Clic : choisir la quantité à prendre.");
                box.EnableInClassList("rslot--picked", r == vaultPick);
                box.RegisterCallback<ClickEvent>(_ =>
                {
                    vaultPick = r;
                    foreach (var other in grid.Children()) other.RemoveFromClassList("rslot--picked");
                    box.AddToClassList("rslot--picked");
                    BuildPicker(v, picker);
                });
            }
            BuildPicker(v, picker);
        }

        /// <summary>Quantités à prendre dans le vault pour la ressource choisie : ×1, ×10, ×100 ou un nombre libre.
        /// Les liquides et gaz se comptent en seaux (1 B = 1000 mB).</summary>
        private void BuildPicker(VaultMachine v, VisualElement picker)
        {
            picker.Clear();
            var r = vaultPick;
            if (r == null)
            {
                Ui.Text("[Choisis une ressource ci-dessus.]", "system", picker);
                return;
            }
            bool fluid = r.Kind != ResourceKind.Item;
            int unit = fluid ? 1000 : 1;
            var head = Ui.El("row vault-picker__head", picker);
            var icon = new ResourceIcon(r) { Canister = fluid };
            icon.AddToClassList("vault-picker__icon");
            head.Add(icon);
            var titles = Ui.El("col grow", head);
            Ui.Text(r.DisplayName, "bold", titles);
            var have = Ui.Text("", "meta", titles);
            void Refresh() => have.text = $"Dans l'inventaire : {Ui.Amount(r, ctx.Session.Inventory.Count(r))}";
            Refresh();

            void Take(int n)
            {
                if (n <= 0 || !ctx.Require(PlayerPermission.VaultTake)) return;
                int got = ctx.Session.Actions.TakeFromVault(v, r, n * unit);
                if (got > 0) ctx.Notify($"+{Ui.Amount(r, got).TrimStart('×')} {r.DisplayName.ToLowerInvariant()}", EventSeverity.Success);
                Refresh();
            }

            var row = Ui.El("row vault-picker__row", picker);
            foreach (var n in new[] { 1, 10, 100 })
            {
                var b = Ui.Button(fluid ? $"{n} B" : $"×{n}", () => Take(n), "btn btn--small vault-picker__btn", row);
                Tooltips.Register(b, "Prendre", fluid ? $"{n * unit} mB, rangés en bonbonnes de {ctx.Session.Inventory.CanisterCapacity} mB." : $"{n} {r.DisplayName.ToLowerInvariant()}.");
            }
            var custom = new IntegerField { value = 64 };
            custom.AddToClassList("vault-picker__field");
            row.Add(custom);
            Ui.Text(fluid ? "B" : "", "meta", row).style.marginRight = 6;
            Ui.Button("Prendre", () => Take(custom.value), "btn btn--primary btn--small", row);
            custom.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode is KeyCode.Return or KeyCode.KeypadEnter) Take(custom.value);
            });
        }

        /// <summary>Trieurs collés sur les faces : liste de ressources à laisser passer (vide : tout passe).</summary>
        private void BuildSorters(Machine m)
        {
            var info = Ui.El("status-window", content);
            info.style.marginBottom = 10;
            Ui.Text("[Un trieur tire de cette machine les ressources de sa liste et les pousse vers ce qui touche sa face (machine ou câble). Liste vide : tout passe.]", "system", info);
            var available = m.Pullable().Select(p => p.resource).Where(r => r != null && r.Kind != ResourceKind.Energy).Distinct().ToList();
            bool first = true;
            foreach (var sorter in m.Sorters.ToList())
            {
                var t = sorter;
                var target = t.Target;
                var sec = Ui.Section($"Face {t.Face.DisplayName().ToLowerInvariant()} → {target?.Def.DisplayName ?? "rien (pose une machine ou un câble devant)"}", content);
                var chips = Ui.El("row sorter-chips", sec);
                if (t.Filters.Count == 0) Ui.Text("[Tout passe.]", "system", chips);
                foreach (var r in t.Filters.ToList())
                {
                    var chip = IconSlot.Create(r, chips, classes: "rslot--small sorter-chip", extra: () => "Clic : retirer de la liste.");
                    chip.RegisterCallback<ClickEvent>(_ => { if (ctx.Require(PlayerPermission.Sides)) { t.RemoveFilter(r); RebuildTab(); } });
                }

                var options = available.Concat(ctx.Db.Resources.Where(r => r != null && r.Kind != ResourceKind.Energy && !available.Contains(r))
                    .OrderBy(r => r.Kind).ThenBy(r => r.DisplayName)).Where(r => !t.Filters.Contains(r)).ToList();
                var choices = new List<GlassDropdown.Option> { new("Ajouter une ressource…") };
                choices.AddRange(options.Select(r => new GlassDropdown.Option(available.Contains(r) ? r.DisplayName + " · disponible" : r.DisplayName, r)));
                var row = Ui.El("row sorter-actions", sec);
                var dd = new GlassDropdown(choices);
                if (first) dd.name = "sorter-add";
                dd.AddToClassList("sorter-add grow");
                dd.Changed += i =>
                {
                    if (i <= 0 || !ctx.Require(PlayerPermission.Sides)) return;
                    t.AddFilter(options[i - 1]);
                    RebuildTab();
                };
                row.Add(dd);
                var remove = Ui.Button("Retirer", () =>
                {
                    if (ctx.Session.Actions.TryDetachSorter(m, t.Face, out var err)) Show(m);
                    else ctx.Notify(err);
                }, "btn btn--ghost btn--small", row);
                remove.style.marginLeft = 6;
                Tooltips.Register(remove, "Retirer le trieur", "Il revient dans l'inventaire.");
                first = false;
            }
        }

        // ---------------- Réseau ----------------

        private void BuildNetwork(TransmitterSegment s)
        {
            var sec = Ui.Section("Réseau", content);
            var size = Ui.Text("", "text-2", sec);
            Ui.Gauge(sec, out var fill);
            var stored = Ui.Text("", "meta", sec);
            var flow = Ui.Text("", "meta", sec);
            var list = Ui.El("col", sec);
            updaters.Add(() =>
            {
                var net = s.Network;
                if (net == null) return;
                size.text = $"{net.Segments.Count} segment(s) · débit {Ui.Amount(ctx.Db.Resources.FirstOrDefault(r => r.Kind == net.Carries), net.Def.ThroughputPerTick)}/tick";
                Ui.SetGauge(fill, net.Capacity == 0 ? 0 : (float)net.Stored / net.Capacity);
                stored.text = $"Contenu : {net.Stored} / {net.Capacity}";
                flow.text = $"Distribué au dernier tick : {net.LastMoved}";
                list.Clear();
                foreach (var (r, a) in net.Contents) Ui.Chip(r, Ui.Amount(r, a), () => ctx.OpenRecipeBook(r), list);
            });
            var help = Ui.El("status-window", content);
            Ui.Text("[Un réseau reçoit ce que les faces de sortie voisines lui poussent, et le distribue aux faces d'entrée voisines, y compris au-dessus et en dessous. " +
                    (s.TransmitterDef.Carries == ResourceKind.Item ? "Les convoyeurs acceptent plusieurs solides à la fois.]" : "Un seul liquide ou gaz à la fois par réseau.]"),
                "system", help);
        }
    }
}
