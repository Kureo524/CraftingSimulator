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
            // Pendant la pose d'une machine, R la tourne (et ne tourne plus la caméra).
            rig.RotationAllowed = () => !ui.CapturesKeys && !(ctx.BuildItem != null && ctx.BuildPlaceable is MachineDef);

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
                case "quest":
                    ui.CloseAll();
                    while (ctx.Session.Tutorial.Index < 2) ctx.Session.Tutorial.Next();
                    ctx.Selection = null;
                    yield return new WaitForSecondsRealtime(1.5f);
                    break;
                case "quest-vault":
                    ctx.Selection = vault;
                    yield return new WaitForSecondsRealtime(1f);
                    break;
                case "quest-done":
                    ctx.Session.Actions.TakeFromVault(vault, R("iron_ore"), 10);
                    yield return new WaitForSecondsRealtime(0.8f);
                    break;
                case "quest-place":
                    ctx.Selection = null;
                    yield return new WaitForSecondsRealtime(2f);
                    break;
                case "rules":
                    while (ctx.Session.Tutorial.Index < 9) ctx.Session.Tutorial.Next();
                    yield return new WaitForSecondsRealtime(1f);
                    break;
                case "machine":
                    ctx.Session.Tutorial.Skip();
                    ctx.Session.Inventory.Add(R("item_electric_furnace"), 1);
                    ctx.Session.Inventory.Add(R("iron_ore"), 24);
                    ctx.Session.Inventory.Add(R("coal"), 12);
                    ctx.Session.Inventory.Add(R("water"), 9500);
                    ctx.Session.Inventory.Add(R("hydrogen"), 2500);
                    ctx.Session.Actions.TryPlace(R("item_electric_furnace"), vault.Position + new Vector3Int(3, 0, 0), out _);
                    ctx.Selection = w.At(vault.Position + new Vector3Int(3, 0, 0));
                    yield return new WaitForSecondsRealtime(0.8f);
                    break;
                case "vault-pick":
                    ctx.Selection = vault;
                    yield return null;
                    var pick = uiDocument.rootVisualElement.Q("inspector").Query(className: "rslot--vault").AtIndex(3);
                    if (pick != null) using (var click = ClickEvent.GetPooled()) { click.target = pick; pick.SendEvent(click); }
                    yield return new WaitForSecondsRealtime(0.5f);
                    break;
                case "quest-craft":
                    ui.CloseAll();
                    ctx.Selection = null;
                    while (ctx.Session.Tutorial.Index < 8) ctx.Session.Tutorial.Next();
                    yield return new WaitForSecondsRealtime(0.5f);
                    ctx.OpenWorkbench();
                    yield return new WaitForSecondsRealtime(1f);
                    break;
                case "filter":
                {
                    ui.CloseAll();
                    ctx.Selection = null;
                    var root = uiDocument.rootVisualElement;
                    var toggle = root.Q<Button>(className: "view-filter__toggle");
                    using (var submit = NavigationSubmitEvent.GetPooled()) { submit.target = toggle; toggle.SendEvent(submit); }
                    yield return null;
                    Click(root.Q(className: "view-filter__dropdown"));
                    yield return new WaitForSecondsRealtime(0.4f);
                    break;
                }
                case "sides-dd":
                    Click(uiDocument.rootVisualElement.Q("inspector").Q(className: "gdrop"));
                    yield return new WaitForSecondsRealtime(0.4f);
                    break;
                case "quest-book":
                    ui.CloseAll();
                    while (ctx.Session.Tutorial.Index < 2) ctx.Session.Tutorial.Next();
                    ctx.OpenRecipeBook(R("iron_ore"));
                    yield return new WaitForSecondsRealtime(1f);
                    break;
                case "quest-solar":
                    ui.CloseAll();
                    while (ctx.Session.Tutorial.Index < 3) ctx.Session.Tutorial.Next();
                    ctx.Selection = vault;
                    yield return new WaitForSecondsRealtime(1f);
                    break;
                case "quest-hotbar":
                    ui.CloseAll();
                    ctx.Selection = null;
                    yield return new WaitForSecondsRealtime(1f);
                    break;
                case "cable":
                    ctx.Session.Tutorial.Skip();
                    ui.CloseAll();
                    yield return null;
                    ctx.Hotbar.Select(ctx.Hotbar.IndexOf(R("item_universal_cable")));
                    ctx.CableKind = ResourceKind.Gas;
                    yield return new WaitForSecondsRealtime(0.5f);
                    break;
                case "build-preview":
                    ctx.Session.Tutorial.Skip();
                    ui.CloseAll();
                    yield return null;
                    ctx.Hotbar.Select(ctx.Hotbar.IndexOf(R("item_electric_furnace")));
                    ctx.BuildFacing = Direction.East;
                    for (int i = 0; i < 8; i++) { Mouse.current?.WarpCursorPosition(new Vector2(Screen.width * 0.42f, Screen.height * 0.45f)); yield return null; }
                    yield return new WaitForSecondsRealtime(0.4f);
                    break;
                case "plan":
                {
                    ctx.Hotbar.Select(ctx.Hotbar.IndexOf(R("item_universal_cable")));
                    ctx.CableKind = ResourceKind.Fluid;
                    var start = vault.Position + new Vector3Int(-3, 2, 0);
                    for (int i = 0; i < 6; i++) ctx.Plan.Add(start + new Vector3Int(i, 0, 0));
                    for (int i = 1; i < 4; i++) ctx.Plan.Add(start + new Vector3Int(5, -i, 0));
                    yield return new WaitForSecondsRealtime(0.5f);
                    break;
                }
                case "delete":
                {
                    ctx.Plan.Confirm();
                    ctx.BuildItem = null;
                    ctx.Session.Inventory.Add(R("item_crusher"), 1);
                    ctx.Session.Actions.TryPlace(R("item_crusher"), vault.Position + new Vector3Int(2, 0, 0), out _, null, Direction.South);
                    ctx.Deletion.Enter();
                    foreach (var e in w.Entities.Where(e => e.Position.y == vault.Position.y + 2).Take(3).ToList()) ctx.Deletion.Mark(e, true);
                    ctx.Deletion.Mark(w.At(vault.Position + new Vector3Int(2, 0, 0)), true);
                    yield return new WaitForSecondsRealtime(0.5f);
                    break;
                }
                case "facings":
                {
                    ctx.Session.Tutorial.Skip();
                    ui.CloseAll();
                    yield return null;
                    ctx.Session.Inventory.Add(R("item_crusher"), 4);
                    int k = 0;
                    foreach (var d in DirectionExtensions.Horizontal)
                        ctx.Session.Actions.TryPlace(R("item_crusher"), vault.Position + new Vector3Int(-3 + 2 * k++, 3, 0), out _, null, d);
                    ctx.Hotbar.Select(ctx.Hotbar.IndexOf(R("item_electric_furnace")));
                    ctx.BuildFacing = Direction.West;
                    ctx.Plan.Add(vault.Position + new Vector3Int(-2, -2, 0));
                    ctx.Plan.Add(vault.Position + new Vector3Int(0, -2, 0));
                    rig.SetZoomForTests(5f);
                    rig.Focus(GridMath.CellCenter(vault.Position));
                    yield return new WaitForSecondsRealtime(0.6f);
                    break;
                }
                case "sorter":
                {
                    ctx.Session.Tutorial.Skip();
                    ui.CloseAll();
                    yield return null;
                    vault = w.FirstMachine<VaultMachine>();
                    var fs = vault.AttachSorter(Direction.North);
                    fs.AddFilter(R("iron_ore"));
                    fs.AddFilter(R("coal"));
                    w.Place(T(ResourceKind.Item), vault.Position + new Vector3Int(0, 1, 0));
                    w.Place(T(ResourceKind.Item), vault.Position + new Vector3Int(0, 2, 0));
                    var furnace = (Machine)w.Place(database.Machines.First(m => m.Id == "electric_furnace"), vault.Position + new Vector3Int(0, 3, 0), Direction.South);
                    furnace.AttachSorter(Direction.East).AddFilter(R("steel_ingot"));
                    vault.AttachSorter(Direction.West);
                    furnace.AttachSorter(Direction.West);
                    rig.SetZoomForTests(7.2f);
                    rig.Focus(GridMath.CellCenter(vault.Position + new Vector3Int(0, 2, 0)));
                    yield return new WaitForSecondsRealtime(1.2f);
                    ctx.FocusSorterFace = Direction.North;
                    ctx.Selection = vault;
                    yield return new WaitForSecondsRealtime(0.5f);
                    break;
                }
                case "configure":
                {
                    ui.CloseAll();
                    ctx.Selection = null;
                    var seg = (Nucleaire.Logistics.TransmitterSegment)w.At(vault.Position + new Vector3Int(0, 2, 0));
                    w.Logistics.CycleCableSide(seg, Direction.North, 2); // pousser vers le four
                    w.Logistics.CycleCableSide((Nucleaire.Logistics.TransmitterSegment)w.At(vault.Position + new Vector3Int(0, 1, 0)), Direction.North); // couper
                    var furnace = (Machine)w.At(vault.Position + new Vector3Int(0, 3, 0));
                    furnace.CycleSide(Direction.West, 3);
                    ctx.Configure.Enter();
                    ctx.Configure.Report("Four électrique · face ouest : " + furnace.SideLabel(Direction.West));
                    yield return new WaitForSecondsRealtime(0.5f);
                    break;
                }
                case "sorter-world":
                    ui.CloseAll();
                    ctx.Selection = null;
                    yield return new WaitForSecondsRealtime(0.5f);
                    break;
                case "level":
                {
                    ctx.Session.Tutorial.Skip();
                    ui.CloseAll();
                    yield return null;
                    vault = w.FirstMachine<VaultMachine>();
                    ctx.Hotbar.Select(ctx.Hotbar.IndexOf(R("item_electric_furnace")));
                    GetComponent<BuildController>().enabled = false;
                    var pv = worldView.GetComponent<BuildPreview>();
                    var furnaceDef = database.Machines.First(m => m.Id == "electric_furnace");
                    for (int i = 0; i < 20; i++) { pv.SetHover(furnaceDef, Direction.North, vault.Position + new Vector3Int(0, 0, 1), true); yield return null; }
                    yield return new WaitForSecondsRealtime(0.3f);
                    break;
                }
                case "multiblock":
                {
                    ctx.Session.Tutorial.Skip();
                    ui.CloseAll();
                    yield return null;
                    vault = w.FirstMachine<VaultMachine>();
                    var o = vault.Position + new Vector3Int(-6, 2, 0);
                    var size = new Vector3Int(5, 5, 3);
                    var max = o + size - Vector3Int.one;
                    StructureDef Sd(string id) => database.Structures.First(x => x.Id == id);
                    for (int z = o.z; z <= max.z; z++)
                    for (int y = o.y; y <= max.y; y++)
                    for (int x = o.x; x <= max.x; x++)
                    {
                        var c = new Vector3Int(x, y, z);
                        int border = (x == o.x || x == max.x ? 1 : 0) + (y == o.y || y == max.y ? 1 : 0) + (z == o.z || z == max.z ? 1 : 0);
                        if (border == 0) { w.Place(Sd("fuel_assembly"), c); continue; }
                        if (c == new Vector3Int(o.x + 2, o.y, 1)) { w.Place(database.Machines.First(m => m.Id == "fission_reactor"), c, Direction.South); continue; }
                        if (c == new Vector3Int(max.x, o.y + 2, 1) || c == new Vector3Int(max.x, o.y + 1, 1)) { w.Place(database.Machines.First(m => m.Id == "multiblock_port"), c); continue; }
                        bool glass = border == 1 && z == 1 && (y == o.y || x == o.x);
                        w.Place(Sd(glass ? "structural_glass" : "structural_frame"), c);
                    }
                    w.Place(database.Machines.First(m => m.Id == "supercritical_shifter"), vault.Position + new Vector3Int(4, 2, 0), Direction.South);
                    ctx.Creative = true;
                    rig.SetZoomForTests(9f);
                    rig.Focus(GridMath.CellCenter(vault.Position + new Vector3Int(-1, 4, 0)));
                    yield return new WaitForSecondsRealtime(1f);
                    break;
                }
                case "multiblock-ui":
                    ctx.Selection = w.Machines.First(m => m.MachineDef.Id == "fission_reactor");
                    yield return null;
                    uiDocument.rootVisualElement.Q("inspector").Q<SegmentedControl>()?.Select(0);
                    yield return new WaitForSecondsRealtime(0.5f);
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
                    inv.Add(R("item_universal_cable"), 16);
                    inv.Add(R("item_crusher"), 1);
                    a.TryPlace(R("item_solar_panel"), origin, out _);
                    a.TryPlace(R("item_solar_panel"), origin + new Vector3Int(0, 1, 0), out _);
                    for (int i = 1; i <= 3; i++) a.TryPlace(R("item_universal_cable"), origin + new Vector3Int(i, 0, 0), out _, T(ResourceKind.Energy));
                    a.TryPlace(R("item_electric_furnace"), origin + new Vector3Int(4, 0, 0), out _);
                    // Broyeur posé au-dessus du four : il pousse vers le bas.
                    a.TryPlace(R("item_crusher"), origin + new Vector3Int(4, 0, 1), out _);
                    var crusher = (Machine)w.At(origin + new Vector3Int(4, 0, 1));
                    foreach (var d in DirectionExtensions.All) crusher.Sides.SetMode(d, SideMode.Input);
                    crusher.Sides.SetMode(Direction.Down, SideMode.Output);
                    crusher.InsertManual(R("iron_ore"), 10);
                    vault.Sides.Set(Direction.East, SideMode.Output, R("iron_ore"));
                    for (int i = 1; i <= 3; i++) a.TryPlace(R("item_universal_cable"), vault.Position + new Vector3Int(i, 0, 0), out _, T(ResourceKind.Item));
                    a.TryPlace(R("item_fission_reactor"), origin + new Vector3Int(-1, 4, 0), out _);
                    a.TryPlace(R("item_electrolyser"), origin + new Vector3Int(2, 4, 0), out _);
                    for (int i = 0; i < 3; i++) a.TryPlace(R("item_universal_cable"), origin + new Vector3Int(3 + i, 4, 0), out _, T(ResourceKind.Gas));
                    a.TryPlace(R("item_universal_cable"), origin + new Vector3Int(5, 4, 1), out _, T(ResourceKind.Gas));
                    ctx.Runner.SetSpeedIndex(3);
                    yield return new WaitForSecondsRealtime(3f);
                    ctx.Selection = w.At(origin + new Vector3Int(4, 0, 0));
                    break;
                case "inspector":
                    ui.CloseAll();
                    ctx.Selection = w.Machines.OfType<ProcessorMachine>().FirstOrDefault(m => m.MachineDef.Id == "electrolyser") ?? (IGridEntity)vault;
                    break;
                case "sides":
                    vault.Sides.Set(Direction.North, SideMode.Output, R("iron_ore"));
                    vault.Sides.Set(Direction.Up, SideMode.Output, null);
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

        private static void Click(VisualElement e)
        {
            if (e == null) return;
            using var click = ClickEvent.GetPooled();
            click.target = e;
            e.SendEvent(click);
        }

        private TransmitterDef T(ResourceKind k) => database.Transmitters.First(t => t.Carries == k);

        private ResourceDef R(string id) => database.Resources.First(r => r.Id == id);
    }
}
