using System;
using UnityEngine;

namespace Nucleaire.Data
{
    /// <summary>Une quantité d'une ressource (unités pour un solide, mB pour un fluide/gaz, J pour l'énergie).</summary>
    [Serializable]
    public struct ResourceStack
    {
        [SerializeField] private ResourceDef resource;
        [SerializeField, Min(1)] private int amount;

        public ResourceStack(ResourceDef resource, int amount)
        {
            this.resource = resource;
            this.amount = amount;
        }

        public ResourceDef Resource => resource;
        public int Amount => amount;
        public bool IsValid => resource != null && amount > 0;

        public override string ToString() => resource == null ? "(vide)" : resource.Format(amount);
    }
}
