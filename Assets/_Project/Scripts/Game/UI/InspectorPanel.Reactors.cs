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
    /// <summary>Panneau de l'élément sélectionné : réacteur à fission et multi-blocs (structure, fusion, déphaseur, matrice, ports).</summary>
    public sealed partial class InspectorPanel
    {
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
            var slider = new SliderInt(0, Mathf.Max(1, r.MaxBurnRate)) { value = r.BurnRate };
            slider.SetEnabled(r.MaxBurnRate > 0);
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
                if (slider.highValue != Mathf.Max(1, r.MaxBurnRate)) slider.highValue = Mathf.Max(1, r.MaxBurnRate);
                slider.SetEnabled(r.MaxBurnRate > 0);
                if (slider.value != r.BurnRate) slider.SetValueWithoutNotify(r.BurnRate);
                value.text = $"{r.BurnRate} / {r.MaxBurnRate} mB/tick";
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

        // ---------------- Multi-blocs ----------------

        /// <summary>Forme du multi-bloc : complet ou non, ce qui manque, ses composants, et les règles de construction.</summary>
        private void BuildStructure(Machine m)
        {
            var mb = ((IMultiblockController)m).Structure;
            mb.Refresh();
            var box = Ui.El("helpbox", content);
            box.AddToClassList(mb.Formed ? "helpbox--success" : "helpbox--warning");
            Ui.Text(mb.Formed ? $"[Structure complète : {mb.Size.x}×{mb.Size.y}×{mb.Size.z}, {mb.InteriorVolume} case(s) intérieure(s), {mb.Ports.Count} port(s).]" : $"[{mb.Problem}]", "system", box);

            var comp = Ui.Section("Composants internes", content);
            foreach (var role in Multiblock.InteriorRoles(mb.Kind))
            {
                var def = ctx.Db.Structures.FirstOrDefault(d => d.Role == role);
                var row = Ui.El("row list-row", comp);
                if (def?.Item != null) IconSlot.Create(def.Item, row, classes: "rslot--small");
                Ui.Text(def?.DisplayName ?? role.ToString(), "grow", row);
                Ui.Text($"×{mb.Count(role)}", "mono", row);
            }
            Ui.Text(Effects(m), "text-2 wrap", comp).style.marginTop = 6;

            var rules = Ui.Section("Comment le construire", content);
            Ui.Text("• Une boîte creuse de 3×3×3 à 7×7×9.", "text-2 wrap", rules);
            Ui.Text("• La paroi : des cadres structurels. Le verre (décoratif) peut en remplacer partout sauf sur les arêtes. Les ports (au moins un) et ce contrôleur (un seul) peuvent remplacer n'importe quel cadre.", "text-2 wrap", rules);
            Ui.Text("• L'intérieur : vide, ou les composants ci-dessus. Plus il y en a, plus la machine produit… et plus elle consomme.", "text-2 wrap", rules);
            Ui.Text("• Les ports font entrer et sortir : règle leurs faces (entrée / sortie) comme celles d'une machine. Les cubes lavande dans l'atelier montrent les blocs de paroi qui manquent.", "text-2 wrap", rules);
        }

        /// <summary>Bloc de structure seul (aucun contrôleur ne l'englobe) : à quoi il sert.</summary>
        private void BuildBlock(StructureBlock b)
        {
            var box = Ui.El("status-window", content);
            Ui.Text(b.StructureDef.IsWall
                ? "[Bloc de paroi : il ne fait partie d'aucune structure. Pose un contrôleur dans la paroi pour former un multi-bloc.]"
                : "[Composant interne : à placer à l'intérieur d'un multi-bloc complet.]", "system", box);
        }

        private string Effects(Machine m) => m switch
        {
            FissionReactor f => $"Combustion max : {f.MaxBurnRate} mB/tick · énergie de contrôle : {f.ControlUpkeep} J/tick · eau : jusqu'à {f.MaxBurnRate * f.CoolantPerFuel} mB/tick.",
            FusionReactor u => $"Allumage : {u.IgnitionEnergy / 1000} kJ · production nette : {u.NetOutput} J/tick · combustible : {u.Coils * u.MachineDef.Multiblock.fuelPerCoilPer10Ticks / 10f:0.#} mB/tick.",
            SpsMachine s => $"Énergie exigée : {s.PowerDemand} J/tick, à chaque tick · {s.Coils} bobine(s) = {s.Coils}× plus vite.",
            InductionMatrix i => $"Capacité : {i.EnergyCapacity / 1000} kJ · débit : {i.Rate} J/tick · fuite : {i.Leak} J/tick.",
            _ => "",
        };

        private void BuildFusion(FusionReactor u)
        {
            StatusBox(u);
            var sec = Ui.Section("Plasma", content);
            var state = Ui.Text("", "number--small", sec);
            Ui.Gauge(sec, out var fill, thick: true);
            var detail = Ui.Text("", "meta", sec);
            updaters.Add(() =>
            {
                state.text = u.Ignited ? "Allumé" : "Éteint";
                Ui.SetGauge(fill, u.Progress, u.Ignited ? "success" : "warning");
                detail.text = u.Ignited
                    ? $"Production nette : {u.NetOutput} J/tick (brut {u.GrossOutput}, entretien {u.Sustain}) · réserve {u.Energy / 1000} kJ"
                    : $"Charge d'allumage : {u.Energy / 1000} / {u.IgnitionEnergy / 1000} kJ. Il faut aussi du combustible D-T.";
            });
            var tanks = Ui.Section("Combustible", content);
            AddSlots(u, u.Inputs, Ui.El("machine-slots", tanks));
            Ui.Text("La charge d'allumage s'envoie par un port en entrée (une fission avec des turbines fait l'affaire). Une fois allumé, il s'entretient seul tant qu'il reçoit du combustible D-T ; l'énergie sort par un port en sortie.", "text-3 wrap", tanks);
        }

        private void BuildSps(SpsMachine sp)
        {
            StatusBox(sp);
            var sec = Ui.Section(null, content);
            var layout = Ui.El("row machine-layout", sec);
            updaters.Add(SlotWidgets.EnergyBar(layout, sp));
            var inputs = Ui.El("machine-col", layout);
            Ui.Text("Entrées", "meta machine-col__title", inputs);
            AddSlots(sp, sp.Inputs, Ui.El("machine-slots", inputs));
            var mid = Ui.El("machine-progress", layout);
            Ui.Text("→", "machine-progress__arrow", mid);
            Ui.Gauge(mid, out var fill);
            var outputs = Ui.El("machine-col", layout);
            Ui.Text("Sorties", "meta machine-col__title", outputs);
            AddSlots(sp, sp.Outputs, Ui.El("machine-slots", outputs));
            var info = Ui.Text("", "meta", sec);
            updaters.Add(() =>
            {
                Ui.SetGauge(fill, sp.Progress, sp.Status == MachineStatus.Working ? null : "warning");
                info.text = $"{sp.Coils} bobine(s) · exige {sp.PowerDemand} J/tick à chaque tick, sinon rien ne progresse.";
            });
        }

        private void BuildInduction(InductionMatrix i)
        {
            StatusBox(i);
            var sec = Ui.Section("Énergie stockée", content);
            var value = Ui.Text("", "number", sec);
            Ui.Gauge(sec, out var fill, thick: true);
            var detail = Ui.Text("", "meta", sec);
            updaters.Add(() =>
            {
                value.text = $"{Ui.Energy(i.Energy)} / {Ui.Energy(i.EnergyCapacity)}";
                Ui.SetGauge(fill, i.Progress);
                detail.text = $"{i.Cells} cellule(s), {i.Providers} fournisseur(s) · débit {i.Rate} J/tick en entrée et en sortie · fuite {i.Leak} J/tick";
            });
        }

        private void BuildPort(PortMachine p)
        {
            StatusBox(p);
            var sec = Ui.Section("Structure", content);
            if (p.Controller == null)
            {
                Ui.Text("Ce port n'appartient à aucune structure complète. Place-le dans la paroi d'un multi-bloc (pas sur un coin).", "text-2 wrap", sec);
                return;
            }
            var row = Ui.El("row", sec);
            if (p.Controller.MachineDef.Item != null) IconSlot.Create(p.Controller.MachineDef.Item, row, classes: "rslot--small");
            Ui.Text(p.Controller.MachineDef.DisplayName, "bold grow", row);
            Ui.Button("Ouvrir", () => { var c = p.Controller; ctx.Selection = null; ctx.Selection = c; }, "btn btn--small", row);
            Ui.Text("Règle ses faces dans l'onglet Faces (ou en mode Modification) : entrée pour remplir la structure, sortie pour en faire sortir les produits ou l'énergie.", "text-3 wrap", sec);
        }
    }
}
