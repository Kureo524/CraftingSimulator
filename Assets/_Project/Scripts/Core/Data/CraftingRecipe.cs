using System.Collections.Generic;
using UnityEngine;

namespace Nucleaire.Data
{
    /// <summary>Recette de l'établi : ingrédients solides → un objet (machine, transmetteur, amélioration, composant).</summary>
    [CreateAssetMenu(menuName = "Nucleaire/Recette d'établi", fileName = "NewCrafting")]
    public sealed class CraftingRecipe : ScriptableObject
    {
        [SerializeField] private List<ResourceStack> ingredients = new();
        [SerializeField] private ResourceStack result;

        public string Id => name;
        public IReadOnlyList<ResourceStack> Ingredients => ingredients;
        public ResourceStack Result => result;

#if UNITY_EDITOR
        public void EditorInit(List<ResourceStack> ins, ResourceStack res) { ingredients = ins; result = res; }
#endif
    }
}
