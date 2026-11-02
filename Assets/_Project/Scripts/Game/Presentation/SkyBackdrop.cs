using UnityEngine;

namespace Nucleaire.Game
{
    /// <summary>Ciel de constellations nocturne (DA, §6), généré sans image et affiché derrière l'atelier.
    /// Couche statique (lumières diffuses, étoiles, constellation) + couche animée (scintillement,
    /// constellation lavande qui respire, étoile filante).</summary>
    public sealed class SkyBackdrop : MonoBehaviour
    {
        private const int W = 960, H = 600;
        private const float AnimFps = 20f;

        private Camera cam;
        private Texture2D texture;
        private Color32[] basePixels, pixels;
        private Transform quad;
        private float nextFrame, nextShootingStar, shootingStart = -10f;
        private Vector2 shootFrom, shootDir;

        private struct Twinkle { public Vector2 p; public float r, speed, phase; }
        private Twinkle[] twinkles;
        private Vector2[] lavenderStars;
        private readonly System.Collections.Generic.List<int> dirty = new();

        public void Init(Camera camera, Material unlitMaterial)
        {
            cam = camera;
            var rng = new System.Random(1337);
            texture = new Texture2D(W, H, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "Sky" };
            basePixels = BuildStatic(rng);
            pixels = (Color32[])basePixels.Clone();
            texture.SetPixels32(pixels);
            texture.Apply(false);

            twinkles = new Twinkle[70];
            for (int i = 0; i < twinkles.Length; i++)
                twinkles[i] = new Twinkle
                {
                    p = new Vector2((float)rng.NextDouble() * W, (float)rng.NextDouble() * H),
                    r = 0.6f + (float)rng.NextDouble() * 0.9f,
                    speed = 0.4f + (float)rng.NextDouble() * 1.4f,
                    phase = (float)rng.NextDouble() * Mathf.PI * 2f,
                };
            lavenderStars = new Vector2[6];
            var origin = new Vector2(W * 0.68f, H * 0.62f);
            for (int i = 0; i < lavenderStars.Length; i++)
                lavenderStars[i] = origin + new Vector2((float)rng.NextDouble() * 160f - 40f, (float)rng.NextDouble() * 110f - 30f);
            nextShootingStar = Time.unscaledTime + 4f;

            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = "Sky";
            Destroy(go.GetComponent<Collider>());
            quad = go.transform;
            quad.SetParent(cam.transform, false);
            var mat = new Material(unlitMaterial) { mainTexture = texture };
            mat.SetTexture("_BaseMap", texture);
            mat.SetColor("_BaseColor", Color.white);
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            go.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        private void LateUpdate()
        {
            if (cam == null) return;
            float d = cam.farClipPlane * 0.95f;
            float h = cam.orthographicSize * 2f;
            float w = h * cam.aspect;
            float texAspect = (float)W / H;
            // « scale-and-crop » : couvre tout l'écran en gardant les proportions.
            if (w / h > texAspect) h = w / texAspect; else w = h * texAspect;
            quad.localPosition = new Vector3(0, 0, d);
            quad.localRotation = Quaternion.identity;
            quad.localScale = new Vector3(w, h, 1f);

            if (Time.unscaledTime >= nextFrame)
            {
                nextFrame = Time.unscaledTime + 1f / AnimFps;
                Animate(Time.unscaledTime);
            }
        }

        // ---------------- Couche statique ----------------

        private static Color32[] BuildStatic(System.Random rng)
        {
            var px = new Color[W * H];
            var bg = new Color(12 / 255f, 12 / 255f, 18 / 255f);
            var lights = new[]
            {
                (Palette.Accent, new Vector2(0.12f, 0.18f), 0.42f, 0.31f),
                (Palette.Lavender, new Vector2(0.88f, 0.12f), 0.36f, 0.25f),
                (Palette.Success, new Vector2(0.60f, 1.00f), 0.42f, 0.19f),
            };
            float diag = Mathf.Sqrt(W * W + H * H);
            for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                var c = bg;
                // y image = 0 en haut : on retourne pour la texture (origine en bas).
                var uv = new Vector2((float)x / W, 1f - (float)y / H);
                foreach (var (col, pos, radius, intensity) in lights)
                {
                    float dist = Vector2.Distance(new Vector2(uv.x * W, uv.y * H), new Vector2(pos.x * W, pos.y * H)) / (radius * diag);
                    float a = Mathf.Max(0f, 1f - dist) * intensity;
                    c = Color.Lerp(c, col, a * 0.35f);
                }
                px[(H - 1 - y) * W + x] = c;
            }

            int starCount = W * H / 2400;
            var bright = new System.Collections.Generic.List<Vector2>();
            for (int i = 0; i < starCount; i++)
            {
                var p = new Vector2((float)rng.NextDouble() * W, (float)rng.NextDouble() * H);
                float r = 0.3f + (float)rng.NextDouble() * 1.3f;
                float a = 0.2f + (float)rng.NextDouble() * 0.45f;
                DrawDot(px, p, r, Palette.Text, a);
                if (r > 1.2f && bright.Count < 14) bright.Add(p);
            }
            for (int i = 0; i < bright.Count; i++)
            for (int j = i + 1; j < bright.Count; j++)
                if (Vector2.Distance(bright[i], bright[j]) < 170f)
                    DrawLine(px, bright[i], bright[j], Palette.Text, 0.16f);

            var result = new Color32[px.Length];
            for (int i = 0; i < px.Length; i++) result[i] = px[i];
            return result;
        }

