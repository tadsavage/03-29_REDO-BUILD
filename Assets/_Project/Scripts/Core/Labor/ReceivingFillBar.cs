using UnityEngine;
using UnityEngine.UI;

namespace GameCore.Labor
{
    /// <summary>
    /// A world-space fill bar that floats above the receiver's head during the receiving animation.
    /// Features:
    /// - Camera-facing billboard (rotates to face camera)
    /// - Scales with distance (normal world-space behavior)
    /// - Fills left-to-right over a configurable duration (default 5 seconds)
    /// - Auto-hides when not receiving
    ///
    /// Attach this script to the receiver NPC. It expects:
    /// - A child Canvas (world space) named "ReceivingCanvas"
    /// - An Image child of that canvas named "FillBar" with Image.fillMethod = Horizontal
    /// - The canvas RectTransform positioned above the receiver's head (e.g., local y = 1.5)
    /// </summary>
    public class ReceivingFillBar : MonoBehaviour
    {
        [SerializeField] private Canvas _canvas;
        [SerializeField] private Image _fillImage;
        [SerializeField] private float _fillerDuration = 10f;
        [SerializeField] private float _fillAmount = 0f;

        // This component is added dynamically at runtime (no dedicated Receiver prefab to hold an
        // Inspector assignment — same reasoning as ReceiverReceivingWorkflow's _solidCaseMaterial), so
        // the world-space canvas hierarchy it needs can never be hand-parented under the receiver in
        // the Editor. Instantiate it here instead, the same way ReceiverReceivingWorkflow spawns the
        // dust poof prefab.
        private const string FillBarCanvasPrefabPath = "Assets/_Project/Prefabs/UI/ReceivingFillBarCanvas.prefab";

        private float _elapsedTime;
        private bool _isReceiving;
        private Camera _mainCamera;

        private void Start()
        {
            _mainCamera = Camera.main;

            // Auto-locate canvas and fill image if not assigned
            if (_canvas == null)
                _canvas = GetComponentInChildren<Canvas>();
            if (_canvas == null)
                _canvas = SpawnFillBarCanvas();
            if (_fillImage == null && _canvas != null)
                _fillImage = _canvas.transform.Find("FillBar")?.GetComponent<Image>()
                             ?? _canvas.GetComponentInChildren<Image>();

            // Start hidden
            if (_canvas != null)
                _canvas.gameObject.SetActive(false);
        }

        private Canvas SpawnFillBarCanvas()
        {
#if UNITY_EDITOR
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(FillBarCanvasPrefabPath);
#else
            var prefab = Resources.Load<GameObject>("ReceivingFillBarCanvas");
#endif
            if (prefab == null)
            {
                Debug.LogWarning("[ReceivingFillBar] ReceivingFillBarCanvas prefab not found — receiving will proceed with no visible progress bar.");
                return null;
            }

            var instance = Instantiate(prefab, transform);
            instance.transform.localPosition = new Vector3(0f, 2.2f, 0f);
            instance.transform.localRotation = Quaternion.identity;
            return instance.GetComponent<Canvas>();
        }

        private void Update()
        {
            if (!_isReceiving)
                return;

            // Advance fill amount
            _elapsedTime += Time.deltaTime;
            _fillAmount = Mathf.Clamp01(_elapsedTime / _fillerDuration);

            if (_fillImage != null)
                _fillImage.fillAmount = _fillAmount;

            // Face camera (billboard effect). A UI canvas reads correctly when its LOCAL +Z points
            // AWAY from the viewer (the readable side faces -Z) — pointing +Z AT the camera instead
            // shows the viewer the back of the canvas, which mirrors the text. So this points away
            // from the camera, not toward it.
            if (_canvas != null && _mainCamera != null)
            {
                Vector3 directionAwayFromCamera = _canvas.transform.position - _mainCamera.transform.position;
                _canvas.transform.rotation = Quaternion.LookRotation(directionAwayFromCamera);
            }

            // Check if receiving complete
            if (_elapsedTime >= _fillerDuration)
            {
                CompleteReceiving();
            }
        }

        /// <summary>Start the receiving animation and show the fill bar.</summary>
        public void StartReceiving()
        {
            _elapsedTime = 0f;
            _isReceiving = true;

            if (_canvas != null)
                _canvas.gameObject.SetActive(true);

            if (_fillImage != null)
                _fillImage.fillAmount = 0f;
        }

        /// <summary>Stop receiving and hide the fill bar.</summary>
        public void CompleteReceiving()
        {
            _isReceiving = false;

            if (_canvas != null)
                _canvas.gameObject.SetActive(false);

            if (_fillImage != null)
                _fillImage.fillAmount = 1f; // Show as complete
        }

        /// <summary>Get current fill amount (0-1).</summary>
        public float GetFillAmount()
        {
            return Mathf.Clamp01(_elapsedTime / _fillerDuration);
        }

        /// <summary>Check if receiving is currently active.</summary>
        public bool IsReceiving => _isReceiving;

        /// <summary>Get/set the fill duration in seconds.</summary>
        public float FillerDuration
        {
            get => _fillerDuration;
            set => _fillerDuration = Mathf.Max(0.1f, value);
        }
    }
}
