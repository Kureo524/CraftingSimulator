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

        public PlayerActions(World world, PlayerInventory inventory, PermissionGate gate = null)
        {
            this.world = world;
            Inventory = inventory;
            Gate = gate ?? new PermissionGate();
        }

        public PlayerInventory Inventory { get; }
        public PermissionGate Gate { get; }

        /// <summary>Pose un objet. <paramref name="variant"/> choisit ce qui est posé quand l'objet en permet plusieurs
        /// (câble universel : électrique, à liquide, à gaz ou à objets).</summary>
        public bool TryPlace(ResourceDef item, Vector3Int pos, out string error, PlaceableDef variant = null, Direction facing = Direction.North)
        {
            if (!Gate.Check(PlayerPermission.Place, out error)) return false;
            var def = variant != null && variant.Item == item ? variant : world.Db.PlaceableForItem(item);
            if (def == null) { error = $"{item?.DisplayName} ne se pose pas."; return false; }
            if (Inventory.Count(item) <= 0) { error = $"Plus de {item.DisplayName} dans l'inventaire."; return false; }
            if (!world.CanPlace(def, pos, out error)) return false;
            Inventory.Remove(item, 1);
            world.Place(def, pos, facing);
            return true;
        }

        /// <summary>Démonte l'élément : il revient dans l'inventaire avec les solides qu'il contenait.
        /// Les liquides et gaz qu'il contenait sont perdus.</summary>
        /// <summary>Déplace une machine (contenu, faces et améliorations conservés).</summary>
        public bool TryMove(Vector3Int from, Vector3Int to, out string error)
        {
            if (!Gate.Check(PlayerPermission.Move, out error)) return false;
            if (!world.CanMove(from, to, out error)) return false;
            return world.Move(from, to);
        }

        public bool TryPickUp(Vector3Int pos, out string error)
        {
            if (!Gate.Check(PlayerPermission.PickUp, out error)) return false;
            var e = world.At(pos);
            if (e == null) { error = "Rien à démonter ici."; return false; }

            if (e is Machine m)
                foreach (var s in m.SalvageableItems()) Inventory.Add(s.Resource, s.Amount);
            world.Remove(pos);
            Inventory.Add(e.Def.Item, 1);
            return true;
        }

        public int Deposit(Machine m, ResourceDef r, int amount)
        {
            if (!Gate.IsAllowed(PlayerPermission.Deposit)) return 0;
            int n = m.InsertManual(r, Mathf.Min(amount, Inventory.Count(r)));
            Inventory.Remove(r, n);
            return n;
        }

        /// <summary>Prend des solides dans une machine (sorties d'abord, puis entrées).</summary>
        public int Take(Machine m, ResourceDef r, int amount)
        {
            if (!PlayerInventory.CanHold(r) || !Gate.IsAllowed(PlayerPermission.Take)) return 0;
            int n = m.Outputs?.Extract(r, amount) ?? 0;
            if (n < amount && m.Inputs != null) n += m.Inputs.Extract(r, amount - n);
            Inventory.Add(r, n);
            return n;
        }

        /// <summary>Vide un réservoir de liquide ou de gaz (le contenu est perdu).</summary>
        public int Dump(Machine m, ResourceDef r)
        {
            if (r == null || r.Kind == ResourceKind.Item || !Gate.IsAllowed(PlayerPermission.Take)) return 0;
            int n = (m.Inputs?.Extract(r, int.MaxValue) ?? 0) + (m.Outputs?.Extract(r, int.MaxValue) ?? 0);
            if (n > 0) world.Log(EventSeverity.Info, $"{m.MachineDef.DisplayName} : {n} mB de {r.DisplayName.ToLowerInvariant()} rejetés.", m);
            return n;
        }

        public int TakeFromVault(VaultMachine vault, ResourceDef r, int amount)
        {
            if (vault == null || !vault.Provides(r) || !PlayerInventory.CanHold(r) || !Gate.IsAllowed(PlayerPermission.VaultTake)) return 0;
            return Inventory.Add(r, amount);
        }

        public bool TryInstallUpgrade(Machine m, ResourceDef item, out string error)
        {
            if (!Gate.Check(PlayerPermission.Upgrades, out error)) return false;
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
            if (!Gate.IsAllowed(PlayerPermission.Upgrades)) return false;
            if (!m.RemoveUpgrade(u)) return false;
            Inventory.Add(u.Item, 1);
            return true;
        }
    }
}
