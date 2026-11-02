using System.Collections.Generic;
using Nucleaire.Data;
using Nucleaire.Logistics;
using Nucleaire.Simulation;
using UnityEngine;
using UnityEngine.Rendering;

namespace Nucleaire.Game
{
    /// <summary>Aperçus dans le monde : la vraie forme de ce qui va être posé (translucide, orientée),
    /// les cases d'une pose multiple, et les éléments marqués en mode suppression (rouge).</summary>
    public sealed class BuildPreview : MonoBehaviour
    {
        private World world;
        private GameContext ctx;

        private GameObject hover;
        private Transform[] hoverArms;
        private (PlaceableDef def, Direction facing) hoverKey;

        private readonly Dictionary<Vector3Int, GameObject> planGhosts = new();
        private (PlaceableDef def, Direction facing) planKey;
        private bool planDirty = true;

        private readonly Dictionary<object, GameObject> deleteMarks = new();
        private Transform deleteHover;

        public void Init(GameContext context)
        {
            ctx = context;
            world = ctx.World;
            ctx.Plan.Changed += () => planDirty = true;
            ctx.Deletion.Changed += RebuildDeleteMarks;
            world.EntityPlaced += _ => planDirty = true;
            world.EntityRemoved += _ => planDirty = true;
            deleteHover = DangerBox("Delete hover", 0.22f);
            deleteHover.gameObject.SetActive(false);
            faceHighlight = DangerBox("Face highlight", 0.4f);
            Shapes.SetColor(faceHighlight.GetComponent<Renderer>(), Palette.WithAlpha(Palette.Accent, 0.45f));
            faceHighlight.gameObject.SetActive(false);
        }

        // ---------------- Faces (trieur, mode modification) ----------------

        private Transform faceHighlight;
        private GameObject sorterGhost;
        private (Machine m, Direction d) sorterGhostKey;

        /// <summary>Place une plaque fine sur la face d'une case (ou le bout d'un côté de câble).</summary>
        private static void OnFace(Transform t, Vector3Int cell, Direction d, float size, float thickness, float inset = 0.5f)
        {
            var center = GridMath.CellCenter(cell, 0.5f);
            if (d.IsVertical())
            {
                t.localPosition = center + Vector3.up * (d == Direction.Up ? inset : -inset + 0.02f);
                t.localRotation = Quaternion.identity;
                t.localScale = new Vector3(size, thickness, size);
            }
            else
            {
                var dir = GridMath.DirectionVector(d);
                t.localPosition = center + dir * inset;
                t.localRotation = Quaternion.LookRotation(dir);
                t.localScale = new Vector3(size, size, thickness);
            }
        }

        /// <summary>Surligne une face de machine (mode modification).</summary>
        public void SetFaceHighlight(Vector3Int? cell, Direction d, bool cableSide = false)
        {
            faceHighlight.gameObject.SetActive(cell != null);
            if (cell == null) return;
            if (cableSide)
            {
                OnFace(faceHighlight, cell.Value, d, 0.3f, 0.3f, 0.38f);
                if (!d.IsVertical()) faceHighlight.localPosition = GridMath.CellCenter(cell.Value, 0.15f) + GridMath.DirectionVector(d) * 0.38f;
            }
            else OnFace(faceHighlight, cell.Value, d, 0.98f, 0.04f, 0.51f);
        }

        /// <summary>Aperçu d'un trieur sur la face visée.</summary>
        public void SetSorterGhost(Machine m, Direction? face, bool valid)
        {
            if (m == null || face == null)
            {
                if (sorterGhost != null) sorterGhost.SetActive(false);
                return;
            }
            if (sorterGhost == null || sorterGhostKey != (m, face.Value))
            {
                if (sorterGhost != null) Destroy(sorterGhost);
                sorterGhostKey = (m, face.Value);
                sorterGhost = new GameObject("Sorter ghost");
                sorterGhost.transform.SetParent(transform, false);
                sorterGhost.transform.localPosition = GridMath.CellCenter(m.Position);
                MachineView.BuildSorterPlate(sorterGhost.transform, face.Value, 0.6f);
                foreach (var r in sorterGhost.GetComponentsInChildren<Renderer>())
                {
                    var c = Shapes.GetColor(r);
                    r.sharedMaterial = WorldView.TransparentMaterial;
                    r.shadowCastingMode = ShadowCastingMode.Off;
                    r.gameObject.AddComponent<PreviewColor>().Original = c;
                }
            }
            sorterGhost.SetActive(true);
            Tint(sorterGhost, valid);
        }

        // ---------------- Objet sous le curseur ----------------

