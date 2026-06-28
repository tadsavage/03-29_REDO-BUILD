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

        private Corridor _corridor;
        private Vector3 _travelDir;     // direction of travel if the player clicks THIS pad
        private Material _mat;
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
            go.transform.rotation = Quaternion.LookRotation(new Vector3(travelDir.x, 0f, travelDir.z).normalized, Vector3.up);

            var pad = go.AddComponent<AislePad>();
            pad._corridor = corridor;
            pad._travelDir = travelDir;
            pad.Build(corridor.Width);
            return pad;
        }

        private void Build(float corridorWidth)
        {
            float width = Mathf.Max(0.6f, corridorWidth - 0.5f); // just short of the aisle width
            float depth = Mathf.Min(width * 0.5f, 1.4f);          // how far the chevron reaches forward
            float thickness = Mathf.Clamp(width * 0.18f, 0.12f, 0.45f);

            var mf = gameObject.AddComponent<MeshFilter>();
            mf.sharedMesh = BuildChevron(width, depth, thickness);

            var mr = gameObject.AddComponent<MeshRenderer>();
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;

            var shader = Shader.Find("Universal Render Pipeline/Unlit")
                         ?? Shader.Find("Unlit/Color")
                         ?? Shader.Find("Sprites/Default");
            _mat = new Material(shader);
            _mat.color = OrangeGlow;
            if (_mat.HasProperty("_BaseColor")) _mat.SetColor("_BaseColor", OrangeGlow);
            if (_mat.HasProperty("_Cull")) _mat.SetFloat("_Cull", 0f); // double-sided — never culled from above
            mr.sharedMaterial = _mat;

            // Solid box covering the chevron footprint — a flat MeshCollider is too thin to click.
            var b = mf.sharedMesh.bounds;
            var col = gameObject.AddComponent<BoxCollider>();
            col.center = new Vector3(b.center.x, 0.25f, b.center.z);
            col.size = new Vector3(Mathf.Max(b.size.x, 0.4f), 0.5f, Mathf.Max(b.size.z, 0.4f));

            Debug.Log($"[AislePad] Created at {transform.position} width={width:0.0} (corridor {corridorWidth:0.0}) shader={shader?.name}");
        }

        /// <summary>
        /// A flat filled chevron ">" on the local XZ plane, tip pointing +Z (local forward).
        /// Two arms (back-corners → tip), each a quad of constant thickness.
        /// </summary>
        private static Mesh BuildChevron(float width, float depth, float thickness)
        {
            float hw = width * 0.5f;
            Vector3 tip = new Vector3(0f, 0f, depth);
            Vector3 bl = new Vector3(-hw, 0f, 0f);
            Vector3 br = new Vector3(hw, 0f, 0f);

            var verts = new System.Collections.Generic.List<Vector3>();
            var tris = new System.Collections.Generic.List<int>();

            AddArm(verts, tris, tip, br, thickness); // right arm: tip → back-right
            AddArm(verts, tris, tip, bl, thickness); // left arm:  tip → back-left

            var mesh = new Mesh { name = "ChevronPad" };
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void AddArm(System.Collections.Generic.List<Vector3> verts,
            System.Collections.Generic.List<int> tris, Vector3 a, Vector3 b, float thickness)
        {
            Vector3 dir = (b - a).normalized;
            Vector3 perp = Vector3.Cross(Vector3.up, dir).normalized * (thickness * 0.5f);
            int i = verts.Count;
            verts.Add(a + perp); // 0
            verts.Add(a - perp); // 1
            verts.Add(b - perp); // 2
            verts.Add(b + perp); // 3
            // Wound so the face points +Y (up).
            tris.Add(i + 0); tris.Add(i + 2); tris.Add(i + 1);
            tris.Add(i + 0); tris.Add(i + 3); tris.Add(i + 2);
        }

        private void Update()
        {
            // Gentle glow pulse so it reads as interactive.
            _pulse += Time.deltaTime * 2.2f;
            float k = 0.65f + 0.35f * (0.5f + 0.5f * Mathf.Sin(_pulse));
            if (_mat == null) return;
            Color c = OrangeGlow * k; c.a = 1f;
            _mat.color = c;
            if (_mat.HasProperty("_BaseColor")) _mat.SetColor("_BaseColor", c);
        }

        private void OnMouseEnter()
        {
            Debug.Log("[AislePad] Hover enter — collider is receiving mouse events.");
            transform.localScale = Vector3.one * 1.08f;

            // Cache the hover UI on first enter.
            if (_hoverUI == null)
                _hoverUI = FindObjectOfType<WorldHoverPopupUI>();
        }

        private void OnMouseExit()
        {
            transform.localScale = Vector3.one;

            // Hide the hover UI when leaving.
            if (_hoverUI != null)
                _hoverUI.TickHover(false, "", 0, 0, transform.position, Camera.main);
        }

        // Fires every frame while the cursor is over the pad's collider.
        private void OnMouseOver()
        {
            // Show hover UI with chevron instructions.
            if (_hoverUI == null)
                _hoverUI = FindObjectOfType<WorldHoverPopupUI>();
            if (_hoverUI != null)
                _hoverUI.TickHover(true, "Right-click: flip direction  •  Double-click: edit", 0, 0,
                    transform.position, Camera.main);

            var mouse = Mouse.current;
            if (mouse == null) return;

            if (mouse.rightButton.wasPressedThisFrame)
                FlipDirection();

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
            }
        }

        private void FlipDirection()
        {
            _travelDir = -_travelDir;
            transform.rotation = Quaternion.LookRotation(
                new Vector3(_travelDir.x, 0f, _travelDir.z).normalized, Vector3.up);
            UIToast.Show("Direction of travel flipped", 1f);
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
            _ = new AisleInitializationModal(root, sections, OnModalClosed, _corridor.Centerline);
        }

        private void OnModalClosed(bool success)
        {
            _modalOpen = false;
            if (!success) return;

            // Un-ghost every section that got named (the chevron path doesn't go through GhostRack).
            foreach (var s in _corridor.SideRowA.Concat(_corridor.SideRowB))
            {
                if (s == null) continue;
                var gr = s.GetComponent<GhostRack>();
                if (gr != null) gr.RestoreFromGhost();
            }
        }
    }
}
