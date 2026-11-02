using System.Collections.Generic;
using Nucleaire.Logistics;
using Nucleaire.Simulation;
using UnityEngine;
using UnityEngine.Rendering;

namespace Nucleaire.Game
{
    /// <summary>Vue 3D de l'atelier : sol en grille, une vue par entité, fantôme de pose, sélection,
    /// et vue en coupe (les colonnes entre la caméra et le point visé disparaissent quand on zoome).</summary>
    public sealed class WorldView : MonoBehaviour
    {
        /// <summary>Zoom (taille orthographique) sous lequel la vue en coupe s'active.</summary>
        public const float CutawayZoom = 6.5f;
        private const float CutawayMargin = 0.75f;

        private sealed class View
        {
            public GameObject Go;
            public Renderer[] Renderers;
            public Collider[] Colliders;
            public bool Hidden;
        }

        private World world;
        private GameContext context;
        private readonly Dictionary<IGridEntity, View> views = new();
        private Transform ghost, selection, hoverFrame;
        private Renderer ghostRenderer;

        public bool CutawayActive { get; private set; }

        public void Init(GameContext ctx)
        {
            context = ctx;
            world = ctx.World;
            BuildFloor();

            ghost = GameObject.CreatePrimitive(PrimitiveType.Cube).transform;
            ghost.name = "Ghost";
            Destroy(ghost.GetComponent<Collider>());
            ghost.SetParent(transform, false);
            ghost.localScale = new Vector3(0.9f, 0.9f, 0.9f);
            ghostRenderer = ghost.GetComponent<Renderer>();
            ghostRenderer.sharedMaterial = TransparentMaterial;
            ghostRenderer.shadowCastingMode = ShadowCastingMode.Off;
            ghost.gameObject.SetActive(false);

            selection = MakeFrame("Selection", Palette.Accent);
            hoverFrame = MakeFrame("Hover", Palette.WithAlpha(Palette.Text, 1f));
            selection.gameObject.SetActive(false);
            hoverFrame.gameObject.SetActive(false);

            foreach (var e in world.Entities) Spawn(e);
            foreach (var m in world.Machines) Watch(m);
            world.EntityPlaced += OnPlaced;
            world.EntityRemoved += OnRemoved;
            world.EntityMoved += OnMoved;
            ctx.SelectionChanged += _ => UpdateSelection();
        }

        private void OnDestroy()
        {
            if (world == null) return;
            world.EntityPlaced -= OnPlaced;
            world.EntityRemoved -= OnRemoved;
            world.EntityMoved -= OnMoved;
        }

        // ---------------- Entités ----------------

        private void Watch(Machine m) => m.Sides.Changed += () => RefreshNeighbours(m.Position);

        private void OnPlaced(IGridEntity e)
        {
            Spawn(e);
            if (e is Machine m) Watch(m);
            RefreshNeighbours(e.Position);
        }

        private void OnRemoved(IGridEntity e)
        {
            if (views.TryGetValue(e, out var v)) Destroy(v.Go);
            views.Remove(e);
            RefreshNeighbours(e.Position);
            if (context.Selection == e) context.Selection = null;
        }

        private void OnMoved(IGridEntity e, Vector3Int from)
        {
            if (views.TryGetValue(e, out var v)) v.Go.GetComponent<MachineView>()?.SyncPosition();
            RefreshNeighbours(from);
            RefreshNeighbours(e.Position);
            UpdateSelection();
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
            views[e] = new View { Go = go, Renderers = go.GetComponentsInChildren<Renderer>(), Colliders = go.GetComponentsInChildren<Collider>() };
        }

        private void RefreshNeighbours(Vector3Int p)
        {
            foreach (var d in DirectionExtensions.All)
                if (world.At(p + d.Offset()) is TransmitterSegment s && views.TryGetValue(s, out var v))
                    v.Go.GetComponent<TransmitterView>().Refresh();
            if (world.At(p) is TransmitterSegment self && views.TryGetValue(self, out var sv))
                sv.Go.GetComponent<TransmitterView>().Refresh();
        }

