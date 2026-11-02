using System.Collections.Generic;
using Nucleaire.Logistics;
using Nucleaire.Simulation;
using UnityEngine;

namespace Nucleaire.Game
{
    /// <summary>Vue 3D de l'atelier : sol en grille, une vue par entité, surbrillance de la case visée et de la sélection.</summary>
    public sealed class WorldView : MonoBehaviour
    {
        private World world;
        private GameContext context;
        private readonly Dictionary<IGridEntity, GameObject> views = new();
        private Transform hover, selection;
        private Renderer hoverRenderer;

        public void Init(GameContext ctx, Material unlit)
        {
            context = ctx;
            world = ctx.World;
            BuildFloor(unlit);
            BuildNorthMarker();

            hover = MakeFlatQuad("Hover", unlit, new Color(0.54f, 0.66f, 0.88f, 0.35f));
            hoverRenderer = hover.GetComponent<Renderer>();
            hover.gameObject.SetActive(false);
            selection = MakeFrame("Selection");
            selection.gameObject.SetActive(false);

            foreach (var e in world.Entities) Spawn(e);
            world.EntityPlaced += OnPlaced;
            world.EntityRemoved += OnRemoved;
            foreach (var m in world.Machines) m.Sides.Changed += () => RefreshNeighbours(m.Position);
            ctx.SelectionChanged += UpdateSelection;
        }

        private void OnDestroy()
        {
            if (world == null) return;
            world.EntityPlaced -= OnPlaced;
            world.EntityRemoved -= OnRemoved;
        }

        // ---------------- Entités ----------------

        private void OnPlaced(IGridEntity e)
        {
            Spawn(e);
            if (e is Machine m) m.Sides.Changed += () => RefreshNeighbours(m.Position);
            RefreshNeighbours(e.Position);
        }

        private void OnRemoved(IGridEntity e)
        {
            if (views.TryGetValue(e, out var go)) Destroy(go);
            views.Remove(e);
            RefreshNeighbours(e.Position);
            if (context.Selection == e) context.Selection = null;
        }

        private void Spawn(IGridEntity e)
        {
            var go = new GameObject(e.Def.DisplayName);
            go.transform.SetParent(transform, false);
            switch (e)
            {
                case Machine m: go.AddComponent<MachineView>().Init(m); break;
                case TransmitterSegment s: go.AddComponent<TransmitterView>().Init(s, world); break;
            }
            views[e] = go;
        }

        private void RefreshNeighbours(Vector2Int p)
        {
            foreach (var d in DirectionExtensions.All)
                if (world.At(p + d.Offset()) is TransmitterSegment s && views.TryGetValue(s, out var go))
                    go.GetComponent<TransmitterView>().Refresh();
            if (world.At(p) is TransmitterSegment self && views.TryGetValue(self, out var selfGo))
                selfGo.GetComponent<TransmitterView>().Refresh();
        }

        public Vector3? ViewPosition(IGridEntity e) => views.TryGetValue(e, out var go) ? go.transform.position : null;

        // ---------------- Surbrillances ----------------

        public void SetHover(Vector2Int? cell, bool valid, bool building)
        {
            if (cell == null || !world.InBounds(cell.Value)) { hover.gameObject.SetActive(false); return; }
            hover.gameObject.SetActive(true);
            hover.position = GridMath.CellCenter(cell.Value, 0.012f);
            var c = !building ? new Color(232 / 255f, 238 / 255f, 250 / 255f, 0.10f)
                : valid ? Palette.WithAlpha(Palette.Accent, 0.40f) : Palette.WithAlpha(Palette.Danger, 0.40f);
            Shapes.SetColor(hoverRenderer, c);
        }

        private void UpdateSelection(IGridEntity e)
        {
            selection.gameObject.SetActive(e != null);
            if (e != null) selection.position = GridMath.CellCenter(e.Position, 0.015f);
        }

        // ---------------- Construction du décor ----------------

        private void BuildFloor(Material unlit)
        {
            var size = world.Settings.gridSize;
            const int cellPx = 32;
            var tex = new Texture2D(size.x * cellPx, size.y * cellPx, TextureFormat.RGBA32, true)
            {
                name = "FloorGrid", filterMode = FilterMode.Trilinear, anisoLevel = 4, wrapMode = TextureWrapMode.Clamp,
            };
            var px = new Color32[tex.width * tex.height];
            Color32 floor = Palette.Floor;
            Color32 line = Color.Lerp(Palette.Floor, Color.white, Palette.FloorLine.a);
            Color32 edge = Color.Lerp(Palette.Floor, Color.white, Palette.FloorEdge.a);
            for (int y = 0; y < tex.height; y++)
            for (int x = 0; x < tex.width; x++)
            {
                bool border = x < 2 || y < 2 || x >= tex.width - 2 || y >= tex.height - 2;
                bool grid = x % cellPx == 0 || y % cellPx == 0;
                px[y * tex.width + x] = border ? edge : grid ? line : floor;
            }
            tex.SetPixels32(px);
            tex.Apply(true);

            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = "Floor";
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(size.x / 2f, 0f, size.y / 2f);
            go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            go.transform.localScale = new Vector3(size.x, size.y, 1f);
            var mat = new Material(unlit);
            mat.SetTexture("_BaseMap", tex);
            mat.mainTexture = tex;
            mat.SetColor("_BaseColor", Color.white);
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;

            // Socle sombre sous le sol pour donner de l'épaisseur à la plaque.
            var slab = Shapes.Box(transform, new Vector3(size.x / 2f, -0.26f, size.y / 2f), new Vector3(size.x + 0.2f, 0.5f, size.y + 0.2f), Palette.MachineBodyDark, "Slab");
            slab.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        private void BuildNorthMarker()
        {
            var size = world.Settings.gridSize;
            var root = new GameObject("North").transform;
            root.SetParent(transform, false);
            root.localPosition = new Vector3(size.x / 2f, 0.02f, size.y + 0.7f);
            var a = Shapes.Box(root, new Vector3(-0.18f, 0, 0), new Vector3(0.5f, 0.03f, 0.08f), Palette.Lavender, "Chevron L");
            a.localRotation = Quaternion.Euler(0, -35f, 0);
            var b = Shapes.Box(root, new Vector3(0.18f, 0, 0), new Vector3(0.5f, 0.03f, 0.08f), Palette.Lavender, "Chevron R");
            b.localRotation = Quaternion.Euler(0, 35f, 0);
        }

        private Transform MakeFlatQuad(string name, Material unlit, Color c)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = name;
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(transform, false);
            go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            go.transform.localScale = new Vector3(0.96f, 0.96f, 1f);
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = TransparentMaterial;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Shapes.SetColor(r, c);
            return go.transform;
        }

        private Transform MakeFrame(string name)
        {
            var root = new GameObject(name).transform;
            root.SetParent(transform, false);
            const float t = 0.05f, l = 1.02f;
            foreach (var (pos, scale) in new[]
                     {
                         (new Vector3(0, 0, l / 2f), new Vector3(l, 0.02f, t)), (new Vector3(0, 0, -l / 2f), new Vector3(l, 0.02f, t)),
                         (new Vector3(l / 2f, 0, 0), new Vector3(t, 0.02f, l)), (new Vector3(-l / 2f, 0, 0), new Vector3(t, 0.02f, l)),
                     })
                Shapes.Box(root, pos, scale, Palette.Accent, "Edge").GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return root;
        }

        /// <summary>Matière transparente (case survolée).</summary>
        public static Material TransparentMaterial { get; set; }
    }
}
