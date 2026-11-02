using System;
using Nucleaire.Data;

namespace Nucleaire.Simulation
{
    public enum SideMode { None, Input, Output }

    /// <summary>Configuration d'une face : entrée, sortie ou fermée, avec un filtre optionnel de ressource.</summary>
    [Serializable]
    public struct SideSetting
    {
        public SideMode Mode;
        /// <summary>Si non nul, seule cette ressource passe par la face.</summary>
        public ResourceDef Filter;
        /// <summary>Emplacement visé (-1 : tous). Ex. « Entrée 2 » = deuxième emplacement d'entrée.</summary>
        public int Slot;

        public SideSetting(SideMode mode, ResourceDef filter = null, int slot = -1) { Mode = mode; Filter = filter; Slot = slot; }

        public bool SameAs(SideSetting o) => Mode == o.Mode && (Mode == SideMode.None || Slot == o.Slot);

        public bool Allows(ResourceDef r) => Filter == null || Filter == r;
    }

    /// <summary>Les six faces d'une machine (quatre côtés, haut, bas).</summary>
    public sealed class SideConfig
    {
        private readonly SideSetting[] sides = new SideSetting[6];

        public event Action Changed;

        public SideConfig(SideMode defaultMode)
        {
            for (int i = 0; i < sides.Length; i++) sides[i] = new SideSetting(defaultMode);
        }

        public SideSetting this[Direction d] => sides[(int)d];

        public void Set(Direction d, SideMode mode, ResourceDef filter = null, int slot = -1)
        {
            sides[(int)d] = new SideSetting(mode, filter, slot);
            Changed?.Invoke();
        }

        public void Set(Direction d, SideSetting s)
        {
            sides[(int)d] = s;
            Changed?.Invoke();
        }

        public void SetMode(Direction d, SideMode mode) => Set(d, mode, sides[(int)d].Filter);
        public void SetFilter(Direction d, ResourceDef filter) => Set(d, sides[(int)d].Mode, filter);

        /// <summary>Fait tourner la face entre Fermée → Entrée → Sortie.</summary>
        public void Cycle(Direction d) => SetMode(d, (SideMode)(((int)sides[(int)d].Mode + 1) % 3));
    }
}
