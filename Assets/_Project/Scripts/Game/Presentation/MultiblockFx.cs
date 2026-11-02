using System.Collections.Generic;
using System.Linq;
using Nucleaire.Simulation;
using UnityEngine;
using UnityEngine.Rendering;

namespace Nucleaire.Game
{
    /// <summary>Effet visuel des multi-blocs : quand une structure est complète, de petites particules montent
    /// le long de sa paroi (plus vite et plus nombreuses quand elle travaille) ; à la formation, une gerbe part
    /// de toute la boîte. La couleur dépend du type (fission, fusion, déphaseur, induction).</summary>
    public sealed class MultiblockFx : MonoBehaviour
    {
        private sealed class Mote
        {
            public Transform T;
            public Renderer R;
            public Vector3 Velocity;
            public float Age, Life, Size;
            public Color Color;
        }

        private sealed class Fx
        {
            public bool WasFormed;
            public float Spawn;
            public readonly List<Mote> Motes = new();
        }

        private const int MaxMotesPerStructure = 70;
        private World world;
        private readonly Dictionary<Machine, Fx> fx = new();
        private readonly Stack<Mote> pool = new();

        public void Init(World w) => world = w;

        private void Update()
        {
            if (world == null) return;
            float dt = Time.deltaTime;

            foreach (var m in world.Machines)
            {
                if (m is not IMultiblockController c) continue;
                var s = c.Structure;
                s.Refresh();
                if (!fx.TryGetValue(m, out var f)) fx[m] = f = new Fx { WasFormed = s.Formed };

                var (lo, hi) = Bounds(s);
                var color = ColorOf(s.Kind);
                if (s.Formed && !f.WasFormed) Burst(f, lo, hi, color);
                f.WasFormed = s.Formed;

                if (s.Formed)
                {
                    bool working = m.Status == MachineStatus.Working;
                    var size = hi - lo;
                    float area = size.x * size.z + 2f * (size.x + size.z) * size.y;
                    float rate = Mathf.Clamp(area * 0.4f, 6f, 30f) * (working ? 2f : 1f);
                    f.Spawn += rate * dt;
                    while (f.Spawn >= 1f && f.Motes.Count < MaxMotesPerStructure)
                    {
                        f.Spawn -= 1f;
                        Emit(f, Surface(lo, hi), new Vector3(Random.Range(-0.08f, 0.08f), Random.Range(0.35f, 0.7f) * (working ? 1.6f : 1f), Random.Range(-0.08f, 0.08f)),
                            Random.Range(1.4f, 2.4f), color, working ? 0.85f : 0.55f);
                    }
                    if (f.Spawn > 1f) f.Spawn = 1f;
                }
                Step(f, dt);
            }

            // Contrôleurs retirés : on libère leurs particules.
            foreach (var gone in fx.Keys.Where(k => !world.Machines.Contains(k)).ToList())
            {
                foreach (var mote in fx[gone].Motes) Release(mote);
                fx.Remove(gone);
            }
        }

        private void Burst(Fx f, Vector3 lo, Vector3 hi, Color color)
        {
            var center = (lo + hi) / 2f;
            for (int i = 0; i < 46 && f.Motes.Count < MaxMotesPerStructure; i++)
            {
                var p = Surface(lo, hi, wallsOnly: false);
                var outward = (p - center).normalized * Random.Range(0.8f, 1.6f) + Vector3.up * 0.4f;
                Emit(f, p, outward, Random.Range(0.8f, 1.3f), Color.Lerp(color, Color.white, 0.3f), 1f);
            }
        }

        private void Emit(Fx f, Vector3 position, Vector3 velocity, float life, Color color, float strength)
        {
            var mote = pool.Count > 0 ? pool.Pop() : Create();
            mote.T.gameObject.SetActive(true);
            mote.T.localPosition = position;
            mote.Velocity = velocity;
            mote.Age = 0f;
            mote.Life = life;
            mote.Size = Random.Range(0.08f, 0.15f) * (0.7f + 0.3f * strength);
            mote.Color = Color.Lerp(color, Color.white, 0.25f + 0.2f * strength);
            f.Motes.Add(mote);
        }

        private void Step(Fx f, float dt)
        {
            for (int i = f.Motes.Count - 1; i >= 0; i--)
            {
                var mote = f.Motes[i];
                mote.Age += dt;
                if (mote.Age >= mote.Life) { f.Motes.RemoveAt(i); Release(mote); continue; }
                float k = mote.Age / mote.Life;
                mote.Velocity *= 1f - 0.6f * dt;               // ralentit doucement
                mote.T.localPosition += mote.Velocity * dt;
                mote.T.localRotation = Quaternion.Euler(k * 180f, k * 240f, 0f);
                float fade = Mathf.Sin(k * Mathf.PI);           // apparaît puis s'efface
                // Matière non éclairée : l'apparition et la disparition se font par la taille.
                mote.T.localScale = Vector3.one * mote.Size * fade;
                Shapes.SetColor(mote.R, mote.Color);
            }
        }

        private Mote Create()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Mote";
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(transform, false);
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = WorldView.UnlitMaterial;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            return new Mote { T = go.transform, R = r };
        }

        private void Release(Mote mote)
        {
            mote.T.gameObject.SetActive(false);
            pool.Push(mote);
        }

        /// <summary>Boîte de la structure en coordonnées du monde.</summary>
        private static (Vector3 lo, Vector3 hi) Bounds(Multiblock s)
        {
            var lo = GridMath.CellCenter(s.Min) - new Vector3(0.5f, 0f, 0.5f);
            var hi = GridMath.CellCenter(s.Max) + new Vector3(0.5f, GridMath.LevelHeight, 0.5f);
            return (lo, hi);
        }

        /// <summary>Point au hasard sur la paroi (dessus et côtés), juste à l'extérieur.</summary>
        private static Vector3 Surface(Vector3 lo, Vector3 hi, bool wallsOnly = false)
        {
            var size = hi - lo;
            float top = size.x * size.z, side = (size.x + size.z) * size.y;
            float pick = Random.value * (top + 2f * side);
            float u = Random.value, v = Random.value;
            const float out_ = 0.04f;
            if (pick < top) return new Vector3(lo.x + u * size.x, hi.y + out_, lo.z + v * size.z);
            pick -= top;
            bool alongX = Random.value < size.x / (size.x + size.z);
            float y = lo.y + v * size.y;
            if (alongX) return new Vector3(lo.x + u * size.x, y, Random.value < 0.5f ? lo.z - out_ : hi.z + out_);
            return new Vector3(Random.value < 0.5f ? lo.x - out_ : hi.x + out_, y, lo.z + u * size.z);
        }

        private static Color ColorOf(MultiblockKind k) => k switch
        {
            MultiblockKind.Fission => Palette.Success,
            MultiblockKind.Fusion => Palette.Lavender,
            MultiblockKind.Sps => Color.Lerp(Palette.Lavender, Palette.Danger, 0.35f),
            _ => Palette.Accent,
        };
    }
}
