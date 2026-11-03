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
    public sealed partial class InspectorPanel
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
            // Cliquer un bloc de structure ouvre le contrôleur de sa structure.
            if (e is StructureBlock block && ControllerOf(block) is { } owner && owner != current) { ctx.Selection = owner; return; }
            current = e;
            root.Clear();
            updaters.Clear();
            headerUpdaters.Clear();
            Ui.SetVisible(root, e != null);
            if (e == null) return;

            BuildHeader(e);
            tabNames = TabsFor(e);
            if (e is Machine withSorters && withSorters.Sorters.Any()) tabNames = tabNames.Append("Trieurs").ToArray();
            if (tabNames.Length > 1)
            {
                // Multi-bloc complet : on ouvre directement sur son pilotage.
                int initial = 0;
                if (e is IMultiblockController mc) { mc.Structure.Refresh(); if (mc.Structure.Formed) initial = 1; }
                if (ctx.FocusSorterFace != null && Array.IndexOf(tabNames, "Trieurs") is var ti && ti >= 0) initial = ti;
                ctx.FocusSorterFace = null;
                tabs = new SegmentedControl(tabNames, initial);
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

        private Machine ControllerOf(StructureBlock b)
        {
            foreach (var m in ctx.World.Machines)
            {
                if (m is not IMultiblockController c) continue;
                c.Structure.Refresh();
                var p = b.Position;
                var lo = c.Structure.Min; var hi = c.Structure.Max;
                if (p.x >= lo.x && p.x <= hi.x && p.y >= lo.y && p.y <= hi.y && p.z >= lo.z && p.z <= hi.z) return m;
            }
            return null;
        }

        private static string[] TabsFor(IGridEntity e) => e switch
        {
            FissionReactor => new[] { "Structure", "Pilotage" },
            FusionReactor => new[] { "Structure", "Plasma" },
            SpsMachine => new[] { "Structure", "Déphasage" },
            InductionMatrix => new[] { "Structure", "Énergie" },
            PortMachine => new[] { "Port", "Faces" },
            VaultMachine => new[] { "Ressources", "Faces" },
            Machine m when m.MachineDef.UpgradeSlots > 0 => new[] { "État", "Faces", "Améliorations" },
            Machine => new[] { "État", "Faces" },
            TransmitterSegment => new[] { "Réseau" },
            _ => new[] { "Bloc" },
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
                "Faces" or "Trieurs" => PlayerPermission.Sides,
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
                case "Trieurs": BuildSorters((Machine)current); break;
                case "Réseau": BuildNetwork((TransmitterSegment)current); break;
                case "Bloc": BuildBlock((StructureBlock)current); break;
                case "Structure": BuildStructure((Machine)current); break;
                case "Plasma": BuildFusion((FusionReactor)current); break;
                case "Déphasage": BuildSps((SpsMachine)current); break;
                case "Énergie": BuildInduction((InductionMatrix)current); break;
                case "Port": BuildPort((PortMachine)current); break;
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
    }
}
