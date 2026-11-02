using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Nucleaire.Game
{
    /// <summary>Raccourcis clavier du jeu (Input System), modifiables dans le menu Options.
    /// Les chemins sont « physiques » : &lt;Keyboard&gt;/w correspond à Z sur un clavier AZERTY.</summary>
    public sealed class GameInput : IDisposable
    {
        public const string CameraForward = "camera_forward", CameraBack = "camera_back", CameraLeft = "camera_left", CameraRight = "camera_right";
        public const string RotateCamera = "rotate_camera", Inventory = "inventory", Workbench = "workbench", RecipeBook = "recipe_book";
        public const string ShowRecipe = "show_recipe", ShowUsage = "show_usage", Pause = "pause", Cancel = "cancel";

        private const string PrefsKey = "nucleaire.bindings";

        public static readonly (string id, string label, string path)[] Definitions =
        {
            (CameraForward, "Caméra : avancer", "<Keyboard>/w"),
            (CameraBack, "Caméra : reculer", "<Keyboard>/s"),
            (CameraLeft, "Caméra : gauche", "<Keyboard>/a"),
            (CameraRight, "Caméra : droite", "<Keyboard>/d"),
            (RotateCamera, "Tourner la caméra", "<Keyboard>/r"),
            (Inventory, "Inventaire", "<Keyboard>/e"),
            (Workbench, "Établi", "<Keyboard>/c"),
            (RecipeBook, "Livre de recettes", "<Keyboard>/b"),
            (ShowRecipe, "Voir la recette (objet survolé)", "<Keyboard>/r"),
            (ShowUsage, "Voir les usages (objet survolé)", "<Keyboard>/u"),
            (Pause, "Pause / reprise", "<Keyboard>/space"),
            (Cancel, "Annuler / fermer / options", "<Keyboard>/escape"),
        };

        private readonly InputActionMap map = new("Jeu");
        private readonly Dictionary<string, InputAction> actions = new();
        private InputActionRebindingExtensions.RebindingOperation rebinding;

        public GameInput()
        {
            foreach (var (id, _, path) in Definitions) actions[id] = map.AddAction(id, InputActionType.Button, path);
            try
            {
                var json = PlayerPrefs.GetString(PrefsKey, "");
                if (!string.IsNullOrEmpty(json)) map.LoadBindingOverridesFromJson(json);
            }
            catch (Exception e) { Debug.LogWarning($"Raccourcis enregistrés illisibles, valeurs par défaut utilisées : {e.Message}"); }
            map.Enable();
        }

        public event Action Changed;
        public bool IsRebinding => rebinding != null;

        /// <summary>Désactivé quand un champ texte a le focus ou pendant une réaffectation.</summary>
        public Func<bool> Suspended { get; set; } = () => false;

        public bool Pressed(string id) => !Suspended() && !IsRebinding && actions[id].WasPressedThisFrame();
        public bool Held(string id) => !Suspended() && !IsRebinding && actions[id].IsPressed();

        public string Display(string id)
        {
            var s = actions[id].GetBindingDisplayString(0);
            return string.IsNullOrEmpty(s) ? "—" : s;
        }

        public void StartRebind(string id, Action done)
        {
            CancelRebind();
            var action = actions[id];
            map.Disable();
            rebinding = action.PerformInteractiveRebinding(0)
                .WithControlsExcluding("<Mouse>/position")
                .WithControlsExcluding("<Mouse>/delta")
                .WithCancelingThrough("<Keyboard>/escape")
                .OnMatchWaitForAnother(0.1f)
                .OnComplete(_ => Finish(done))
                .OnCancel(_ => Finish(done))
                .Start();
        }

        private void Finish(Action done)
        {
            rebinding?.Dispose();
            rebinding = null;
            map.Enable();
            Save();
            done?.Invoke();
            Changed?.Invoke();
        }

        public void CancelRebind() => rebinding?.Cancel();

        public void ResetAll()
        {
            map.RemoveAllBindingOverrides();
            Save();
            Changed?.Invoke();
        }

        private void Save()
        {
            PlayerPrefs.SetString(PrefsKey, map.SaveBindingOverridesAsJson());
            PlayerPrefs.Save();
        }

        public void Dispose()
        {
            rebinding?.Dispose();
            map.Dispose();
        }
    }
}
