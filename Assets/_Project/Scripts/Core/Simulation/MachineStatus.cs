namespace Nucleaire.Simulation
{
    /// <summary>État lisible d'une machine, affiché au-dessus d'elle et dans son panel.</summary>
    public enum MachineStatus
    {
        Idle,          // rien à faire
        Working,       // en marche
        MissingInput,  // ingrédients manquants
        NoPower,       // aucune énergie
        LowPower,      // énergie insuffisante : ralentie
        OutputFull,    // sortie pleine : bloquée
        Overheating,   // réacteur : chaleur élevée
        Meltdown,      // réacteur : surchauffe, arrêt d'urgence
        Disabled,      // arrêtée par le joueur
    }

    public static class MachineStatusExtensions
    {
        public static bool IsProblem(this MachineStatus s) =>
            s is MachineStatus.MissingInput or MachineStatus.NoPower or MachineStatus.LowPower
                or MachineStatus.OutputFull or MachineStatus.Overheating or MachineStatus.Meltdown;

        public static string DisplayName(this MachineStatus s) => s switch
        {
            MachineStatus.Idle => "En attente",
            MachineStatus.Working => "En marche",
            MachineStatus.MissingInput => "Entrée manquante",
            MachineStatus.NoPower => "Pas d'énergie",
            MachineStatus.LowPower => "Énergie insuffisante",
            MachineStatus.OutputFull => "Sortie pleine",
            MachineStatus.Overheating => "Surchauffe imminente",
            MachineStatus.Meltdown => "Arrêt d'urgence",
            MachineStatus.Disabled => "Arrêtée",
            _ => s.ToString(),
        };
    }
}