        /// <summary>Filtre d'affichage choisi par le joueur (null = tout afficher).</summary>
        public System.Func<IGridEntity, bool> Filter { get; set; }

        public int VisibleCount { get; private set; }
        public int TotalCount => views.Count;

        public bool IsVisible(IGridEntity e) => views.TryGetValue(e, out var v) && !v.Hidden;

        // ---------------- Vue en coupe ----------------

        /// <summary>Masque les colonnes entières (toute la hauteur) situées devant le point visé, quand on zoome.</summary>
        public void UpdateCutaway(float zoom, Vector3 focus, Vector3 horizontalForward)
        {
            CutawayActive = zoom < CutawayZoom;
            var f = new Vector3(horizontalForward.x, 0f, horizontalForward.z).normalized;
            int visible = 0;
            foreach (var (e, v) in views)
            {
                var c = GridMath.CellCenter(e.Position);
                bool hide = CutawayActive && Vector3.Dot(new Vector3(c.x - focus.x, 0f, c.z - focus.z), f) < -CutawayMargin;
                if (Filter != null && !Filter(e)) hide = true;
                if (!hide) visible++;
                if (hide == v.Hidden) continue;
                v.Hidden = hide;
                v.Go.SetActive(!hide);
            }
            VisibleCount = visible;
            if (context.Selection != null && !IsVisible(context.Selection)) selection.gameObject.SetActive(false);
            else UpdateSelection();
        }

        // ---------------- Surbrillances ----------------

        /// <summary>Fantôme de la case où l'objet sera posé (bleu : possible, rouge : impossible).</summary>
        public void SetGhost(Vector3Int? cell, bool valid)
        {
            ghost.gameObject.SetActive(cell != null);
            if (cell == null) return;
            ghost.position = GridMath.CellCenter(cell.Value, 0.46f);
            Shapes.SetColor(ghostRenderer, Palette.WithAlpha(valid ? Palette.Accent : Palette.Danger, 0.32f));
        }

        public void SetHoverEntity(IGridEntity e)
        {
            hoverFrame.gameObject.SetActive(e != null && e != context.Selection);
            if (e != null) hoverFrame.position = GridMath.CellCenter(e.Position, 0.015f);
        }

        private void UpdateSelection()
        {
            var e = context.Selection;
            selection.gameObject.SetActive(e != null && IsVisible(e));
            if (e != null) selection.position = GridMath.CellCenter(e.Position, 0.02f);
        }

        // ---------------- Décor ----------------

        private void BuildFloor()
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
            var mat = new Material(UnlitMaterial);
            mat.SetTexture("_BaseMap", tex);
            mat.mainTexture = tex;
            mat.SetColor("_BaseColor", Color.white);
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            var col = go.AddComponent<BoxCollider>();
            col.size = new Vector3(1f, 1f, 0.01f);
            go.AddComponent<FloorMarker>();

            var slab = Shapes.Box(transform, new Vector3(size.x / 2f, -0.26f, size.y / 2f), new Vector3(size.x + 0.2f, 0.5f, size.y + 0.2f), Palette.MachineBodyDark, "Slab");
            slab.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
        }

        private Transform MakeFrame(string name, Color c)
        {
            var root = new GameObject(name).transform;
            root.SetParent(transform, false);
            const float t = 0.05f, l = 1.02f;
            foreach (var (pos, scale) in new[]
                     {
                         (new Vector3(0, 0, l / 2f), new Vector3(l, 0.02f, t)), (new Vector3(0, 0, -l / 2f), new Vector3(l, 0.02f, t)),
                         (new Vector3(l / 2f, 0, 0), new Vector3(t, 0.02f, l)), (new Vector3(-l / 2f, 0, 0), new Vector3(t, 0.02f, l)),
                     })
                Shapes.Box(root, pos, scale, c, "Edge").GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            return root;
        }

        public static Material TransparentMaterial { get; set; }
        public static Material UnlitMaterial { get; set; }
    }

    /// <summary>Marque le collider du sol (clic sur une case vide).</summary>
    public sealed class FloorMarker : MonoBehaviour { }
}
