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
        private Transform selection, hoverFrame;

        public bool CutawayActive { get; private set; }

        public void Init(GameContext ctx)
        {
            context = ctx;
            world = ctx.World;
            BuildFloor();
            world.AreaChanged += BuildFloor;

            selection = MakeFrame("Selection", Palette.Accent);
            hoverFrame = MakeFrame("Hover", Palette.WithAlpha(Palette.Text, 1f));
            selection.gameObject.SetActive(false);
            hoverFrame.gameObject.SetActive(false);

            foreach (var e in world.Entities) Spawn(e);
            foreach (var m in world.Machines) Watch(m);
            world.EntityPlaced += OnPlaced;
            world.EntityRemoved += OnRemoved;
            world.EntityMoved += OnMoved;
            world.Logistics.SideChanged += seg => RefreshNeighbours(seg.Position);
            ctx.SelectionChanged += _ => UpdateSelection();
        }

        private void OnDestroy()
        {
            if (world == null) return;
            world.EntityPlaced -= OnPlaced;
            world.EntityRemoved -= OnRemoved;
            world.EntityMoved -= OnMoved;
            world.AreaChanged -= BuildFloor;
        }

        // ---------------- Entités ----------------

        private void Watch(Machine m)
        {
            m.Sides.Changed += () => RefreshNeighbours(m.Position);
            m.AttachmentsChanged += () => RefreshNeighbours(m.Position);
        }

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
                case StructureBlock b: go.AddComponent<StructureView>().Init(b); break;
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

        public void SetHoverEntity(IGridEntity e)
        {
            hoverFrame.gameObject.SetActive(e != null && e != context.Selection);
            if (e != null) Fit(hoverFrame, e, 0.015f);
        }

        private void UpdateSelection()
        {
            var e = context.Selection;
            selection.gameObject.SetActive(e != null && IsVisible(e));
            if (e != null) Fit(selection, e, 0.02f);
        }

        /// <summary>Cases couvertes par la surbrillance : le multi-bloc complet dont l'entité fait partie
        /// (contrôleur, port, paroi ou intérieur), sinon sa seule case.</summary>
        private (Vector3Int min, Vector3Int max) Footprint(IGridEntity e)
        {
            var p = e.Position;
            foreach (var m in world.Machines)
            {
                if (m is not IMultiblockController c) continue;
                var s = c.Structure;
                s.Refresh();
                if (!s.Formed) continue;
                if (p.x >= s.Min.x && p.x <= s.Max.x && p.y >= s.Min.y && p.y <= s.Max.y && p.z >= s.Min.z && p.z <= s.Max.z)
                    return (s.Min, s.Max);
            }
            return (p, p);
        }

        /// <summary>Cadre (et voile) au pied de l'entité, ou de tout son multi-bloc s'il est complet.</summary>
        private void Fit(Transform frame, IGridEntity e, float lift)
        {
            var (lo, hi) = Footprint(e);
            float w = hi.x - lo.x + 1, d = hi.y - lo.y + 1;
            frame.position = new Vector3(lo.x + w / 2f, lo.z * GridMath.LevelHeight + lift, lo.y + d / 2f);
            // Un multi-bloc couvre toute sa base : le cadre déborde autour pour rester visible.
            bool multi = lo != hi;
            float t = multi ? 0.07f : 0.05f, pad = multi ? 0.5f : 0.02f;
            float lw = w + pad, ld = d + pad;
            Edge(frame, 0, new Vector3(0, 0, ld / 2f), new Vector3(lw, 0.02f, t));
            Edge(frame, 1, new Vector3(0, 0, -ld / 2f), new Vector3(lw, 0.02f, t));
            Edge(frame, 2, new Vector3(lw / 2f, 0, 0), new Vector3(t, 0.02f, ld));
            Edge(frame, 3, new Vector3(-lw / 2f, 0, 0), new Vector3(t, 0.02f, ld));
            // Voile : seulement pour un multi-bloc, pour que toute sa base s'allume.
            var fill = frame.GetChild(4);
            fill.gameObject.SetActive(multi);
            fill.localScale = new Vector3(lw, 1f, ld);
        }

        private static void Edge(Transform frame, int i, Vector3 pos, Vector3 scale)
        {
            var t = frame.GetChild(i);
            t.localPosition = pos;
            t.localScale = scale;
        }

        // ---------------- Décor ----------------

        private GameObject floor, slab;
        private Texture2D floorTex;

        /// <summary>Sol : une dalle flottant dans le vide, de la taille de la zone constructible.
        /// Elle est reconstruite (plus grande) à chaque agrandissement de l'atelier.</summary>
        private void BuildFloor()
        {
            var area = world.Area;
            const int cellPx = 32;
            if (floor == null)
            {
                floor = GameObject.CreatePrimitive(PrimitiveType.Quad);
                floor.name = "Floor";
                Destroy(floor.GetComponent<Collider>());
                floor.transform.SetParent(transform, false);
                floor.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                var mat = new Material(UnlitMaterial);
                mat.SetColor("_BaseColor", Color.white);
                floor.GetComponent<MeshRenderer>().sharedMaterial = mat;
                var col = floor.AddComponent<BoxCollider>();
                col.size = new Vector3(1f, 1f, 0.01f);
                floor.AddComponent<FloorMarker>();
                slab = Shapes.Box(transform, Vector3.zero, Vector3.one, Palette.MachineBodyDark, "Slab").gameObject;
                slab.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            }

            if (floorTex != null) Destroy(floorTex);
            floorTex = new Texture2D(area.width * cellPx, area.height * cellPx, TextureFormat.RGBA32, true)
            {
                name = "FloorGrid", filterMode = FilterMode.Trilinear, anisoLevel = 4, wrapMode = TextureWrapMode.Clamp,
            };
            var px = new Color32[floorTex.width * floorTex.height];
            Color32 ground = Palette.Floor;
            Color32 line = Color.Lerp(Palette.Floor, Color.white, Palette.FloorLine.a);
            Color32 edge = Color.Lerp(Palette.Floor, Color.white, Palette.FloorEdge.a);
            for (int y = 0; y < floorTex.height; y++)
            for (int x = 0; x < floorTex.width; x++)
            {
                bool border = x < 2 || y < 2 || x >= floorTex.width - 2 || y >= floorTex.height - 2;
                bool grid = x % cellPx == 0 || y % cellPx == 0;
                px[y * floorTex.width + x] = border ? edge : grid ? line : ground;
            }
            floorTex.SetPixels32(px);
            floorTex.Apply(true);
            var m = floor.GetComponent<MeshRenderer>().sharedMaterial;
            m.SetTexture("_BaseMap", floorTex);
            m.mainTexture = floorTex;

            floor.transform.localPosition = new Vector3(area.center.x, 0f, area.center.y);
            floor.transform.localScale = new Vector3(area.width, area.height, 1f);
            slab.transform.localPosition = new Vector3(area.center.x, -0.26f, area.center.y);
            slab.transform.localScale = new Vector3(area.width + 0.2f, 0.5f, area.height + 0.2f);
        }

        private Transform MakeFrame(string name, Color c)
        {
            var root = new GameObject(name).transform;
            root.SetParent(transform, false);
            for (int i = 0; i < 4; i++)
                Shapes.Box(root, Vector3.zero, Vector3.one, c, "Edge").GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            var fill = GameObject.CreatePrimitive(PrimitiveType.Quad);
            fill.name = "Fill";
            Destroy(fill.GetComponent<Collider>());
            fill.transform.SetParent(root, false);
            var holder = new GameObject("FillPivot").transform;
            holder.SetParent(root, false);
            fill.transform.SetParent(holder, false);
            fill.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var r = fill.GetComponent<Renderer>();
            r.sharedMaterial = TransparentMaterial;
            r.shadowCastingMode = ShadowCastingMode.Off;
            Shapes.SetColor(r, Palette.WithAlpha(c, 0.22f));
            holder.gameObject.SetActive(false);
            return root;
        }

        public static Material TransparentMaterial { get; set; }
        public static Material UnlitMaterial { get; set; }
    }

    /// <summary>Marque le collider du sol (clic sur une case vide).</summary>
    public sealed class FloorMarker : MonoBehaviour { }
}
