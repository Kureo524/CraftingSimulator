using UnityEngine;

namespace Nucleaire.Data
{
    /// <summary>Objectif affiché au joueur : produire une quantité d'une ressource.</summary>
    [CreateAssetMenu(menuName = "Nucleaire/Objectif", fileName = "NewObjective")]
    public sealed class ObjectiveDef : ScriptableObject
    {
        [SerializeField] private string title = "Objectif";
        [SerializeField, TextArea] private string description;
        [SerializeField] private ResourceStack target;
        [Tooltip("Ordre d'affichage / difficulté croissante.")]
        [SerializeField] private int order;
        [SerializeField, TextArea] private string hint;

        public string Id => name;
        public string Title => title;
        public string Description => description;
        public ResourceStack Target => target;
        public int Order => order;
        public string Hint => hint;

#if UNITY_EDITOR
        public void EditorInit(string t, string desc, ResourceStack tgt, int o, string h)
        {
            title = t; description = desc; target = tgt; order = o; hint = h;
        }
#endif
    }
}
