namespace Nucleaire.Data
{
    /// <summary>Silhouette d'une ressource solide dans l'interface (les liquides, gaz et l'énergie ont un rendu fixe).</summary>
    public enum IconShape
    {
        Auto,
        Ore,         // roche grise piquetée de la couleur du métal
        Lump,        // bloc brut (charbon)
        Dust,        // petit tas de poudre
        Ingot,       // lingot en volume
        Crystal,     // gemme hexagonale
        Pellet,      // pastille ronde lumineuse
        Pane,        // plaque translucide (verre)
        Circuit,     // puce à pattes
        Machine,     // cube isométrique
        Transmitter, // tronçon de tuyau
        Upgrade,     // insigne à chevrons
    }
}
