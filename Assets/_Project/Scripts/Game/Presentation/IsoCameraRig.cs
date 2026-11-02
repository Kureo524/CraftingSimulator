using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Nucleaire.Game
{
    /// <summary>Caméra isométrique orthographique : déplacement (raccourcis ou clic molette), zoom (molette),
    /// rotation libre au défilement horizontal (deux doigts sur le pavé tactile, molette inclinable),
    /// et R pour aller au quart de tour suivant (angles « iso » 45°, 135°…).</summary>
    public sealed class IsoCameraRig : MonoBehaviour
    {
        [SerializeField] private float pitch = 32f;
        [SerializeField] private float distance = 40f;
        [SerializeField] private float minZoom = 3.5f, maxZoom = 16f;
        [SerializeField] private float panSpeed = 1.1f;   // en « tailles d'écran » par seconde
        [SerializeField] private float smoothing = 12f;
        [Tooltip("Degrés de rotation par unité de défilement horizontal.")]
        [SerializeField] private float freeRotateSpeed = 3f;

        private Vector3 target, smoothTarget;
        private float zoom = 7.5f, smoothZoom = 7.5f;
        private float yaw = 45f, smoothYaw = 45f;
        private Vector2 bounds;

        public Camera Camera { get; private set; }
        public GameInput Input { get; set; }
        /// <summary>Faux quand un panneau capture les touches (ex. inventaire : R = voir la recette).</summary>
        public System.Func<bool> RotationAllowed { get; set; } = () => true;
        public float Zoom => smoothZoom;
        public Vector3 Focus3 => smoothTarget;
        /// <summary>Direction horizontale de la caméra vers la scène.</summary>
        public Vector3 HorizontalForward => Quaternion.Euler(0, smoothYaw, 0) * Vector3.forward;
        /// <summary>Indique si le pointeur est au-dessus de l'interface (fourni par l'UI).</summary>
        public Func<bool> PointerOverUI { get; set; } = () => false;
        /// <summary>Quart de tour actuel (0..3) : sert à orienter la boussole de l'UI.</summary>
        public float Yaw => smoothYaw;
        public event Action Rotated;

        public void Init(Vector2Int gridSize)
        {
            bounds = gridSize;
            target = smoothTarget = new Vector3(gridSize.x / 2f, 0f, gridSize.y / 2f - 2f);

            Camera = GetComponentInChildren<Camera>();
            if (Camera == null)
            {
                var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
                camGo.transform.SetParent(transform, false);
                Camera = camGo.AddComponent<Camera>();
                camGo.AddComponent<AudioListener>();
            }
            Camera.orthographic = true;
            Camera.clearFlags = CameraClearFlags.SolidColor;
            Camera.backgroundColor = Palette.Bg;
            Camera.nearClipPlane = 0.1f;
            Camera.farClipPlane = distance * 2.5f;
            Apply(true);
        }

        private void Update()
        {
            var mouse = Mouse.current;
            float dt = Time.unscaledDeltaTime;

            if (Input != null)
            {
                Vector2 move = Vector2.zero;
                if (Input.Held(GameInput.CameraForward)) move.y += 1;
                if (Input.Held(GameInput.CameraBack)) move.y -= 1;
                if (Input.Held(GameInput.CameraRight)) move.x += 1;
                if (Input.Held(GameInput.CameraLeft)) move.x -= 1;
                if (move != Vector2.zero) Pan(move.normalized * (panSpeed * zoom * 2f * dt));
                if (RotationAllowed() && Input.Pressed(GameInput.RotateCamera))
                {
                    bool reverse = Keyboard.current != null && Keyboard.current.shiftKey.isPressed;
                    Rotate(reverse ? -1 : 1);
                }
            }

            if (mouse != null)
            {
                if (mouse.middleButton.isPressed)
                {
                    Vector2 delta = mouse.delta.ReadValue();
                    float unitsPerPixel = 2f * zoom / Screen.height;
                    Pan(-delta * unitsPerPixel * 1.2f);
                }
                var wheel = mouse.scroll.ReadValue();
                bool overUI = PointerOverUI();
                // Défilement horizontal : rotation libre. Vertical : zoom.
                if (Mathf.Abs(wheel.x) > Mathf.Abs(wheel.y) && Mathf.Abs(wheel.x) > 0.01f)
                {
                    if (!overUI && RotationAllowed()) RotateFree(wheel.x * freeRotateSpeed);
                }
                else if (Mathf.Abs(wheel.y) > 0.01f && !overUI)
                    zoom = Mathf.Clamp(zoom * (wheel.y > 0 ? 0.88f : 1.14f), minZoom, maxZoom);
            }

            Apply(false);
        }

        /// <summary>Déplacement relatif à l'orientation de la caméra.</summary>
        private void Pan(Vector2 screenMove)
        {
            var right = Quaternion.Euler(0, yaw, 0) * Vector3.right;
            var forward = Quaternion.Euler(0, yaw, 0) * Vector3.forward;
            target += right * screenMove.x + forward * screenMove.y / Mathf.Sin(pitch * Mathf.Deg2Rad) * 0.5f;
            target.x = Mathf.Clamp(target.x, 0f, bounds.x);
            target.z = Mathf.Clamp(target.z, 0f, bounds.y);
        }

        /// <summary>R : aller au quart de tour suivant (45°, 135°, 225°, 315°), même après une rotation libre.</summary>
        public void Rotate(int dir)
        {
            float steps = (yaw - 45f) / 90f;
            float next = dir > 0 ? Mathf.Floor(steps + 1e-3f) + 1f : Mathf.Ceil(steps - 1e-3f) - 1f;
            yaw = 45f + next * 90f;
            Rotated?.Invoke();
        }

        /// <summary>Rotation libre (défilement horizontal), suivie sans lissage pour rester sous le doigt.</summary>
        public void RotateFree(float degrees)
        {
            degrees = Mathf.Clamp(degrees, -20f, 20f);
            yaw += degrees;
            smoothYaw += degrees;
            Rotated?.Invoke();
        }

        public void Focus(Vector3 worldPoint) => target = new Vector3(worldPoint.x, 0f, worldPoint.z);

        /// <summary>Utilisé par les captures automatiques.</summary>
        public void SetZoomForTests(float z) => zoom = Mathf.Clamp(z, minZoom, maxZoom);

        private void Apply(bool instant)
        {
            float k = instant ? 1f : 1f - Mathf.Exp(-smoothing * Time.unscaledDeltaTime);
            smoothTarget = Vector3.Lerp(smoothTarget, target, k);
            smoothZoom = Mathf.Lerp(smoothZoom, zoom, k);
            smoothYaw = Mathf.Lerp(smoothYaw, yaw, k);

            transform.position = smoothTarget;
            transform.rotation = Quaternion.Euler(pitch, smoothYaw, 0f);
            Camera.transform.localPosition = new Vector3(0f, 0f, -distance);
            Camera.transform.localRotation = Quaternion.identity;
            Camera.orthographicSize = smoothZoom;
        }


    }
}
