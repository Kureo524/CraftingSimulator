using UnityEngine;
using UnityEngine.UIElements;

namespace Nucleaire.Game.UI
{
    /// <summary>Menu Options : consulter et modifier les raccourcis clavier.</summary>
    public sealed class OptionsPanel
    {
        private readonly GameContext ctx;
        private readonly VisualElement layer, list;
        private int lastRebindFrame = -10;

        public OptionsPanel(GameContext ctx, VisualElement root)
        {
            this.ctx = ctx;
            layer = root.Q("options-layer");
            var panel = root.Q("options");
            layer.RegisterCallback<PointerDownEvent>(evt => { if (evt.target == layer && !ctx.Input.IsRebinding) Close(); });

            var head = Ui.El("row", panel);
            Ui.Text("Options", "sheet-title grow", head);
            Ui.Button("×", Close, "btn btn--ghost btn--icon", head);

            var sec = Ui.Section("Raccourcis clavier", panel, "Clique sur un raccourci puis appuie sur la nouvelle touche (Échap pour annuler). Les raccourcis sont enregistrés.");
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.AddToClassList("options__scroll");
            sec.Add(scroll);
            list = scroll.contentContainer;

            var mouse = Ui.Section("Souris", panel);
            foreach (var (label, value) in new[]
                     {
                         ("Inspecter une machine", "Clic gauche"), ("Menu de la machine (déplacer, supprimer)", "Clic droit"),
                         ("Zoom (vue en coupe en zoomant)", "Molette"), ("Déplacer la caméra", "Clic molette maintenu"),
                         ("Tourner dans l'autre sens", "Maj + rotation"),
                     })
            {
                var row = Ui.El("row options__row", mouse);
                Ui.Text(label, "grow text-2", row);
                Ui.Text(value, "mono", row);
            }

            var actions = Ui.El("row options__actions", panel);
            Ui.Button("Réinitialiser les raccourcis", () => { ctx.Input.ResetAll(); Rebuild(); }, "btn btn--ghost", actions);
            Ui.El("spacer", actions);
            Ui.Button("Quitter le jeu", Application.Quit, "btn btn--danger", actions);
            Ui.Button("Reprendre", Close, "btn btn--primary", actions).style.marginLeft = 6;

            ctx.Input.Changed += Rebuild;
            Ui.SetVisible(layer, false);
        }

        public bool IsOpen => !layer.ClassListContains("hidden");
        /// <summary>Vrai juste après une réaffectation (l'Échap d'annulation ne doit pas fermer le menu).</summary>
        public bool JustRebound => Time.frameCount - lastRebindFrame < 3 || ctx.Input.IsRebinding;

        public void Toggle() { if (IsOpen) Close(); else Open(); }
        public void Open() { Ui.SetVisible(layer, true); Rebuild(); }

        public void Close()
        {
            ctx.Input.CancelRebind();
            Ui.SetVisible(layer, false);
        }

        private void Rebuild()
        {
            list.Clear();
            foreach (var (id, label, _) in GameInput.Definitions)
            {
                var row = Ui.El("row options__row", list);
                Ui.Text(label, "grow", row);
                Button key = null;
                key = Ui.Button(ctx.Input.Display(id), () =>
                {
                    key.text = "Appuie sur une touche…";
                    key.AddToClassList("btn--active");
                    ctx.Input.StartRebind(id, () => { lastRebindFrame = Time.frameCount; });
                }, "btn btn--small options__key", row);
            }
        }
    }
}
