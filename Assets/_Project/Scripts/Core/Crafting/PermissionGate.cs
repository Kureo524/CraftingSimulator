using System;
using Nucleaire.Data;

namespace Nucleaire.Crafting
{
    /// <summary>Filtre les actions du joueur (utilisé par le tutoriel). Par défaut, tout est permis.</summary>
    public sealed class PermissionGate
    {
        private PlayerPermission allowed = PlayerPermission.All;

        public PlayerPermission Allowed => allowed;
        public event Action Changed;

        public void Set(PlayerPermission p)
        {
            if (allowed == p) return;
            allowed = p;
            Changed?.Invoke();
        }

        public bool IsAllowed(PlayerPermission p) => (allowed & p) == p;

        public bool Check(PlayerPermission p, out string error)
        {
            error = IsAllowed(p) ? null : $"Pas encore : tu pourras {p.DisplayName()} plus loin dans le tutoriel (ou passe le tutoriel).";
            return error == null;
        }
    }
}