        private static void DrawDot(Color[] px, Vector2 p, float r, Color col, float alpha)
        {
            int r0 = Mathf.CeilToInt(r + 1);
            for (int y = (int)p.y - r0; y <= (int)p.y + r0; y++)
            for (int x = (int)p.x - r0; x <= (int)p.x + r0; x++)
            {
                if (x < 0 || y < 0 || x >= W || y >= H) continue;
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), p);
                float a = Mathf.Clamp01(r + 0.5f - d) * alpha;
                if (a <= 0) continue;
                int i = y * W + x;
                px[i] = Color.Lerp(px[i], col, a);
            }
        }

        private static void DrawLine(Color[] px, Vector2 a, Vector2 b, Color col, float alpha)
        {
            int steps = Mathf.CeilToInt(Vector2.Distance(a, b));
            for (int s = 0; s <= steps; s++)
            {
                var p = Vector2.Lerp(a, b, (float)s / steps);
                int x = (int)p.x, y = (int)p.y;
                if (x < 0 || y < 0 || x >= W || y >= H) continue;
                int i = y * W + x;
                px[i] = Color.Lerp(px[i], col, alpha);
            }
        }

        // ---------------- Couche animée ----------------

        private void Animate(float t)
        {
            foreach (int i in dirty) pixels[i] = basePixels[i];
            dirty.Clear();

            foreach (var s in twinkles)
            {
                float tw = 0.5f + 0.5f * Mathf.Sin(t * s.speed + s.phase);
                Plot(s.p, s.r * Mathf.Lerp(0.8f, 1.2f, tw), Palette.Text, 0.12f + 0.5f * tw);
            }

            float breathe = Mathf.Lerp(0.08f, 0.20f, 0.5f + 0.5f * Mathf.Sin(t * Mathf.PI * 2f / 16f));
            for (int i = 0; i < lavenderStars.Length - 1; i++) PlotLine(lavenderStars[i], lavenderStars[i + 1], Palette.Lavender, breathe);
            foreach (var p in lavenderStars) Plot(p, 1.3f, Palette.Lavender, 0.6f);

            if (t >= nextShootingStar)
            {
                shootingStart = t;
                nextShootingStar = t + Random.Range(7f, 14f);
                shootFrom = new Vector2(Random.Range(W * 0.15f, W * 0.85f), Random.Range(H * 0.7f, H * 0.95f));
                shootDir = new Vector2(Random.Range(-1f, 1f) < 0 ? -1f : 1f, -0.35f).normalized;
            }
            float k = (t - shootingStart) / 1.3f;
            if (k >= 0f && k <= 1f)
            {
                var head = shootFrom + shootDir * (k * 260f);
                float fade = Mathf.Sin(k * Mathf.PI);
                PlotLine(head - shootDir * 140f, head, new Color(201 / 255f, 214 / 255f, 245 / 255f), 0.55f * fade, taper: true);
            }

            texture.SetPixels32(pixels);
            texture.Apply(false);
        }

        private void Plot(Vector2 p, float r, Color col, float alpha)
        {
            int r0 = Mathf.CeilToInt(r + 1);
            for (int y = (int)p.y - r0; y <= (int)p.y + r0; y++)
            for (int x = (int)p.x - r0; x <= (int)p.x + r0; x++)
            {
                if (x < 0 || y < 0 || x >= W || y >= H) continue;
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), p);
                float a = Mathf.Clamp01(r + 0.5f - d) * alpha;
                if (a <= 0) continue;
                Blend(y * W + x, col, a);
            }
        }

        private void PlotLine(Vector2 a, Vector2 b, Color col, float alpha, bool taper = false)
        {
            int steps = Mathf.CeilToInt(Vector2.Distance(a, b));
            for (int s = 0; s <= steps; s++)
            {
                float f = (float)s / Mathf.Max(1, steps);
                var p = Vector2.Lerp(a, b, f);
                int x = (int)p.x, y = (int)p.y;
                if (x < 0 || y < 0 || x >= W || y >= H) continue;
                Blend(y * W + x, col, taper ? alpha * f : alpha);
            }
        }

        private void Blend(int i, Color col, float a)
        {
            dirty.Add(i);
            Color c = basePixels[i];
            pixels[i] = Color.Lerp(c, col, Mathf.Clamp01(a));
        }
    }
}
