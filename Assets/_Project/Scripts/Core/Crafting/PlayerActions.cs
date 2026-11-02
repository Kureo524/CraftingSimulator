using System.Linq;
using Nucleaire.Data;
using Nucleaire.Simulation;
using UnityEngine;

namespace Nucleaire.Crafting
{
    /// <summary>Actions du joueur sur l'atelier : poser/démonter, déposer/prendre, vault, améliorations.
    /// Chaque action renvoie un message lisible en cas d'échec.</summary>
    public sealed class PlayerActions
    {
        private readonly World world;

        public PlayerActions(World world, PlayerInventory inventory)
        {
            this.world = world;
            Inventory = inventory;
        }

        public PlayerInventory Inventory { get; }

        public bool TryPlace(ResourceDef item, Vector2Int pos, out string error)
        {
            var def = world.Db.PlaceableForItem(item);
            if (def == null) { error = $"{item?.DisplayName} ne se pose pas."; return false; }
            if (Inventory.Count(item) <= 0) { error = $"Plus de {item.DisplayName} dans l'inventaire."; return false; }
            if (!world.CanPlace(def, pos, out error)) return false;
            Inventory.Remove(item, 1);
            world.Place(def, pos);
            return true;
        }

        /// <summary>Démonte l'élément : il revient dans l'inventaire avec les solides qu'il contenait.
        /// Les liquides et gaz qu'il contenait sont perdus.</summary>
        public bool TryPickUp(Vector2Int pos, out string error)
        {
            error = null;
            var e = world.At(pos);
            if (e == null) { error = "Rien à démonter ici."; return false; }
            if (world.Db.PrePlacedMachines.Contains(e.Def as MachineDef)) { error = $"{e.Def.DisplayName} est fixe."; return false; }

            if (e is Machine m)
                foreach (var s in m.SalvageableItems()) Inventory.Add(s.Resource, s.Amount);
            world.Remove(pos);
            Inventory.Add(e.Def.Item, 1);
            return true;
        }

        public int Deposit(Machine m, ResourceDef r, int amount)
        {
            int n = m.InsertManual(r, Mathf.Min(amount, Inventory.Count(r)));
            Inventory.Remove(r, n);
            return n;
        }

        /// <summary>Prend des solides dans une machine (sorties d'abord, puis entrées).</summary>
        public int Take(Machine m, ResourceDef r, int amount)
        {
            if (!PlayerInventory.CanHold(r)) return 0;
            int n = m.Outputs?.Extract(r, amount) ?? 0;
            if (n < amount && m.Inputs != null) n += m.Inputs.Extract(r, amount - n);
            Inventory.Add(r, n);
            return n;
        }

        public int TakeFromVault(VaultMachine vault, ResourceDef r, int amount)
        {
            if (vault == null || !vault.Provides(r) || !PlayerInventory.CanHold(r)) return 0;
            return Inventory.Add(r, amount);
        }

        public bool TryInstallUpgrade(Machine m, ResourceDef item, out string error)
        {
            var u = world.Db.UpgradeForItem(item);
            if (u == null) { error = $"{item?.DisplayName} n'est pas une amélioration."; return false; }
            if (Inventory.Count(item) <= 0) { error = $"Aucune {u.DisplayName} dans l'inventaire."; return false; }
            if (!m.CanInstall(u, out error)) return false;
            Inventory.Remove(item, 1);
            m.InstallUpgrade(u);
            return true;
        }

        public bool RemoveUpgrade(Machine m, UpgradeDef u)
        {
            if (!m.RemoveUpgrade(u)) return false;
            Inventory.Add(u.Item, 1);
            return true;
        }
    }
}
