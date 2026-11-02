namespace Nucleaire.Data
{
    /// <summary>Logique de simulation utilisée par une machine.</summary>
    public enum MachineBehaviour
    {
        /// <summary>Exécute des <see cref="ProcessRecipe"/> (la grande majorité des machines, générateurs compris).</summary>
        Processor,
        /// <summary>Stock illimité des ressources de base, sorties filtrées.</summary>
        Vault,
        /// <summary>Craft manuel des objets (machines, transmetteurs, améliorations).</summary>
        Workbench,
        /// <summary>Stocke de l'énergie.</summary>
        Battery,
        /// <summary>Détruit un gaz ou un liquide en consommant de l'énergie.</summary>
        Vent,
        /// <summary>Réacteur à fission : combustion pilotée, refroidissement à l'eau, chaleur.</summary>
        Reactor,
    }
}
