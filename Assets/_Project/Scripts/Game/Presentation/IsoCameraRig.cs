using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Nucleaire.Game
{
    /// <summary>Caméra isométrique orthographique : déplacement (ZQSD/WASD ou clic molette), zoom (molette),
    /// rotation par quarts de tour (A/E sur AZERTY, Q/E sur QWERTY).</summary>
    public sealed class IsoCameraRig : MonoBehaviour
    {
        [SerializeField] private float pitch = 32f;
        [SerializeField] private float distance = 40f;
        [SerializeField] private float minZoom = 3.5f, maxZoom = 16f;
        [SerializeField] private float panSpeed = 1.1f;   // en « tailles d'écran » par seconde
        [SerializeField] private float smoothing = 12f;

        private Vector3 target, smoothTarget;
        private float zoom = 7.5f, smoothZoom = 7.5f;
        private int quarter;
        private float yaw = 45f, smoothYaw = 45f;
        private Vector2 bounds;

        public Camera Camera { get; private set; }
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
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            float dt = Time.unscaledDeltaTime;

            if (kb != null && !IsTyping())
            {
                Vector2 move = Vector2.zero;
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed) move.y += 1;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed) move.y -= 1;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) move.x += 1;
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) move.x -= 1;
                if (move != Vector2.zero) Pan(move.normalized * (panSpeed * zoom * 2f * dt));

                if (kb.qKey.wasPressedThisFrame) Rotate(-1);
                if (kb.eKey.wasPressedThisFrame) Rotate(1);
            }

            if (mouse != null)
            {
                if (mouse.middleButton.isPressed)
                {
                    Vector2 delta = mouse.delta.ReadValue();
                    float unitsPerPixel = 2f * zoom / Screen.height;
                    Pan(-delta * unitsPerPixel * 1.2f);
                }
                float scroll = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(scroll) > 0.01f && !PointerOverUI())
                    zoom = Mathf.Clamp(zoom * (scroll > 0 ? 0.88f : 1.14f), minZoom, maxZoom);
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

        public void Rotate(int dir)
        {
            quarter = (quarter + dir + 4) % 4;
            yaw += dir * 90f;
            Rotated?.Invoke();
        }

        public void Focus(Vector3 worldPoint) => target = new Vector3(worldPoint.x, 0f, worldPoint.z);

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

        public bool ScreenToCell(Vector2 screen, out Vector2Int cell, out Vector3 point)
        {
            cell = default;
            if (!GridMath.RayToGround(Camera.ScreenPointToRay(screen), out point)) return false;
            cell = GridMath.WorldToCell(point);
            return true;
        }

        /// <summary>Vrai si un champ texte de l'UI a le focus (on ne déplace pas la caméra en tapant).</summary>
        public Func<bool> IsTyping { get; set; } = () => false;
    }
}
