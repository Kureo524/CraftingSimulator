using Nucleaire.Data;
using Nucleaire.Game.UI;
using Nucleaire.Simulation;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Nucleaire.Game
{
    /// <summary>Point d'entrée de la scène : crée la partie à partir des données et assemble monde 3D et interface.</summary>
    public sealed class GameBootstrap : MonoBehaviour
    {
        [Header("Données")]
        [SerializeField] private RecipeDatabase database;
        [SerializeField] private SimulationSettings settings;
        [SerializeField] private TutorialDef tutorial;

        [Header("Rendu")]
        [SerializeField] private UIDocument uiDocument;
        [SerializeField] private Material litMaterial;
        [SerializeField] private Material unlitMaterial;
        [SerializeField] private Material transparentMaterial;

        private GameContext ctx;
        private GameUI ui;
        private GameInput input;
        private IsoCameraRig rig;
        private WorldView worldView;

        private void Awake()
        {
            Application.targetFrameRate = 120;
            Shapes.Lit = litMaterial;
            WorldView.TransparentMaterial = transparentMaterial;
            WorldView.UnlitMaterial = unlitMaterial;

            var session = new GameSession(database, settings, tutorial);
            var runner = gameObject.AddComponent<SimulationRunner>();
            runner.Init(session.World);
            input = new GameInput();
            ctx = new GameContext(session, runner, input);

            BuildLight();
            rig = new GameObject("Camera Rig").AddComponent<IsoCameraRig>();
            rig.Init(session.World.Area);
            session.World.AreaChanged += () => rig.SetBounds(session.World.Area);
            rig.Input = input;
            rig.gameObject.AddComponent<SkyBackdrop>().Init(rig.Camera, unlitMaterial);
            ModelIcons.Init(database, rig.Camera);
            ResourceIcon.ModelIcon = ModelIcons.For;
            var vault = session.World.FirstMachine<VaultMachine>();
            if (vault != null) rig.Focus(GridMath.CellCenter(vault.Position) + new Vector3(0.5f, 0, 1.5f));

            worldView = new GameObject("World").AddComponent<WorldView>();
            worldView.Init(ctx);
            worldView.gameObject.AddComponent<MultiblockFx>().Init(session.World);

            ui = new GameUI(ctx, uiDocument, rig.Camera, worldView);
            input.Suspended = () => ui.IsTyping;
            rig.PointerOverUI = () => Mouse.current != null && ui.IsPointerOverUI(Mouse.current.position.ReadValue());
            // Pendant la pose d'une machine, R la tourne (et ne tourne plus la caméra).
            rig.RotationAllowed = () => !ui.CapturesKeys && !(ctx.BuildItem != null && ctx.BuildPlaceable is MachineDef);

            var build = gameObject.AddComponent<BuildController>();
            build.Init(ctx, rig, worldView);
            build.PointerOverUI = () => BuildController.DebugPointer == null && rig.PointerOverUI();

            var demos = new DemoScenarios(ctx, ui, rig, worldView, build, uiDocument, database);
            gameObject.AddComponent<AutoScreenshot>().Scenario = demos.Run;
        }

        private void OnDestroy() => input?.Dispose();

        private void BuildLight()
        {
            var go = new GameObject("Moonlight");
            var light = go.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = Color.Lerp(Color.white, Palette.Accent, 0.25f);
            light.intensity = 1.25f;
            light.shadows = LightShadows.Soft;
            light.shadowStrength = 0.6f;
            go.transform.rotation = Quaternion.Euler(50f, -35f, 0f);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = Color.Lerp(Palette.Bg, Palette.Accent, 0.35f);
        }

        private void Update()
        {
            if (input.Pressed(GameInput.DeleteToggle) && !ui.IsTyping)
            {
                ui.CloseAll();
                ctx.CycleToolMode();
            }
            if (ctx.ToolModeActive)
            {
                // Modes suppression / modification : aucun panneau, seulement l'atelier et le panneau du mode.
                if (input.Pressed(GameInput.Cancel)) { ctx.Deletion.Exit(); ctx.Configure.Exit(); }
                worldView.UpdateCutaway(rig.Zoom, rig.Focus3, rig.HorizontalForward);
                ui.Update(rig.Yaw, worldView.CutawayActive);
                return;
            }
            if (input.Pressed(GameInput.Inventory)) ui.ToggleInventory();
            if (input.Pressed(GameInput.Workbench)) ui.ToggleWorkbench();
            if (input.Pressed(GameInput.RecipeBook))
            {
                if (ui.Book.IsOpen) ui.Book.Close();
                else ctx.OpenRecipeBook();
            }
            if (input.Pressed(GameInput.Pause) && ctx.Require(PlayerPermission.SimulationSpeed)) ctx.Runner.TogglePause();
            if (input.Pressed(GameInput.Cancel) || (ui.IsTyping && Keyboard.current?.escapeKey.wasPressedThisFrame == true)) Cancel();

            worldView.UpdateCutaway(rig.Zoom, rig.Focus3, rig.HorizontalForward);
            ui.Update(rig.Yaw, worldView.CutawayActive);
        }

        /// <summary>Échap : ferme ce qui est ouvert, du plus récent au plus général ; sinon ouvre les options.</summary>
        private void Cancel()
        {
            if (ui.CancelTransfer() || UI.GlassDropdown.CloseOpen()) { }
            else if (ctx.Plan.Active) ctx.Plan.Clear();
            else if (ctx.ContextTarget != null) ctx.ContextTarget = null;
            else if (ctx.MovingMachine != null) ctx.MovingMachine = null;
            else if (ctx.BuildItem != null) ctx.BuildItem = null;
            else if (!ui.CloseTopPanel())
            {
                if (ctx.Selection != null) ctx.Selection = null;
                else ui.ToggleOptions();
            }
        }
    }
}
