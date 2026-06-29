using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Warehouse
{
    /// <summary>
    /// A glowing orange chevron laid flush on the floor at one end of a corridor, pointing INTO the
    /// aisle. Clicking it = "the picker enters from this end" → that sets the travel direction, and
    /// opens the aisle initialization modal for this corridor.
    /// Built procedurally (mesh + emissive material) so no art asset is needed.
    /// </summary>
    public class AislePad : MonoBehaviour
    {
        private static readonly Color OrangeGlow = new Color(0xF0 / 255f, 0x8C / 255f, 0x22 / 255f, 1f);

        private const float DoubleClickWindow = 0.3f;

        private const float BaseScale = 0.5f;
        private const float HoverScale = BaseScale * 1.08f;

        private Corridor _corridor;
        private Vector3 _travelDir;     // direction of travel if the player clicks THIS pad
        private Vector2 _chevronPos2D;  // flat (X, Z) position of this chevron
        private SpriteRenderer _spriteRenderer;
        private float _pulse;
        private float _lastLeftClickTime = -1f;
        private bool _modalOpen;
        private WorldHoverPopupUI _hoverUI;

        /// <summary>
        /// Build a chevron pad at <paramref name="position"/> pointing along <paramref name="travelDir"/>.
        /// </summary>
        public static AislePad Create(Corridor corridor, Vector3 position, Vector3 travelDir, Transform parent)
        {
            var go = new GameObject("AislePad");
            go.transform.SetParent(parent, false);
            go.transform.position = position + Vector3.up * 0.03f; // just above the floor
            go.transform.rotation = CalculatePadRotation(travelDir, false);

            var pad = go.AddComponent<AislePad>();
            pad._corridor = corridor;
            pad._travelDir = travelDir;
            pad.Build(corridor.Width);
            return pad;
        }

        private void Build(float corridorWidth)
        {
            // Store this chevron's 2D position for label distance calculations
            _chevronPos2D = new Vector2(transform.position.x, transform.position.z);

            // Load the chevron sprite
            var sprite = Resources.Load<Sprite>("UI/Chevron");
            if (sprite == null) return;

            // Add sprite renderer
            _spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
            _spriteRenderer.sprite = sprite;
            _spriteRenderer.color = OrangeGlow;
            _spriteRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _spriteRenderer.receiveShadows = false;
            _spriteRenderer.sortingOrder = 10;

            // Scale the sprite to a fixed small size
            gameObject.transform.localScale = new Vector3(BaseScale, BaseScale, 1f);

            // Collider for clicking
            var col = gameObject.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0.5f, 0f);
            col.size = new Vector3(5f, 5f, 1f);
        }


        private void Update()
        {
            // Gentle glow pulse so it reads as interactive.
            _pulse += Time.deltaTime * 2.2f;
            float k = 0.65f + 0.35f * (0.5f + 0.5f * Mathf.Sin(_pulse));
            if (_spriteRenderer == null) return;
            Color c = OrangeGlow * k;
            c.a = 1f;
            _spriteRenderer.color = c;
        }

        private void OnMouseEnter()
        {
            // Cache the hover UI on first enter.
            if (_hoverUI == null)
                _hoverUI = Object.FindAnyObjectByType<WorldHoverPopupUI>();
        }

        private void OnMouseOver()
        {
            var mouse = Mouse.current;
            if (mouse == null) return;

            // Handle input FIRST
            if (mouse.rightButton.wasPressedThisFrame)
            {
                FlipDirection();
                return;
            }

            if (mouse.leftButton.wasPressedThisFrame)
            {
                float now = Time.unscaledTime;
                if (now - _lastLeftClickTime < DoubleClickWindow)
                {
                    _lastLeftClickTime = -1f;
                    OpenModal();
                }
                else
                {
                    _lastLeftClickTime = now;
                }
                return;
            }

            // Show hover UI with chevron instructions.
            if (_hoverUI == null)
                _hoverUI = Object.FindAnyObjectByType<WorldHoverPopupUI>();
            if (_hoverUI != null)
                _hoverUI.TickHover(true, "Right-click: flip direction  •  Double-click: edit", 0, 0,
                    transform.position, Camera.main);
        }

        private void OnMouseExit()
        {
            // Hide the hover UI when leaving.
            if (_hoverUI != null)
                _hoverUI.TickHover(false, "", 0, 0, transform.position, Camera.main);
        }

        private void FlipDirection()
        {
            _travelDir = -_travelDir;
            transform.rotation = CalculatePadRotation(_travelDir, true);
            UIToast.Show("Direction of travel flipped", 1f);
        }

        private static Quaternion CalculatePadRotation(Vector3 travelDir, bool isFlipped)
        {
            var yaw = Quaternion.LookRotation(new Vector3(travelDir.x, 0f, travelDir.z).normalized, Vector3.up).eulerAngles.y;
            float xRotation = isFlipped ? 270f : -90f;
            return Quaternion.Euler(xRotation, yaw, 0f);
        }

        private void OpenModal()
        {
            if (_modalOpen) return;

            var root = AisleInitializationModal.FindHudRoot();
            if (root == null)
            {
                Debug.LogError("[AislePad] No HUD UIDocument root — modal can't render.");
                return;
            }

            // The aisle is the corridor: both bordering rows. Cull labels toward the walkway centre.
            var sections = new List<RackLabelDisplay>();
            sections.AddRange(_corridor.SideRowA);
            sections.AddRange(_corridor.SideRowB);
            sections = sections.Where(s => s != null).Distinct().ToList();

            if (sections.Count == 0) { UIToast.Show("No racks border this aisle.", 2f); return; }

            _modalOpen = true;
            _ = new AisleInitializationModal(root, sections, OnModalClosed, _corridor.Centerline, _travelDir, _chevronPos2D);
        }

        private void OnModalClosed(bool success)
        {
            _modalOpen = false;
            if (!success) return;

            // Un-ghost every section that got named. InitializeAisle already culled
            // labels to face-the-aisle only; just restore materials and enable display.
            foreach (var s in _corridor.SideRowA.Concat(_corridor.SideRowB))
            {
                if (s == null) continue;
                var gr = s.GetComponent<GhostRack>();
                if (gr != null) gr.RestoreFromGhost();

                // Enable the RackLabelDisplay
                var rackLabel = s.GetComponent<RackLabelDisplay>();
                if (rackLabel != null) rackLabel.enabled = true;
            }
        }
    }
}