        /// <summary>Aperçu de pose sur une case (null pour le cacher).</summary>
        public void SetHover(PlaceableDef def, Direction facing, Vector3Int? cell, bool valid)
        {
            if (def == null || cell == null)
            {
                if (hover != null) hover.SetActive(false);
                return;
            }
            if (hover == null || hoverKey != (def, facing))
            {
                if (hover != null) Destroy(hover);
                hoverKey = (def, facing);
                hover = Build(def, facing, out hoverArms);
            }
            hover.SetActive(true);
            hover.transform.localPosition = GridMath.CellCenter(cell.Value);
            if (def is TransmitterDef td) Connect(hoverArms, td, cell.Value, null);
            Tint(hover, valid);
        }

        // ---------------- Pose multiple ----------------

        private void LateUpdate()
        {
            var def = ctx.Plan.Active ? ctx.BuildPlaceable : null;
            if (!planDirty && planKey == (def, ctx.BuildFacing)) return;
            planDirty = false;
            planKey = (def, ctx.BuildFacing);
            foreach (var g in planGhosts.Values) Destroy(g);
            planGhosts.Clear();
            if (def == null) return;

            var cells = new HashSet<Vector3Int>(ctx.Plan.Cells);
            foreach (var c in ctx.Plan.Cells)
            {
                var g = Build(def, ctx.BuildFacing, out var arms);
                g.transform.localPosition = GridMath.CellCenter(c);
                if (def is TransmitterDef td) Connect(arms, td, c, cells);
                Tint(g, ctx.Plan.IsValid(c));
                planGhosts[c] = g;
            }
        }

        // ---------------- Suppression ----------------

        public void SetDeleteHover(object e)
        {
            deleteHover.gameObject.SetActive(e != null && ctx.Deletion.Active);
            if (e != null) Place(deleteHover, e);
        }

        private static void Place(Transform t, object e)
        {
            switch (e)
            {
                case FaceSorter s:
                    OnFace(t, s.Machine.Position, s.Face, 0.66f, 0.1f, 0.52f);
                    break;
                case IGridEntity g:
                    t.localPosition = GridMath.CellCenter(g.Position, 0.5f);
                    t.localRotation = Quaternion.identity;
                    t.localScale = new Vector3(1.02f, 1.02f, 1.02f);
                    break;
            }
        }

        private void RebuildDeleteMarks()
        {
            foreach (var g in deleteMarks.Values) Destroy(g);
            deleteMarks.Clear();
            foreach (var e in ctx.Deletion.Marks)
            {
                var box = DangerBox("Delete mark", 0.45f);
                Place(box, e);
                deleteMarks[e] = box.gameObject;
            }
            if (!ctx.Deletion.Active) deleteHover.gameObject.SetActive(false);
        }

        private Transform DangerBox(string name, float alpha)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(transform, false);
            go.transform.localScale = new Vector3(1.02f, 1.02f, 1.02f);
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = WorldView.TransparentMaterial;
            r.shadowCastingMode = ShadowCastingMode.Off;
            Shapes.SetColor(r, Palette.WithAlpha(Palette.Danger, alpha));
            return go.transform;
        }

        // ---------------- Construction des aperçus ----------------

        private GameObject Build(PlaceableDef def, Direction facing, out Transform[] arms)
        {
            var go = new GameObject("Preview " + def.DisplayName);
            go.transform.SetParent(transform, false);
            arms = null;
            switch (def)
            {
                case MachineDef md:
                    MachineView.BuildModel(go.transform, md, facing);
                    break;
                case TransmitterDef td:
                    arms = new Transform[6];
                    TransmitterView.BuildGeometry(go.transform, td, arms);
                    break;
            }
            foreach (var r in go.GetComponentsInChildren<Renderer>())
            {
                var c = Shapes.GetColor(r);
                r.sharedMaterial = WorldView.TransparentMaterial;
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.gameObject.AddComponent<PreviewColor>().Original = c;
            }
            return go;
        }

        /// <summary>Bras de l'aperçu de câble : vers les câbles du même type, les machines, et les autres cases prévues.</summary>
        private void Connect(Transform[] arms, TransmitterDef def, Vector3Int c, HashSet<Vector3Int> planned)
        {
            if (arms == null) return;
            for (int i = 0; i < 6; i++)
            {
                var d = (Direction)i;
                var n = c + d.Offset();
                bool on = planned != null && planned.Contains(n) || world.At(n) switch
                {
                    TransmitterSegment s => s.TransmitterDef == def,
                    Machine m => m.Sides[d.Opposite()].Mode != SideMode.None,
                    _ => false,
                };
                arms[i].gameObject.SetActive(on);
            }
        }

        private static void Tint(GameObject go, bool valid)
        {
            foreach (var p in go.GetComponentsInChildren<PreviewColor>(true))
            {
                var c = valid ? Color.Lerp(p.Original, Palette.Accent, 0.3f) : Color.Lerp(p.Original, Palette.Danger, 0.75f);
                Shapes.SetColor(p.GetComponent<Renderer>(), Palette.WithAlpha(c, 0.55f));
            }
        }

        private sealed class PreviewColor : MonoBehaviour
        {
            public Color Original;
        }
    }
}
