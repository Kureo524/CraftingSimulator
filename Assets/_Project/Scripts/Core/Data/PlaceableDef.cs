using UnityEngine;

namespace Nucleaire.Data
{
    /// <summary>Tout ce qui se pose sur la grille de l'atelier (machine ou transmetteur).
    /// L'objet d'inventaire correspondant est <see cref="Item"/>.</summary>
    public abstract class PlaceableDef : ScriptableObject
    {
        [SerializeField] private string displayName = "Placeable";
        [SerializeField, TextArea] private string description;
        [Tooltip("Objet d'inventaire qui permet de poser cet élément.")]
        [SerializeField] private ResourceDef item;
        [SerializeField] private Color color = Color.gray;

        public string Id => name;
        public string DisplayName => displayName;
        public string Description => description;
        public ResourceDef Item => item;
        public Color Color => color;

#if UNITY_EDITOR
        public void EditorInitPlaceable(string display, string desc, ResourceDef itemDef, Color c)
        {
            displayName = display; description = desc; item = itemDef; color = c;
        }
#endif
    }
}
