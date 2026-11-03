using UnityEngine;

namespace Nucleaire.Data
{
    /// <summary>Une ressource du jeu : minerai, lingot, gaz, liquide, machine en tant qu'objet, amélioration…</summary>
    [CreateAssetMenu(menuName = "Nucleaire/Ressource", fileName = "NewResource")]
    public sealed class ResourceDef : ScriptableObject
    {
        [SerializeField] private string displayName = "Ressource";
        [SerializeField, TextArea] private string description;
        [SerializeField] private ResourceKind kind = ResourceKind.Item;
        [SerializeField] private Color color = Color.white;
        [Tooltip("Ressource de base : disponible en quantité illimitée dans le vault.")]
        [SerializeField] private bool isBaseResource;
        [Tooltip("Forme de l'icône dans l'interface. Auto : déduite de l'identifiant (minerai, poudre, lingot…). Les liquides et gaz ont toujours leur rendu propre.")]
        [SerializeField] private IconShape iconShape = IconShape.Auto;
        [Tooltip("Formule ou symbole affiché sur l'icône des liquides et des gaz (H₂, UF₆…), pour les distinguer d'un coup d'œil.")]
        [SerializeField] private string symbol;

        /// <summary>Identifiant stable = nom de l'asset.</summary>
        public string Id => name;
        public string DisplayName => displayName;
        public string Description => description;
        public ResourceKind Kind => kind;
        public Color Color => color;
        public bool IsBaseResource => isBaseResource;
        public IconShape IconShape => iconShape;
        public string Symbol => symbol;

        public string Format(int amount) => $"{amount}{kind.Unit()} {displayName}";

#if UNITY_EDITOR
        public void EditorInit(string display, ResourceKind k, Color c, bool isBase, string desc)
        {
            displayName = display; kind = k; color = c; isBaseResource = isBase; description = desc;
        }
#endif
    }
}
