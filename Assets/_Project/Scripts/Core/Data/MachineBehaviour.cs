namespace Nucleaire.Data
{
    /// <summary>Logique de simulation utilisée par une machine.
    /// Les valeurs sont figées : elles sont sérialisées dans les assets (la valeur 2, ancien établi posable, est libre).</summary>
    public enum MachineBehaviour
    {
        /// <summary>Exécute des <see cref="ProcessRecipe"/> (la grande majorité des machines, générateurs compris).</summary>
        Processor = 0,
        /// <summary>Stock illimité des ressources de base, sorties filtrées.</summary>
        Vault = 1,
        /// <summary>Contrôleur de matrice à induction (multi-bloc) : stocke l'énergie.</summary>
        Battery = 3,
        /// <summary>Détruit un gaz ou un liquide en consommant de l'énergie.</summary>
        Vent = 4,
        /// <summary>Contrôleur de réacteur à fission (multi-bloc) : combustion pilotée, refroidissement à l'eau, chaleur.</summary>
        Reactor = 5,
        /// <summary>Contrôleur de déphaseur supercritique (multi-bloc) : polonium + énergie énorme → antimatière.</summary>
        Sps = 6,
        /// <summary>Contrôleur de réacteur à fusion (multi-bloc) : allumage coûteux, puis énergie massive.</summary>
        FusionReactor = 7,
        /// <summary>Port d'un multi-bloc : entrée / sortie de la structure.</summary>
        MultiblockPort = 8,
    }
}
