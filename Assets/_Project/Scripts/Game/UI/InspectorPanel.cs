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
    /// <summary>Panneau de l'élément sélectionné. Une machine affiche ses emplacements comme un four :
    /// entrées → progression → sorties, chaque type de contenu avec sa propre représentation.
    /// Les problèmes de la machine s'affichent ici (et non dans le monde).</summary>
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
        private float nextUpdate;

        public InspectorPanel(GameContext ctx, VisualElement root)
        {
            this.ctx = ctx;
            this.root = root;
            ctx.SelectionChanged += Show;
            ctx.Session.Inventory.Changed += () => { if (current != null && CurrentTab is "État" or "Améliorations") RebuildTab(); };
            ctx.World.EntityMoved += (e, _) => { if (e == current) Show(current); };
            // Ce qu'on lâche n'importe où sur le panneau de la machine y est déposé.
            ItemTransfer.RegisterTarget(root, p => MachineDrop.Accepts(current as Machine, p),
                p => { int n = MachineDrop.Deposit(ctx, (Machine)current, p); if (n > 0) RebuildTabIfUpgrades(); return n; });
            Show(null);
        }

        public bool IsOpen => current != null;

        private void RebuildTabIfUpgrades() { if (CurrentTab == "Améliorations") RebuildTab(); }

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
            SorterMachine => new[] { "Filtre" },
            BatteryMachine => new[] { "État", "Faces" },
            Machine m when m.MachineDef.UpgradeSlots > 0 => new[] { "État", "Faces", "Améliorations" },
            Machine => new[] { "État", "Faces" },
            _ => new[] { "Réseau" },
        };

        private string CurrentTab => tabNames.Length == 0 ? "" : tabNames[tabs?.Selected ?? 0];

        private void RebuildTab()
        {
            if (current == null || content == null) return;
            float scrollY = scrollView?.scrollOffset.y ?? 0f;
            content.Clear();
            updaters.Clear();
            var locked = CurrentTab switch
            {
                "Faces" or "Filtre" => PlayerPermission.Sides,
                "Améliorations" => PlayerPermission.Upgrades,
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
                case "Filtre": BuildSorter((SorterMachine)current); break;
                case "Réseau": BuildNetwork((TransmitterSegment)current); break;
            }
            RunUpdaters();
            var sv = scrollView;
            content.schedule.Execute(() => sv.scrollOffset = new Vector2(0, scrollY));
        }

        public void Update()
        {
            if (current == null || Time.unscaledTime < nextUpdate) return;
            nextUpdate = Time.unscaledTime + 0.12f;
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
            if (e.Def.Item != null) IconSlot.Create(e.Def.Item, top, classes: "rslot--hero");
            else Ui.Swatch(Palette.Muted(e.Def.Color), top, large: true);
            var titles = Ui.El("col grow", top);
            Ui.Text(e.Def.DisplayName, "sheet-title", titles);
            Ui.Text(KindLabel(e), "meta", titles);
            Ui.Button("×", () => ctx.Selection = null, "btn btn--ghost btn--icon", top);
            if (!string.IsNullOrEmpty(e.Def.Description)) Ui.Text(e.Def.Description, "text-2 wrap inspector__desc", header);

            if (e is Machine m && m is not VaultMachine)
            {
                var bar = Ui.El("row inspector__actions", header);
                var badge = Ui.Badge("", null, bar);
                Ui.El("spacer", bar);
                var toggle = Ui.Button("", () => { if (ctx.Require(PlayerPermission.MachineControl)) m.SetEnabled(!m.Enabled); }, "btn btn--small", bar);
                headerUpdaters.Add(() =>
                {
                    Ui.SetBadge(badge, m.Status.DisplayName(), Ui.StatusState(m.Status));
                    toggle.text = m.Enabled ? "Arrêter" : "Démarrer";
                });
            }
        }

        private static string KindLabel(IGridEntity e)
        {
            var p = $"case {e.Position.x},{e.Position.y} · étage {e.Position.z + 1}";
            return e is TransmitterSegment s ? $"Transmetteur · {s.TransmitterDef.Carries.DisplayName()} · {p}" : $"Machine · {p}";
        }

        // ---------------- État ----------------

        private void BuildState(Machine m)
        {
            StatusBox(m);
            var sec = Ui.Section(null, content);
            var layout = Ui.El("row machine-layout", sec);

            if (m.ConsumesEnergy || m.ProducesEnergy) updaters.Add(SlotWidgets.EnergyBar(layout, m));

            var inputs = Ui.El("machine-col", layout);
            Ui.Text("Entrées", "meta machine-col__title", inputs);
            AddSlots(m, m.Inputs, Ui.El("machine-slots", inputs));
            if (m.Inputs == null || m.Inputs.Slots.Count == 0) Ui.Text("—", "text-3", inputs);

            var mid = Ui.El("machine-progress", layout);
            Ui.Text("→", "machine-progress__arrow", mid);
            Ui.Gauge(mid, out var fill);
            var pct = Ui.Text("", "meta", mid);
            updaters.Add(() =>
            {
                Ui.SetGauge(fill, m.Progress, m.Status == MachineStatus.Working ? null : "warning");
                pct.text = m.Progress > 0 ? $"{m.Progress * 100f:0} %" : "";
            });

            var outputs = Ui.El("machine-col", layout);
            Ui.Text("Sorties", "meta machine-col__title", outputs);
            AddSlots(m, m.Outputs, Ui.El("machine-slots", outputs));
            if ((m.Outputs == null || m.Outputs.Slots.Count == 0) && m.ProducesEnergy) Ui.Text("Énergie", "text-3", outputs);

            if (m is ProcessorMachine p)
            {
                var info = Ui.Text("", "meta", sec);
                info.style.marginTop = 8;
                updaters.Add(() =>
                {
                    var r = p.CurrentRecipe;
                    info.text = r == null ? "La machine démarre dès qu'elle a les bons ingrédients."
                        : $"En cours · {p.EnergyCostPerTick(r)} J/tick · vitesse ×{p.SpeedMultiplier:0.##}";
                });
            }
            DepositSection(m);
        }

        private void AddSlots(Machine m, ResourceStorage storage, VisualElement parent)
        {
            if (storage == null) return;
            foreach (var slot in storage.Slots)
            {
                var s = slot;
                if (s.Kind == ResourceKind.Item)
                    updaters.Add(SlotWidgets.ItemSlot(parent, s, () => storage.Capacity(s.Resource ?? FirstOfKind(ResourceKind.Item)), r =>
                    {
                        if (!ctx.Require(PlayerPermission.Take)) return;
                        ctx.Session.Actions.Take(m, r, int.MaxValue);
                    }, from: m));
                else
                    updaters.Add(SlotWidgets.Tank(parent, s, () => storage.Capacity(s.Resource ?? FirstOfKind(s.Kind)), r =>
                    {
                        if (ctx.Require(PlayerPermission.Take)) ctx.Session.Actions.Dump(m, r);
                    }, r =>
                    {
                        if (ctx.Require(PlayerPermission.Take)) ctx.Session.Actions.Take(m, r, int.MaxValue);
                    }, m));
            }
        }

        private ResourceDef FirstOfKind(ResourceKind k) => ctx.Db.Resources.FirstOrDefault(r => r.Kind == k);

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

        private void DepositSection(Machine m)
        {
            if (m.Inputs == null || m.Inputs.Slots.Count == 0) return;
            var help = Ui.El("status-window inspector__drop-hint", content);
            Ui.Text("[Dépose les ingrédients depuis l'inventaire juste en dessous : glisse-les sur la machine. Les liquides et gaz s'y versent depuis leurs bonbonnes.]", "system", help);
        }

        // ---------------- Réacteur ----------------

        private void BuildReactor(ReactorMachine r)
        {
            StatusBox(r);
            var s = r.Settings;

            var tanks = Ui.Section(null, content);
            var layout = Ui.El("row machine-layout", tanks);
            var inputs = Ui.El("machine-col", layout);
            Ui.Text("Entrées", "meta machine-col__title", inputs);
            AddSlots(r, r.Inputs, Ui.El("machine-slots", inputs));
            var mid = Ui.El("machine-progress", layout);
            Ui.Text("→", "machine-progress__arrow", mid);
            var outputs = Ui.El("machine-col", layout);
            Ui.Text("Sorties", "meta machine-col__title", outputs);
            AddSlots(r, r.Outputs, Ui.El("machine-slots", outputs));

            var burn = Ui.Section("Vitesse de combustion", content,
                "Plus elle est haute, plus le réacteur produit de déchets et de vapeur… et plus il lui faut d'eau.");
            var slider = new SliderInt(0, s.maxBurnRate) { value = r.BurnRate };
            slider.AddToClassList("inspector__slider");
            burn.Add(slider);
            var value = Ui.Text("", "number--small", burn);
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
        }

        // ---------------- Faces ----------------

        private void BuildSides(Machine m)
        {
            var help = Ui.Section(null, content);
            Ui.Text("Faces de la machine", "section-title", help);
            Ui.Text(m is VaultMachine
                ? "Entrée : ce qu'on pousse dans le vault est rangé dans le stockage commun (ton inventaire). Pour en sortir une ressource, colle un trieur au vault."
                : "Entrée : reçoit depuis un câble ou une machine voisine. Sortie : pousse tous les produits vers le voisin. " +
                  "Pour ne laisser sortir qu'une ressource, colle un trieur à la machine. Haut et Bas relient les étages.", "text-3 wrap", help);

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
            string label = setting.Mode switch { SideMode.Input => "Entrée", SideMode.Output => "Sortie", _ => "Fermée" };
            var mode = Ui.Button(label, () =>
            {
                if (!ctx.Require(PlayerPermission.Sides)) return;
                if (m.HasOutputSides) m.Sides.Cycle(d);
                else m.Sides.SetMode(d, setting.Mode == SideMode.Input ? SideMode.None : SideMode.Input);
                RebuildTab();
            }, "btn btn--small side-mode side-mode--" + setting.Mode.ToString().ToLowerInvariant(), cell);
            mode.name = "side-" + d;
            Tooltips.Register(mode, $"Face {d.DisplayName()}", m.HasOutputSides ? "Clic : Fermée → Entrée → Sortie." : "Clic : Fermée ↔ Entrée. Pour sortir quelque chose, colle un trieur.");

            var neighbour = ctx.World.At(m.Position + d.Offset());
            Ui.Text(neighbour == null ? "—" : neighbour.Def.DisplayName, "meta side-neighbour", cell);
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
                    IconSlot.Create(u.Item, row, classes: "rslot--small");
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
                IconSlot.Create(item, row, classes: "rslot--small");
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

        // ---------------- Trieur ----------------

        private void BuildSorter(SorterMachine t)
        {
            StatusBox(t);
            var flow = Ui.Section("Sens", content);
            var row = Ui.El("row sorter-flow", flow);
            Ui.Text(t.Source?.Def.DisplayName ?? "rien", "bold", row);
            Ui.Text($"  ({t.Back.DisplayName().ToLowerInvariant()})", "meta", row);
            Ui.Text("  →  trieur  →  ", "text-3", row);
            Ui.Text(t.Target?.Def.DisplayName ?? "rien", "bold", row);
            Ui.Text($"  ({t.Facing.DisplayName().ToLowerInvariant()})", "meta", row);
            Ui.Text("Il tire de ce qui est derrière lui (sorties d'une machine, ou stockage commun et ressources de base d'un vault) et pousse par son avant, repéré par le bandeau clair.", "text-3 wrap", flow);

            var sec = Ui.Section("Filtre", content, "Seule cette ressource passe. « Tout » laisse passer chaque ressource disponible, l'une après l'autre.");
            var available = t.Source?.Pullable().Select(p => p.resource).Where(r => r != null && r.Kind != ResourceKind.Energy).Distinct().ToList() ?? new List<ResourceDef>();
            var options = available.Concat(ctx.Db.Resources.Where(r => r != null && r.Kind != ResourceKind.Energy && !available.Contains(r))
                .OrderBy(r => r.Kind).ThenBy(r => r.DisplayName)).ToList();
            var choices = new List<GlassDropdown.Option> { new("Tout laisser passer") };
            choices.AddRange(options.Select(r => new GlassDropdown.Option(available.Contains(r) ? r.DisplayName + " · disponible" : r.DisplayName, r)));
            var dd = new GlassDropdown(choices, t.Filter == null ? 0 : options.IndexOf(t.Filter) + 1) { name = "sorter-filter" };
            dd.AddToClassList("sorter-filter");
            dd.Changed += i =>
            {
                if (!ctx.Require(PlayerPermission.Sides)) { dd.Select(t.Filter == null ? 0 : options.IndexOf(t.Filter) + 1, notify: false); return; }
                t.SetFilter(i <= 0 ? null : options[i - 1]);
            };
            sec.Add(dd);

            var buffer = Ui.Section("En transit", content);
            AddSlots(t, t.Outputs, Ui.El("machine-slots", buffer));
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
