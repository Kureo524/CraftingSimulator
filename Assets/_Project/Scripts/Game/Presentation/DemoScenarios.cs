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
    /// <summary>Scénarios de captures automatiques (vérification visuelle, lancés par <see cref="AutoScreenshot"/>
    /// avec « -autoshot »). Hors du jeu normal : mettent l'atelier dans un état type puis rendent la main.</summary>
    public sealed class DemoScenarios
    {
        private readonly GameContext ctx;
        private readonly GameUI ui;
        private readonly IsoCameraRig rig;
        private readonly WorldView worldView;
        private readonly BuildController build;
        private readonly UIDocument uiDocument;
        private readonly RecipeDatabase database;

        public DemoScenarios(GameContext ctx, GameUI ui, IsoCameraRig rig, WorldView worldView, BuildController build,
            UIDocument uiDocument, RecipeDatabase database)
        {
            this.ctx = ctx;
            this.ui = ui;
            this.rig = rig;
            this.worldView = worldView;
            this.build = build;
            this.uiDocument = uiDocument;
            this.database = database;
        }

        public IEnumerator Run(string name)
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
                    build.enabled = false;
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
                case "multiblock-hover":
                case "side-hover":
                case "plan-wall":
                {
                    var inner = Run("multiblock");
                    while (inner.MoveNext()) yield return inner.Current;
                    var o = w.FirstMachine<VaultMachine>().Position + new Vector3Int(-6, 2, 0);
                    Vector3 aim;
                    if (name == "multiblock-hover") aim = GridMath.CellCenter(o + new Vector3Int(2, 2, 2), 1f);
                    else
                    {
                        ctx.Session.Inventory.Add(R("item_structural_frame"), 64);
                        ctx.BuildItem = R("item_structural_frame");
                        // Face ouest du bloc (o.x, o.y + 3, étage 2) : la caméra la voit.
                        aim = GridMath.CellCenter(o + new Vector3Int(0, 3, 2), 0.5f) - new Vector3(0.5f, 0f, 0f);
                        if (name == "plan-wall")
                            for (int dy = 0; dy < 4; dy++)
                            for (int dz = 0; dz < 3; dz++)
                                ctx.Plan.Add(o + new Vector3Int(-1, 1 + dy, dz), Direction.West);
                    }
                    BuildController.DebugPointer = rig.Camera.WorldToScreenPoint(aim);
                    for (int i = 0; i < 8; i++) yield return null;
                    yield return new WaitForSecondsRealtime(0.4f);
                    break;
                }
                case "gallery":
                {
                    // Toutes les machines côte à côte (vérification des modèles).
                    ctx.Session.Tutorial.Skip();
                    ui.CloseAll();
                    yield return null;
                    w.Expand(); w.Expand();
                    var area = w.Area;
                    var defs = database.Machines.Where(m => m != null).ToList();
                    for (int i = 0; i < defs.Count; i++)
                    {
                        var c = new Vector3Int(area.xMin + 2 + (i % 6) * 2, area.yMin + 2 + (i / 6) * 2, 0);
                        if (w.At(c) != null) w.Remove(c);
                        w.Place(defs[i], c, Direction.South);
                    }
                    rig.SetZoomForTests(7f);
                    rig.Focus(GridMath.CellCenter(new Vector3Int(area.xMin + 7, area.yMin + 4, 0)));
                    yield return new WaitForSecondsRealtime(1f);
                    break;
                }
                case "gallery-back":
                    rig.Rotate(1); rig.Rotate(1);
                    yield return new WaitForSecondsRealtime(1.2f);
                    break;
                case "multiblock-ui":
                    ctx.Selection = w.Entities.OfType<StructureBlock>().First(b => b.Role == StructureRole.Frame);
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
                case "tooltip-edge":
                {
                    // Survol de l'emplacement le plus à droite du catalogue : l'infobulle doit rester dans l'écran.
                    ctx.Creative = true;
                    ui.CloseAll();
                    ui.ToggleInventory();
                    yield return new WaitForSecondsRealtime(0.5f);
                    var slots = uiDocument.rootVisualElement.Q("catalog").Query(className: "rslot").ToList();
                    var right = slots.OrderByDescending(e => e.worldBound.xMax).ThenBy(e => e.worldBound.y).First();
                    var c = right.worldBound.center;
                    var sys = new Event { type = EventType.MouseMove, mousePosition = c };
                    using (var enter = PointerEnterEvent.GetPooled(sys)) { enter.target = right; right.SendEvent(enter); }
                    using (var move = PointerMoveEvent.GetPooled(sys)) { move.target = right; right.SendEvent(move); }
                    yield return new WaitForSecondsRealtime(0.8f);
                    break;
                }
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
