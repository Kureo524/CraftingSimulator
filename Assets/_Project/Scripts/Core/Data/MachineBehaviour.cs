namespace Nucleaire.Data
{
    /// <summary>Logique de simulation utilisée par une machine.</summary>
    public enum MachineBehaviour
    {
        /// <summary>Exécute des <see cref="ProcessRecipe"/> (la grande majorité des machines, générateurs compris).</summary>
        Processor,
        /// <summary>Stock illimité des ressources de base, sorties filtrées.</summary>
        Vault,
        /// <summary>Obsolète : l'établi est désormais un panneau. Conservé pour la numérotation des assets.</summary>
        Workbench,
        /// <summary>Contrôleur de matrice à induction (multi-bloc) : stocke l'énergie.</summary>
        Battery,
        /// <summary>Détruit un gaz ou un liquide en consommant de l'énergie.</summary>
        Vent,
        /// <summary>Contrôleur de réacteur à fission (multi-bloc) : combustion pilotée, refroidissement à l'eau, chaleur.</summary>
        Reactor,
        /// <summary>Contrôleur de déphaseur supercritique (multi-bloc) : polonium + énergie énorme → antimatière.</summary>
        Sps,
        /// <summary>Contrôleur de réacteur à fusion (multi-bloc) : allumage coûteux, puis énergie massive.</summary>
        FusionReactor,
        /// <summary>Port d'un multi-bloc : entrée / sortie de la structure.</summary>
        MultiblockPort,
    }
}
