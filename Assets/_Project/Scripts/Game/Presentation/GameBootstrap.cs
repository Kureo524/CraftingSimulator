using System.Collections;
using System.Linq;
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
            rig.Init(settings.gridSize);
            rig.Input = input;
            rig.gameObject.AddComponent<SkyBackdrop>().Init(rig.Camera, unlitMaterial);
            var vault = session.World.FirstMachine<VaultMachine>();
            if (vault != null) rig.Focus(GridMath.CellCenter(vault.Position) + new Vector3(0.5f, 0, 1.5f));

            worldView = new GameObject("World").AddComponent<WorldView>();
            worldView.Init(ctx);

            ui = new GameUI(ctx, uiDocument, rig.Camera, worldView);
            input.Suspended = () => ui.IsTyping;
            rig.PointerOverUI = () => Mouse.current != null && ui.IsPointerOverUI(Mouse.current.position.ReadValue());
            rig.RotationAllowed = () => !ui.CapturesKeys;

            var build = gameObject.AddComponent<BuildController>();
            build.Init(ctx, rig, worldView);
            build.PointerOverUI = rig.PointerOverUI;

            gameObject.AddComponent<AutoScreenshot>().Scenario = DemoScenario;
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
            if (ctx.ContextTarget != null) ctx.ContextTarget = null;
            else if (ctx.MovingMachine != null) ctx.MovingMachine = null;
            else if (ctx.BuildItem != null) ctx.BuildItem = null;
            else if (!ui.CloseTopPanel())
            {
                if (ctx.Selection != null) ctx.Selection = null;
                else ui.ToggleOptions();
            }
        }

        // ---------------- Scénarios de captures (vérification) ----------------

        private IEnumerator DemoScenario(string name)
        {
            var w = ctx.World;
            var vault = w.FirstMachine<VaultMachine>();
            var origin = vault.Position + new Vector3Int(2, 2, 0);
            var a = ctx.Session.Actions;
            switch (name)
            {
                case "overview":
                    ui.CloseAll();
                    ctx.Selection = null;
                    break;
                case "tutorial":
                    ui.CloseAll();
                    ctx.Selection = null;
                    break;
                case "skip":
                    ctx.Session.Tutorial.Skip();
                    break;
                case "chain":
                    ctx.Session.Tutorial.Skip();
                    var inv = ctx.Session.Inventory;
                    inv.Add(R("item_solar_panel"), 4);
                    inv.Add(R("item_electrolyser"), 1);
                    inv.Add(R("item_fission_reactor"), 1);
                    inv.Add(R("item_pressurized_tube"), 8);
                    inv.Add(R("item_crusher"), 1);
                    a.TryPlace(R("item_solar_panel"), origin, out _);
                    a.TryPlace(R("item_solar_panel"), origin + new Vector3Int(0, 1, 0), out _);
                    for (int i = 1; i <= 3; i++) a.TryPlace(R("item_cable"), origin + new Vector3Int(i, 0, 0), out _);
                    a.TryPlace(R("item_electric_furnace"), origin + new Vector3Int(4, 0, 0), out _);
                    // Broyeur posé au-dessus du four : il pousse vers le bas.
                    a.TryPlace(R("item_crusher"), origin + new Vector3Int(4, 0, 1), out _);
                    var crusher = (Machine)w.At(origin + new Vector3Int(4, 0, 1));
                    foreach (var d in DirectionExtensions.All) crusher.Sides.SetMode(d, SideMode.Input);
                    crusher.Sides.SetMode(Direction.Down, SideMode.Output);
                    crusher.InsertManual(R("iron_ore"), 10);
                    vault.Sides.Set(Direction.East, SideMode.Output, R("iron_ore"));
                    for (int i = 1; i <= 3; i++) a.TryPlace(R("item_conveyor"), vault.Position + new Vector3Int(i, 0, 0), out _);
                    a.TryPlace(R("item_fission_reactor"), origin + new Vector3Int(-1, 4, 0), out _);
                    a.TryPlace(R("item_electrolyser"), origin + new Vector3Int(2, 4, 0), out _);
                    for (int i = 0; i < 3; i++) a.TryPlace(R("item_pressurized_tube"), origin + new Vector3Int(3 + i, 4, 0), out _);
                    a.TryPlace(R("item_pressurized_tube"), origin + new Vector3Int(5, 4, 1), out _);
                    ctx.Runner.SetSpeedIndex(3);
                    yield return new WaitForSecondsRealtime(3f);
                    ctx.Selection = w.At(origin + new Vector3Int(4, 0, 0));
                    break;
                case "inspector":
                    ui.CloseAll();
                    ctx.Selection = w.Machines.OfType<ProcessorMachine>().FirstOrDefault(m => m.MachineDef.Id == "electrolyser") ?? (IGridEntity)vault;
                    break;
                case "sides":
                    ctx.Selection = vault;
                    yield return null;
                    uiDocument.rootVisualElement.Q("inspector").Q<SegmentedControl>()?.Select(1);
                    break;
                case "context":
                    ctx.Selection = null;
                    ctx.ContextTarget = w.Machines.OfType<ProcessorMachine>().FirstOrDefault() ?? (IGridEntity)vault;
                    break;
                case "reactor":
                    ctx.ContextTarget = null;
                    var reactor = w.FirstMachine<ReactorMachine>();
                    if (reactor != null)
                    {
                        reactor.InsertManual(R("fissile_fuel"), 2000);
                        reactor.InsertManual(R("water"), 3000);
                        reactor.SetBurnRate(6);
                        ctx.Selection = reactor;
                        yield return new WaitForSecondsRealtime(1f);
                    }
                    break;
                case "cutaway":
                    ui.CloseAll();
                    ctx.Selection = null;
                    for (int i = 0; i < 6; i++) { Mouse.current?.WarpCursorPosition(new Vector2(Screen.width / 2f, Screen.height / 2f)); yield return null; }
                    rig.SetZoomForTests(4.5f);
                    rig.Focus(GridMath.CellCenter(origin + new Vector3Int(3, 2, 0)));
                    yield return new WaitForSecondsRealtime(1f);
                    break;
                case "workbench":
                    ctx.Selection = null;
                    rig.SetZoomForTests(7.5f);
                    ui.CloseAll();
                    ctx.OpenWorkbench();
                    break;
                case "inventory":
                    ui.CloseAll();
                    ui.ToggleInventory();
                    break;
                case "options":
                    ui.CloseAll();
                    ui.ToggleOptions();
                    break;
                case "book":
                    ui.CloseAll();
                    ctx.OpenRecipeBook(R("fissile_fuel"));
                    break;
            }
            yield return null;
        }

        private ResourceDef R(string id) => database.Resources.First(r => r.Id == id);
    }
}
