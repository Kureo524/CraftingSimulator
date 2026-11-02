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
    /// <summary>Panneau latéral de l'élément sélectionné : état, faces, améliorations, vault, établi, réseau.</summary>
    public sealed class InspectorPanel
    {
        private readonly GameContext ctx;
        private readonly VisualElement root;
        private readonly List<Action> updaters = new();
        private readonly List<Action> headerUpdaters = new();
        private IGridEntity current;
        private VisualElement content;
        private ScrollView scrollView;
        private SegmentedControl tabs;
        private string[] tabNames = Array.Empty<string>();
        private int workbenchCategory;
        private float nextUpdate;

        public InspectorPanel(GameContext ctx, VisualElement root)
        {
            this.ctx = ctx;
            this.root = root;
            ctx.SelectionChanged += Show;
            ctx.Session.Inventory.Changed += () => { if (current != null && UsesInventory()) RebuildTab(); };
            Show(null);
        }

        public bool IsOpen => current != null;

        // ---------------- Structure ----------------

        private void Show(IGridEntity e)
        {
            current = e;
            root.Clear();
            updaters.Clear();
            headerUpdaters.Clear();
            Ui.SetVisible(root, e != null);
            if (e == null) return;

            BuildHeader(e);
            tabNames = TabsFor(e);
            if (tabNames.Length > 1)
            {
                tabs = new SegmentedControl(tabNames);
                tabs.AddToClassList("inspector__tabs");
                tabs.Changed += _ => RebuildTab();
                root.Add(tabs);
            }
            else tabs = null;

            scrollView = new ScrollView(ScrollViewMode.Vertical);
            scrollView.AddToClassList("inspector__scroll");
            root.Add(scrollView);
            content = scrollView.contentContainer;
            RebuildTab();
        }

        private static string[] TabsFor(IGridEntity e) => e switch
        {
            ReactorMachine => new[] { "Pilotage", "Faces", "Améliorations" },
            VaultMachine => new[] { "Ressources", "Faces" },
            WorkbenchMachine => new[] { "Fabriquer" },
            BatteryMachine => new[] { "État", "Faces" },
            Machine m when m.MachineDef.UpgradeSlots > 0 => new[] { "État", "Faces", "Améliorations" },
            Machine => new[] { "État", "Faces" },
            _ => new[] { "Réseau" },
        };

        private string CurrentTab => tabNames.Length == 0 ? "" : tabNames[tabs?.Selected ?? 0];

        private bool UsesInventory() => CurrentTab is "État" or "Améliorations" or "Fabriquer" or "Ressources";

        private void RebuildTab()
        {
            if (current == null || content == null) return;
            float scrollY = scrollView?.scrollOffset.y ?? 0f;
            content.Clear();
            updaters.Clear();
            var locked = CurrentTab switch
            {
                "Faces" => PlayerPermission.Sides,
                "Améliorations" => PlayerPermission.Upgrades,
                "Fabriquer" => PlayerPermission.Craft,
                _ => PlayerPermission.None,
            };
            if (locked != PlayerPermission.None && !ctx.IsAllowed(locked))
            {
                var box = Ui.El("status-window", content);
                Ui.Text($"[Tu pourras {locked.DisplayName()} plus loin dans le tutoriel.]", "system", box);
                return;
            }
            switch (CurrentTab)
            {
                case "État": BuildState((Machine)current); break;
                case "Pilotage": BuildReactor((ReactorMachine)current); break;
                case "Faces": BuildSides((Machine)current); break;
                case "Améliorations": BuildUpgrades((Machine)current); break;
                case "Ressources": BuildVault((VaultMachine)current); break;
                case "Fabriquer": BuildWorkbench(); break;
                case "Réseau": BuildNetwork((TransmitterSegment)current); break;
            }
            RunUpdaters();
            var sv = scrollView;
            content.schedule.Execute(() => sv.scrollOffset = new Vector2(0, scrollY));
        }

        public void Update()
        {
            if (current == null || Time.unscaledTime < nextUpdate) return;
            nextUpdate = Time.unscaledTime + 0.15f;
            RunUpdaters();
        }

        private void RunUpdaters()
        {
            foreach (var u in headerUpdaters) u();
            foreach (var u in updaters) u();
        }

        // ---------------- En-tête ----------------

        private void BuildHeader(IGridEntity e)
        {
            var header = Ui.El("glass inspector__header", root);
            var top = Ui.El("row", header);
            Ui.Swatch(Palette.Muted(e.Def.Color), top, large: true);
            var titles = Ui.El("col grow", top);
            Ui.Text(e.Def.DisplayName, "sheet-title", titles);
            Ui.Text(KindLabel(e), "meta", titles);
            Ui.Button("×", () => ctx.Selection = null, "btn btn--ghost btn--icon", top);

            if (!string.IsNullOrEmpty(e.Def.Description)) Ui.Text(e.Def.Description, "text-2 wrap inspector__desc", header);

            if (e is Machine m && m is not VaultMachine && m is not WorkbenchMachine)
            {
                var bar = Ui.El("row inspector__actions", header);
                var badge = Ui.Badge("", null, bar);
                Ui.El("spacer", bar);
                var toggle = Ui.Button("", () => { if (ctx.Require(PlayerPermission.MachineControl)) m.SetEnabled(!m.Enabled); }, "btn btn--small", bar);
                var pick = Ui.Button("Démonter", () => PickUp(e), "btn btn--danger btn--small", bar);
                Tooltips.Register(pick, "Démonter", "La machine, ses solides et ses améliorations reviennent dans l'inventaire. Les liquides et gaz sont perdus. (Clic droit dans l'atelier.)");
                headerUpdaters.Add(() =>
                {
                    Ui.SetBadge(badge, m.Status.DisplayName(), Ui.StatusState(m.Status));
                    toggle.text = m.Enabled ? "Arrêter" : "Démarrer";
                });
            }
            else if (e is TransmitterSegment)
            {
                var bar = Ui.El("row inspector__actions", header);
                Ui.El("spacer", bar);
                Ui.Button("Démonter", () => PickUp(e), "btn btn--danger btn--small", bar);
            }
        }

        private string KindLabel(IGridEntity e) => e switch
        {
            TransmitterSegment s => $"Transmetteur · {s.TransmitterDef.Carries.DisplayName()} · case {e.Position.x},{e.Position.y}",
            _ => $"Machine · case {e.Position.x},{e.Position.y}",
        };

        private void PickUp(IGridEntity e)
        {
            if (ctx.Session.Actions.TryPickUp(e.Position, out var err)) ctx.Notify($"{e.Def.DisplayName} démonté.", EventSeverity.Info);
            else ctx.Notify(err);
        }

        // ---------------- État (machines à procédés, batterie, évent) ----------------

        private void BuildState(Machine m)
        {
            StatusBox(m);

            if (m is ProcessorMachine p)
            {
                var sec = Ui.Section("Procédé", content);
                var name = Ui.Text("", "bold", sec);
                Ui.Gauge(sec, out var fill, thick: true);
                var meta = Ui.Text("", "meta", sec);
                updaters.Add(() =>
                {
                    var r = p.CurrentRecipe;
                    name.text = r != null ? r.DisplayName : "Aucun procédé en cours";
                    Ui.SetGauge(fill, p.Progress);
                    var shown = r ?? p.LockedRecipe;
                    meta.text = shown == null ? "" :
                        $"{shown.DurationTicks / (float)ctx.World.Settings.ticksPerSecond / p.SpeedMultiplier:0.#} s · {p.EnergyCostPerTick(shown)} J/tick · vitesse ×{p.SpeedMultiplier:0.##}";
                });

                if (p.Recipes.Count > 1)
                {
                    var choices = new List<string> { "Automatique" };
                    choices.AddRange(p.Recipes.Select(r => r.DisplayName));
                    var dd = new DropdownField("Procédé", choices, p.LockedRecipe == null ? 0 : p.Recipes.ToList().IndexOf(p.LockedRecipe) + 1);
                    dd.AddToClassList("inspector__field");
                    dd.RegisterValueChangedCallback(evt =>
                    {
                        if (!ctx.Require(PlayerPermission.MachineControl)) { dd.SetValueWithoutNotify(evt.previousValue); return; }
                        int i = choices.IndexOf(evt.newValue);
                        p.LockRecipe(i <= 0 ? null : p.Recipes[i - 1]);
                    });
                    sec.Add(dd);
                    Tooltips.Register(dd, "Choix du procédé", "En automatique, la machine lance le premier procédé dont elle a tous les ingrédients. Impose un procédé pour éviter qu'elle consomme le mauvais ingrédient.");
                }
            }

            if (m.ConsumesEnergy || m.ProducesEnergy)
            {
                var sec = Ui.Section("Énergie", content);
                Ui.Gauge(sec, out var fill);
                var label = Ui.Text("", "meta", sec);
                updaters.Add(() =>
                {
                    Ui.SetGauge(fill, m.EnergyCapacity == 0 ? 0 : (float)m.Energy / m.EnergyCapacity, m.Energy == 0 && m.ConsumesEnergy ? "danger" : null);
                    label.text = $"{Ui.Energy(m.Energy)} / {Ui.Energy(m.EnergyCapacity)}";
                });
            }

            if (m.Inputs != null) StorageSection("Entrées", m, m.Inputs, takeable: true);
            DepositSection(m);
            if (m.Outputs != null) StorageSection("Sorties", m, m.Outputs, takeable: true);
            if (m is ProcessorMachine proc) RecipesSection(proc);
        }

        private void StatusBox(Machine m)
        {
            var box = Ui.El("helpbox", content);
            var label = Ui.Text("", "system", box);
            updaters.Add(() =>
            {
                var state = Ui.StatusState(m.Status) ?? "info";
                foreach (var s in new[] { "info", "success", "warning", "danger" }) box.EnableInClassList("helpbox--" + s, s == state);
                string detail = string.IsNullOrEmpty(m.StatusDetail) ? "" : " " + m.StatusDetail;
                label.text = $"[{m.Status.DisplayName()}.{detail}]";
            });
        }

        private void StorageSection(string title, Machine m, ResourceStorage storage, bool takeable)
        {
            var resources = (storage.Allowed ?? storage.Contents.Select(kv => kv.Key).ToList()).Where(r => r != null)
                .OrderByDescending(r => storage.Amount(r) > 0).ThenBy(r => r.DisplayName).ToList();
            if (resources.Count == 0 && storage.Allowed != null) return;
            var sec = Ui.Section(title, content);
            if (resources.Count == 0) { Ui.System("Vide.", sec); return; }
            foreach (var r in resources)
            {
                var row = Ui.El("list-row", sec);
                Ui.Swatch(Palette.Muted(r.Color), row);
                var col = Ui.El("col grow", row);
                var name = Ui.Text(r.DisplayName, null, col);
                Ui.Gauge(col, out var fill).AddToClassList("gauge--inline");
                var amount = Ui.Text("", "mono", row);
                amount.style.marginLeft = 8;
                if (takeable && r.Kind == ResourceKind.Item)
                {
                    var take = Ui.Button("Prendre", () =>
                    {
                        if (!ctx.Require(PlayerPermission.Take)) return;
                        int n = ctx.Session.Actions.Take(m, r, int.MaxValue);
                        if (n == 0) ctx.Notify($"Aucun {r.DisplayName.ToLowerInvariant()} à prendre.", EventSeverity.Info);
                    }, "btn btn--ghost btn--small", row);
                    take.style.marginLeft = 6;
                }
                name.RegisterCallback<ClickEvent>(_ => ctx.OpenRecipeBook(r));
                updaters.Add(() =>
                {
                    int a = storage.Amount(r), cap = storage.Capacity(r);
                    amount.text = $"{Ui.Amount(r, a)}";
                    Ui.SetGauge(fill, cap == 0 ? 0 : (float)a / cap, a >= cap && cap > 0 ? "warning" : null);
                });
            }
        }

        private void DepositSection(Machine m)
        {
            if (m.Inputs == null) return;
            var accepted = ctx.Session.Inventory.Items.Where(kv => m.Inputs.Accepts(kv.Key)).ToList();
            bool anyItemInput = m.Inputs.Allowed == null || m.Inputs.Allowed.Any(r => r.Kind == ResourceKind.Item);
            if (!anyItemInput) return;

            var sec = Ui.Section("Déposer depuis l'inventaire", content);
            if (accepted.Count == 0)
            {
                Ui.System("Aucun objet de ton inventaire n'entre dans cette machine.", sec);
                return;
            }
            foreach (var (r, count) in accepted)
            {
                var row = Ui.El("list-row", sec);
                Ui.Swatch(Palette.Muted(r.Color), row);
                Ui.Text($"{r.DisplayName}", "grow", row);
                Ui.Text($"×{count}", "meta", row).style.marginRight = 6;
                foreach (var (label, n) in new[] { ("+1", 1), ("+10", 10), ("Tout", int.MaxValue) })
                {
                    var b = Ui.Button(label, () =>
                    {
                        if (!ctx.Require(PlayerPermission.Deposit)) return;
                        int moved = ctx.Session.Actions.Deposit(m, r, n);
                        if (moved == 0) ctx.Notify($"{m.MachineDef.DisplayName} : plus de place pour {r.DisplayName.ToLowerInvariant()}.");
                    }, "btn btn--small", row);
                    b.style.marginLeft = 4;
                }
            }
        }

        private void RecipesSection(ProcessorMachine p)
        {
            var sec = Ui.Section("Ce que fait cette machine", content);
            foreach (var r in p.Recipes)
            {
                var card = Ui.El("recipe-line", sec);
                Ui.Text(r.DisplayName, "bold", card);
                var flow = Ui.El("row recipe-flow", card);
                foreach (var i in r.Inputs) Ui.Chip(i.Resource, Ui.Amount(i.Resource, i.Amount), () => ctx.OpenRecipeBook(i.Resource), flow);
                if (r.Inputs.Count > 0) Ui.Text("→", "text-3 arrow", flow);
                foreach (var o in r.Outputs) Ui.Chip(o.Resource, Ui.Amount(o.Resource, o.Amount), () => ctx.OpenRecipeBook(o.Resource), flow);
            }
        }

        // ---------------- Réacteur ----------------

        private void BuildReactor(ReactorMachine r)
        {
            StatusBox(r);
            var s = r.Settings;

            var burn = Ui.Section("Vitesse de combustion", content,
                "Plus elle est haute, plus le réacteur produit de déchets et de vapeur… et plus il lui faut d'eau.");
            var slider = new SliderInt(0, s.maxBurnRate) { value = r.BurnRate };
            slider.AddToClassList("inspector__slider");
            var value = Ui.Text("", "number--small", burn);
            burn.Insert(burn.IndexOf(value), slider);
            var need = Ui.Text("", "meta", burn);
            slider.RegisterValueChangedCallback(evt =>
            {
                if (ctx.Require(PlayerPermission.MachineControl)) r.SetBurnRate(evt.newValue);
                else slider.SetValueWithoutNotify(r.BurnRate);
            });
            updaters.Add(() =>
            {
                if (slider.value != r.BurnRate) slider.SetValueWithoutNotify(r.BurnRate);
                value.text = $"{r.BurnRate} mB/tick";
                need.text = $"Eau nécessaire : {r.BurnRate * r.CoolantPerFuel} mB/tick · vapeur produite : {r.BurnRate * r.HeatedCoolantPerFuel} mB/tick";
            });

            var heat = Ui.Section("Chaleur", content);
            var heatRow = Ui.El("row", heat);
            var heatValue = Ui.Text("", "number grow", heatRow);
            var delta = Ui.Text("", "mono", heatRow);
            Ui.Gauge(heat, out var heatFill, thick: true);
            var cooling = Ui.Text("", "meta", heat);
            var restart = Ui.Button("Relancer le réacteur", () =>
            {
                if (!ctx.Require(PlayerPermission.MachineControl)) return;
                if (!r.Restart()) ctx.Notify("Le réacteur est encore trop chaud pour être relancé.");
            }, "btn btn--primary", heat);
            restart.style.marginTop = 8;
            updaters.Add(() =>
            {
                heatValue.text = $"{r.Heat:0}° / {s.meltdownHeat:0}°";
                delta.text = r.LastHeatDelta > 0.05f ? $"+{r.LastHeatDelta:0.#}/tick" : r.LastHeatDelta < -0.05f ? $"{r.LastHeatDelta:0.#}/tick" : "stable";
                float ratio = r.HeatRatio;
                Ui.SetGauge(heatFill, ratio, ratio >= ctx.World.Settings.reactorWarningRatio ? "danger" : ratio > 0.35f ? "warning" : "success");
                cooling.text = r.LastBurn > 0 ? $"Refroidissement assuré à {r.LastCooling:P0}" : "Combustion à l'arrêt";
                Ui.SetVisible(restart, r.IsScrammed);
                restart.SetEnabled(r.CanRestart);
            });

            StorageSection("Entrées", r, r.Inputs, takeable: false);
            StorageSection("Sorties", r, r.Outputs, takeable: false);
        }

        // ---------------- Faces ----------------

        private void BuildSides(Machine m)
        {
            var help = Ui.Section(null, content);
            Ui.Text("Faces de la machine", "section-title", help);
            Ui.Text("Entrée : reçoit depuis un tuyau ou une machine voisine. Sortie : pousse les produits vers le voisin. " +
                    "Sur la machine, les plaques bleues sont des entrées, les plaques lavande des sorties.", "text-3 wrap", help);

            var cross = Ui.El("sides-cross", content);
            var rows = new[] { Ui.El("row sides-row", cross), Ui.El("row sides-row", cross), Ui.El("row sides-row", cross) };
            Ui.El("sides-cell sides-cell--empty", rows[0]);
            SideCell(m, Direction.North, rows[0]);
            Ui.El("sides-cell sides-cell--empty", rows[0]);
            SideCell(m, Direction.West, rows[1]);
            var center = Ui.El("sides-cell sides-center", rows[1]);
            Ui.Swatch(Palette.Muted(m.MachineDef.Color), center, large: true);
            SideCell(m, Direction.East, rows[1]);
            Ui.El("sides-cell sides-cell--empty", rows[2]);
            SideCell(m, Direction.South, rows[2]);
            Ui.El("sides-cell sides-cell--empty", rows[2]);
        }

        private void SideCell(Machine m, Direction d, VisualElement parent)
        {
            var setting = m.Sides[d];
            var cell = Ui.El("sides-cell glass", parent);
            Ui.Text(d.DisplayName(), "meta", cell);
            string label = setting.Mode switch { SideMode.Input => "Entrée", SideMode.Output => "Sortie", _ => "Fermée" };
            var mode = Ui.Button(label, () => { m.Sides.Cycle(d); RebuildTab(); }, "btn btn--small side-mode side-mode--" + setting.Mode.ToString().ToLowerInvariant(), cell);
            Tooltips.Register(mode, $"Face {d.DisplayName()}", "Clic : Fermée → Entrée → Sortie.");

            var neighbour = ctx.World.At(m.Position + d.Offset());
            Ui.Text(neighbour == null ? "—" : neighbour.Def.DisplayName, "meta side-neighbour", cell);

            if (setting.Mode == SideMode.None) return;
            var options = FilterOptions(m, setting.Mode);
            bool vault = m is VaultMachine;
            var names = new List<string> { vault ? "Choisir…" : "Tout" };
            names.AddRange(options.Select(r => r.DisplayName));
            int index = setting.Filter == null ? 0 : options.IndexOf(setting.Filter) + 1;
            var dd = new DropdownField(names, Mathf.Max(0, index));
            dd.AddToClassList("side-filter");
            dd.RegisterValueChangedCallback(evt =>
            {
                int i = names.IndexOf(evt.newValue);
                m.Sides.SetFilter(d, i <= 0 ? null : options[i - 1]);
            });
            cell.Add(dd);
            if (vault && setting.Mode == SideMode.Output && setting.Filter == null)
                Ui.Text("Choisis la ressource à sortir", "meta side-warning", cell);
        }

        private List<ResourceDef> FilterOptions(Machine m, SideMode mode)
        {
            IEnumerable<ResourceDef> list = m switch
            {
                VaultMachine v => v.BaseResources,
                _ when mode == SideMode.Input => (m.Inputs?.Allowed ?? ctx.Db.Resources.Where(r => r.Kind is ResourceKind.Fluid or ResourceKind.Gas).ToList())
                    .Concat(m.ConsumesEnergy ? new[] { ctx.Db.Energy } : Array.Empty<ResourceDef>()),
                _ => (m.Outputs?.Allowed ?? (IReadOnlyCollection<ResourceDef>)Array.Empty<ResourceDef>())
                    .Concat(m.ProducesEnergy ? new[] { ctx.Db.Energy } : Array.Empty<ResourceDef>()),
            };
            return list.Where(r => r != null).Distinct().ToList();
        }

        // ---------------- Améliorations ----------------

        private void BuildUpgrades(Machine m)
        {
            var slots = Ui.Section($"Emplacements ({m.Upgrades.Count}/{m.MachineDef.UpgradeSlots})", content);
            for (int i = 0; i < m.MachineDef.UpgradeSlots; i++)
            {
                var row = Ui.El("list-row", slots);
                if (i < m.Upgrades.Count)
                {
                    var u = m.Upgrades[i];
                    Ui.Swatch(Palette.Muted(u.Item.Color), row);
                    Ui.Text(u.DisplayName, "grow", row);
                    Ui.Button("Retirer", () => { ctx.Session.Actions.RemoveUpgrade(m, u); RebuildTab(); }, "btn btn--ghost btn--small", row);
                }
                else Ui.Text("Emplacement libre", "text-3", row);
            }

            var fx = Ui.Section("Effets cumulés", content);
            void Line(string label, MachineStat stat)
            {
                var row = Ui.El("row", fx);
                Ui.Text(label, "text-2 grow", row);
                float v = m.Multiplier(stat);
                Ui.Text($"×{v:0.##}", "mono" + (Mathf.Approximately(v, 1f) ? " text-3" : ""), row);
            }
            if (m is ReactorMachine)
            {
                Line("Eau consommée", MachineStat.CoolantUsage);
                Line("Vapeur produite", MachineStat.HeatedCoolant);
                Line("Capacité des réservoirs", MachineStat.Capacity);
            }
            else
            {
                Line("Vitesse", MachineStat.Speed);
                Line("Consommation d'énergie", MachineStat.EnergyUsage);
                Line("Capacité des réservoirs", MachineStat.Capacity);
            }

            var inv = Ui.Section("Installer depuis l'inventaire", content);
            var available = ctx.Session.Inventory.Items
                .Select(kv => (item: kv.Key, count: kv.Value, upgrade: ctx.Db.UpgradeForItem(kv.Key)))
                .Where(x => x.upgrade != null).ToList();
            if (available.Count == 0)
            {
                Ui.System("Aucune amélioration dans l'inventaire. Elles se fabriquent à l'établi.", inv);
                return;
            }
            foreach (var (item, count, upgrade) in available)
            {
                var row = Ui.El("list-row", inv);
                Ui.Swatch(Palette.Muted(item.Color), row);
                var col = Ui.El("col grow", row);
                Ui.Text($"{upgrade.DisplayName} ×{count}", null, col);
                Ui.Text(item.Description, "meta wrap", col);
                bool ok = m.CanInstall(upgrade, out var reason);
                var b = Ui.Button("Installer", () =>
                {
                    if (ctx.Session.Actions.TryInstallUpgrade(m, item, out var err)) RebuildTab();
                    else ctx.Notify(err);
                }, ok ? "btn btn--primary btn--small" : "btn btn--small", row);
                b.SetEnabled(ok);
                if (!ok) Tooltips.Register(b, "Impossible", reason);
            }
        }

        // ---------------- Vault ----------------

        private void BuildVault(VaultMachine v)
        {
            var info = Ui.El("status-window", content);
            info.style.marginBottom = 12;
            Ui.Text("[Ressources de base illimitées. Pour automatiser, ouvre l'onglet Faces : mets une face en sortie et choisis la ressource.]", "system", info);

            var sec = Ui.Section("Prendre", content);
            foreach (var r in v.BaseResources)
            {
                var row = Ui.El("list-row", sec);
                Ui.Swatch(Palette.Muted(r.Color), row);
                var name = Ui.Text(r.DisplayName, "grow", row);
                name.RegisterCallback<ClickEvent>(_ => ctx.OpenRecipeBook(r));
                var have = Ui.Text($"×{ctx.Session.Inventory.Count(r)}", "meta", row);
                have.style.marginRight = 6;
                foreach (var n in new[] { 1, 10, 64 })
                {
                    var b = Ui.Button($"+{n}", () => { if (ctx.Require(PlayerPermission.VaultTake)) ctx.Session.Actions.TakeFromVault(v, r, n); }, "btn btn--small", row);
                    b.style.marginLeft = 4;
                }
            }
        }

        // ---------------- Établi ----------------

        private static readonly string[] WorkbenchCategories = { "Machines", "Transport", "Composants", "Améliorations" };

        private int CraftCategory(CraftingRecipe c)
        {
            var r = c.Result.Resource;
            var p = ctx.Db.PlaceableForItem(r);
            if (p is MachineDef) return 0;
            if (p is TransmitterDef) return 1;
            if (ctx.Db.UpgradeForItem(r) != null) return 3;
            return 2;
        }

        private void BuildWorkbench()
        {
            var seg = new SegmentedControl(WorkbenchCategories, workbenchCategory);
            seg.AddToClassList("inspector__subtabs");
            seg.Changed += i => { workbenchCategory = i; RebuildTab(); };
            content.Add(seg);

            var wb = ctx.Session.Workbench;
            var recipes = wb.Recipes.Where(c => CraftCategory(c) == workbenchCategory)
                .OrderByDescending(c => wb.CanCraft(c, out _)).ThenBy(c => c.Result.Resource.DisplayName).ToList();

            foreach (var c in recipes)
            {
                bool can = wb.CanCraft(c, out _);
                var card = Ui.El("glass card craft-card", content);
                card.EnableInClassList("craft-card--disabled", !can);
                var head = Ui.El("row", card);
                Ui.Swatch(Palette.Muted(c.Result.Resource.Color), head);
                var title = Ui.Text(c.Result.Resource.DisplayName, "bold grow", head);
                title.RegisterCallback<ClickEvent>(_ => ctx.OpenRecipeBook(c.Result.Resource));
                Ui.Text($"×{c.Result.Amount}", "meta", head);

                var flow = Ui.El("row recipe-flow", card);
                foreach (var i in c.Ingredients)
                {
                    int have = ctx.Session.Inventory.Count(i.Resource);
                    Ui.Chip(i.Resource, $"{have}/{i.Amount}", () => ctx.OpenRecipeBook(i.Resource), flow, have >= i.Amount ? "ok" : "missing");
                }

                var actions = Ui.El("row craft-actions", card);
                Ui.El("spacer", actions);
                var one = Ui.Button("Fabriquer", () => wb.Craft(c), can ? "btn btn--primary btn--small" : "btn btn--small", actions);
                one.SetEnabled(can);
                var five = Ui.Button("×5", () => wb.Craft(c, 5), "btn btn--small", actions);
                five.style.marginLeft = 4;
                five.SetEnabled(can);
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
            Ui.Text("[Un réseau reçoit ce que les faces de sortie voisines lui poussent, et le distribue aux faces d'entrée voisines. " +
                    (s.TransmitterDef.Carries == ResourceKind.Item ? "Les convoyeurs acceptent plusieurs solides à la fois.]" : "Un seul liquide ou gaz à la fois par réseau.]"),
                "system", help);
        }
    }
}
