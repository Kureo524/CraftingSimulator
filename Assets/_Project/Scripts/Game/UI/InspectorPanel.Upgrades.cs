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
    /// <summary>Panneau de l'élément sélectionné : améliorations installées et installables.</summary>
    public sealed partial class InspectorPanel
    {
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
    }
}
