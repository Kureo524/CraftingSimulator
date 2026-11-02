using System;

namespace Nucleaire.Data
{
    /// <summary>Actions du joueur, débloquées progressivement par le tutoriel.</summary>
    [Flags]
    public enum PlayerPermission
    {
        None = 0,
        RecipeBook = 1 << 0,      // ouvrir le livre de recettes
        Inspect = 1 << 1,         // sélectionner une machine
        VaultTake = 1 << 2,       // prendre dans le vault
        Place = 1 << 3,           // poser un objet
        Deposit = 1 << 4,         // déposer dans une machine
        Take = 1 << 5,            // prendre dans une machine
        Sides = 1 << 6,           // configurer les faces
        Craft = 1 << 7,           // fabriquer à l'établi
        Upgrades = 1 << 8,        // installer / retirer des améliorations
        PickUp = 1 << 9,          // démonter
        MachineControl = 1 << 10, // arrêter/démarrer, choisir le procédé, piloter le réacteur
        SimulationSpeed = 1 << 11,// pause / accélération
        All = ~0,
    }

    public static class PlayerPermissionExtensions
    {
        public static string DisplayName(this PlayerPermission p) => p switch
        {
            PlayerPermission.RecipeBook => "ouvrir le livre de recettes",
            PlayerPermission.Inspect => "inspecter les machines",
            PlayerPermission.VaultTake => "prendre dans le vault",
            PlayerPermission.Place => "poser des objets",
            PlayerPermission.Deposit => "déposer dans une machine",
            PlayerPermission.Take => "prendre dans une machine",
            PlayerPermission.Sides => "configurer les faces",
            PlayerPermission.Craft => "fabriquer à l'établi",
            PlayerPermission.Upgrades => "installer des améliorations",
            PlayerPermission.PickUp => "démonter",
            PlayerPermission.MachineControl => "piloter les machines",
            PlayerPermission.SimulationSpeed => "changer la vitesse",
            _ => p.ToString(),
        };
    }
}
