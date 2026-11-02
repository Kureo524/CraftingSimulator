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
        private IsoCameraRig rig;
        private WorldView worldView;

        private void Awake()
        {
            Application.targetFrameRate = 120;
            Shapes.Lit = litMaterial;
            WorldView.TransparentMaterial = transparentMaterial;

            var session = new GameSession(database, settings);
            var runner = gameObject.AddComponent<SimulationRunner>();
            runner.Init(session.World);
            ctx = new GameContext(session, runner);

            BuildLight();
            rig = new GameObject("Camera Rig").AddComponent<IsoCameraRig>();
            rig.Init(settings.gridSize);
            rig.gameObject.AddComponent<SkyBackdrop>().Init(rig.Camera, unlitMaterial);
            if (session.World.Machines.Count > 0) rig.Focus(GridMath.CellCenter(session.World.Machines[0].Position) + new Vector3(1f, 0, 1.5f));

            worldView = new GameObject("World").AddComponent<WorldView>();
            worldView.Init(ctx, unlitMaterial);

            ui = new GameUI(ctx, uiDocument, rig.Camera, new Objectives.TutorialTracker(session, tutorial));
            rig.PointerOverUI = () => Mouse.current != null && ui.IsPointerOverUI(Mouse.current.position.ReadValue());
            rig.IsTyping = () => ui.IsTyping;

            var build = gameObject.AddComponent<BuildController>();
            build.Init(ctx, rig, worldView);
            build.PointerOverUI = rig.PointerOverUI;

            var shots = gameObject.AddComponent<AutoScreenshot>();
            shots.Scenario = DemoScenario;
        }

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
            var kb = Keyboard.current;
            if (kb != null && !ui.IsTyping)
            {
                if (kb.bKey.wasPressedThisFrame) ui.Book.Toggle();
                if (kb.spaceKey.wasPressedThisFrame) ctx.Runner.TogglePause();
                if (kb.escapeKey.wasPressedThisFrame)
                {
                    if (ui.Book.IsOpen) ui.Book.Close();
                    else if (ctx.BuildItem == null) ctx.Selection = null;
                }
            }
            else if (kb != null && kb.escapeKey.wasPressedThisFrame && ui.Book.IsOpen) ui.Book.Close();

            ui.Update(rig.Yaw);
        }

        // ---------------- Scénarios de captures (vérification) ----------------

        private IEnumerator DemoScenario(string name)
        {
            var w = ctx.World;
            var vault = w.FirstMachine<VaultMachine>();
            var origin = vault.Position + new Vector2Int(2, 2);
            switch (name)
            {
                case "overview":
                    ui.Book.Close();
                    ctx.Selection = null;
                    break;
                case "chain":
                    // Petite chaîne : panneaux solaires → câbles → four ; vault → convoyeur → broyeur → four.
                    var inv = ctx.Session.Inventory;
                    inv.Add(R("item_solar_panel"), 4);
                    inv.Add(R("item_electrolyser"), 1);
                    inv.Add(R("item_fission_reactor"), 1);
                    inv.Add(R("item_pressurized_tube"), 8);
                    var a = ctx.Session.Actions;
                    a.TryPlace(R("item_solar_panel"), origin, out _);
                    a.TryPlace(R("item_solar_panel"), origin + Vector2Int.up, out _);
                    for (int i = 1; i <= 3; i++) a.TryPlace(R("item_cable"), origin + new Vector2Int(i, 0), out _);
                    a.TryPlace(R("item_electric_furnace"), origin + new Vector2Int(4, 0), out _);
                    a.TryPlace(R("item_crusher"), origin + new Vector2Int(4, 1), out _);
                    ((Machine)w.At(origin + new Vector2Int(4, 1))).Sides.SetMode(Direction.South, SideMode.Output);
                    vault.Sides.Set(Direction.East, SideMode.Output, R("iron_ore"));
                    for (int i = 1; i <= 3; i++) a.TryPlace(R("item_conveyor"), vault.Position + new Vector2Int(i, 0), out _);
                    a.TryPlace(R("item_fission_reactor"), origin + new Vector2Int(-1, 4), out _);
                    a.TryPlace(R("item_electrolyser"), origin + new Vector2Int(2, 4), out _);
                    for (int i = 0; i < 3; i++) a.TryPlace(R("item_pressurized_tube"), origin + new Vector2Int(3 + i, 4), out _);
                    ctx.Runner.SetSpeedIndex(3);
                    yield return new WaitForSecondsRealtime(3f);
                    ctx.Selection = w.At(origin + new Vector2Int(4, 0));
                    break;
                case "inspector":
                    ctx.Selection = w.Machines.OfType<ProcessorMachine>().FirstOrDefault(m => m.MachineDef.Id == "electric_furnace") ?? (IGridEntity)vault;
                    break;
                case "sides":
                    ctx.Selection = vault;
                    yield return null;
                    uiDocument.rootVisualElement.Q("inspector").Q<SegmentedControl>()?.Select(1);
                    break;
                case "reactor":
                    var reactor = w.FirstMachine<ReactorMachine>();
                    if (reactor != null)
                    {
                        reactor.InsertManual(R("fissile_fuel"), 2000);
                        reactor.SetBurnRate(6);
                        ctx.Selection = reactor;
                        yield return new WaitForSecondsRealtime(2f);
                    }
                    break;
                case "workbench":
                    ctx.Selection = w.FirstMachine<WorkbenchMachine>();
                    break;
                case "tutorial":
                    ui.Book.Close();
                    ctx.Selection = null;
                    break;
                case "book":
                    ctx.Selection = null;
                    ctx.OpenRecipeBook(R("fissile_fuel"));
                    break;
            }
            yield return null;
        }

        private ResourceDef R(string id) => database.Resources.First(r => r.Id == id);
    }
}
