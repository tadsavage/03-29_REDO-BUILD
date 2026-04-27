using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace SaveLoadSystem
{
    public enum SaveLoadMode { Save, Load }

    public class SaveLoadWindowController : MonoBehaviour
    {
        [Header("UI Documents")]
        [SerializeField] private UIDocument saveLoadDocument;
        [SerializeField] private VisualTreeAsset saveSlotTemplate;

        private VisualElement root;
        private VisualElement overlay;
        private VisualElement modalPanel;
        private Label titleLabel;
        private Button tabSaveButton;
        private Button tabLoadButton;
        private Button closeButton;
        private ScrollView slotScrollView;
        private VisualElement slotContainer;

        private VisualElement confirmOverlay;
        private Label confirmLabel;
        private Button confirmYesButton;
        private Button confirmNoButton;

        private SaveLoadMode currentMode = SaveLoadMode.Save;
        private bool isOpen = false;
        private int pendingActionSlot = -1;
        private string pendingOverwriteName = null;

        private List<VisualElement> slotElements = new();
        private Dictionary<int, Texture2D> loadedThumbnails = new();

        private bool eventsRegistered = false;

        public bool IsOpen => isOpen;

        // ========== LIFECYCLE ==========

        private void OnEnable()
        {
            root = saveLoadDocument.rootVisualElement;
            QueryElements();
            RegisterEvents();

            overlay.style.display = DisplayStyle.None;
            SetConfirmVisible(false);
        }

        private void OnDisable()
        {
            UnregisterEvents();
            FreeThumbnails();
        }

        private void QueryElements()
        {
            overlay = root.Q<VisualElement>("save-load-overlay");
            modalPanel = root.Q<VisualElement>("modal-panel");
            titleLabel = root.Q<Label>("title-label");
            tabSaveButton = root.Q<Button>("tab-save");
            tabLoadButton = root.Q<Button>("tab-load");
            closeButton = root.Q<Button>("close-button");
            slotScrollView = root.Q<ScrollView>("slot-scroll-view");
            slotContainer = root.Q<VisualElement>("slot-container");
            confirmOverlay = root.Q<VisualElement>("confirm-overlay");
            confirmLabel = root.Q<Label>("confirm-label");
            confirmYesButton = root.Q<Button>("confirm-yes");
            confirmNoButton = root.Q<Button>("confirm-no");
        }

        // ========== EVENT REGISTRATION (named methods) ==========

        // ========== EVENT REGISTRATION ==========

        private void RegisterEvents()
        {
            if (eventsRegistered) return;
            eventsRegistered = true;

            // ROOT-LEVEL capture: fires BEFORE any child element can
            // intercept. Uses worldBound hit-testing so it doesn't matter
            // what's visually on top of the tab buttons.
            root.RegisterCallback<PointerDownEvent>(
                OnRootPointerDown, TrickleDown.TrickleDown);

            root.RegisterCallback<KeyDownEvent>(OnKeyDown);

            confirmYesButton.RegisterCallback<ClickEvent>(OnConfirmYesClicked);
            confirmNoButton.RegisterCallback<ClickEvent>(OnConfirmNoClicked);

            if (SaveManager.Instance != null)
            {
                SaveManager.Instance.OnSaveCompleted += OnSaveEvent;
                SaveManager.Instance.OnSlotDeleted += OnSaveEvent;
            }
        }

        private void UnregisterEvents()
        {
            if (!eventsRegistered) return;
            eventsRegistered = false;

            root.UnregisterCallback<PointerDownEvent>(
                OnRootPointerDown, TrickleDown.TrickleDown);
            root.UnregisterCallback<KeyDownEvent>(OnKeyDown);
            confirmYesButton.UnregisterCallback<ClickEvent>(OnConfirmYesClicked);
            confirmNoButton.UnregisterCallback<ClickEvent>(OnConfirmNoClicked);

            if (SaveManager.Instance != null)
            {
                SaveManager.Instance.OnSaveCompleted -= OnSaveEvent;
                SaveManager.Instance.OnSlotDeleted -= OnSaveEvent;
            }
        }
        // ========== ROOT-LEVEL POINTER HANDLER ==========

        /// <summary>
        /// Catches ALL pointer-down events at the root during the capture
        /// (TrickleDown) phase — before any child element can intercept.
        /// Checks worldBound coordinates to determine what was clicked.
        /// Only handles tabs, close button, and overlay-background-close.
        /// Everything else (slots, scroll, confirm buttons) falls through
        /// to their own handlers normally.
        /// </summary>
        private void OnRootPointerDown(PointerDownEvent evt)
        {
            if (!isOpen) return;

            Vector2 pos = new Vector2(evt.position.x, evt.position.y);

            // --- Tab buttons (coordinate-based, bypasses any blocker) ---
            if (tabSaveButton.worldBound.Contains(pos))
            {
                Debug.Log("[SaveLoadWindow] SAVE tab clicked (root capture)");
                evt.StopImmediatePropagation();
                SwitchMode(SaveLoadMode.Save);
                return;
            }

            if (tabLoadButton.worldBound.Contains(pos))
            {
                Debug.Log("[SaveLoadWindow] LOAD tab clicked (root capture)");
                evt.StopImmediatePropagation();
                SwitchMode(SaveLoadMode.Load);
                return;
            }

            // --- Close button ---
            if (closeButton.worldBound.Contains(pos))
            {
                evt.StopImmediatePropagation();
                Close();
                return;
            }

            // --- Click outside modal = close ---
            if (!modalPanel.worldBound.Contains(pos))
            {
                Close();
                return;
            }
        }

    // --- Everything else (slots, scrollview, confirm buttons)
    //     falls through — event propagates normally to children.


            // ========== EVENT HANDLERS ==========

        
        private void OnKeyDown(KeyDownEvent evt)
        {
            if (evt.keyCode == KeyCode.Escape && isOpen)
            {
                Close();
                evt.StopPropagation();
            }
        }

        private void OnConfirmYesClicked(ClickEvent evt)
        {
            evt.StopPropagation();
            ConfirmYes();
        }

        private void OnConfirmNoClicked(ClickEvent evt)
        {
            evt.StopPropagation();
            ConfirmNo();
        }

        private void OnSaveEvent(int _)
        {
            if (isOpen) RefreshSlotsSafe();
        }

        // ========== PUBLIC API ==========

        public void Open(SaveLoadMode mode)
        {
            Debug.Log($"[SaveLoadWindow] Open → {mode}");

            currentMode = mode;
            isOpen = true;
            pendingActionSlot = -1;
            pendingOverwriteName = null;

            overlay.style.display = DisplayStyle.Flex;
            SetConfirmVisible(false);
            EnsureTabsClickable();
            UpdateTabVisuals();
            RefreshSlotsSafe();

            root.Focus();
        }

        public void Close()
        {
            Debug.Log("[SaveLoadWindow] Close");

            isOpen = false;
            pendingActionSlot = -1;
            pendingOverwriteName = null;

            overlay.style.display = DisplayStyle.None;
            SetConfirmVisible(false);
            FreeThumbnails();
        }

        // ========== TAB SWITCHING ==========

        private void SwitchMode(SaveLoadMode mode)
        {
            Debug.Log($"[SaveLoadWindow] SwitchMode → {mode}");

            currentMode = mode;
            pendingActionSlot = -1;
            pendingOverwriteName = null;

            SetConfirmVisible(false);
            UpdateTabVisuals();
            RefreshSlotsSafe();
            EnsureTabsClickable();
        }

        private void UpdateTabVisuals()
        {
            titleLabel.text = currentMode == SaveLoadMode.Save
                ? "SAVE GAME" : "LOAD GAME";

            bool isSave = currentMode == SaveLoadMode.Save;

            tabSaveButton.EnableInClassList("tab-active", isSave);
            tabSaveButton.EnableInClassList("tab-inactive", !isSave);
            tabLoadButton.EnableInClassList("tab-active", !isSave);
            tabLoadButton.EnableInClassList("tab-inactive", isSave);
        }

        /// <summary>
        /// Belt-and-suspenders: force both tabs to stay enabled
        /// and pickable no matter what else happened.
        /// </summary>
        private void EnsureTabsClickable()
        {
            tabSaveButton.SetEnabled(true);
            tabLoadButton.SetEnabled(true);
            tabSaveButton.pickingMode = PickingMode.Position;
            tabLoadButton.pickingMode = PickingMode.Position;
            tabSaveButton.style.display = DisplayStyle.Flex;
            tabLoadButton.style.display = DisplayStyle.Flex;
        }

        // ========== CONFIRMATION VISIBILITY ==========

        private void SetConfirmVisible(bool visible)
        {
            confirmOverlay.style.display = visible
                ? DisplayStyle.Flex
                : DisplayStyle.None;

            // Propagate pickingMode to ALL children so nothing
            // silently swallows clicks when hidden.
            var mode = visible
                ? PickingMode.Position
                : PickingMode.Ignore;

            confirmOverlay.pickingMode = mode;
            confirmYesButton.pickingMode = mode;
            confirmNoButton.pickingMode = mode;

            // Re-enable confirm buttons when showing
            if (visible)
            {
                confirmYesButton.SetEnabled(true);
                confirmNoButton.SetEnabled(true);
            }
        }

        // ========== SLOT RENDERING ==========

        private void RefreshSlotsSafe()
        {
            try { RefreshSlots(); }
            catch (System.Exception e)
            {
                Debug.LogError($"[SaveLoadWindow] RefreshSlots failed: {e}");
            }
        }

        private void RefreshSlots()
        {
            slotContainer.Clear();
            slotElements.Clear();
            FreeThumbnails();

            if (SaveManager.Instance == null)
            {
                Debug.LogError("[SaveLoadWindow] SaveManager.Instance is null!");
                return;
            }

            SaveMetadata[] allMeta = SaveManager.Instance.GetAllMetadata();

            for (int i = 0; i < SaveManager.MAX_SLOTS; i++)
            {
                VisualElement slotRoot = saveSlotTemplate.Instantiate();
                slotContainer.Add(slotRoot);
                slotElements.Add(slotRoot);
                BindSlot(slotRoot, i, allMeta[i]);
            }
        }

        private void BindSlot(VisualElement slotRoot, int slotIndex,
                              SaveMetadata metadata)
        {
            Label indexLabel = slotRoot.Q<Label>("slot-index-label");
            VisualElement thumbImage = slotRoot.Q<VisualElement>("thumbnail-image");
            TextField nameField = slotRoot.Q<TextField>("save-name-field");
            Label tsLabel = slotRoot.Q<Label>("timestamp-label");
            Button actionBtn = slotRoot.Q<Button>("action-button");
            Button deleteBtn = slotRoot.Q<Button>("delete-button");
            VisualElement emptyOverlay = slotRoot.Q<VisualElement>("empty-slot-overlay");

            indexLabel.text = $"SLOT {slotIndex + 1}";
            bool isEmpty = (metadata == null);

            if (isEmpty)
            {
                emptyOverlay.style.display = DisplayStyle.Flex;
                thumbImage.style.display = DisplayStyle.None;
                nameField.style.display = DisplayStyle.None;
                tsLabel.style.display = DisplayStyle.None;
                deleteBtn.style.display = DisplayStyle.None;

                if (currentMode == SaveLoadMode.Save)
                {
                    actionBtn.text = "NEW SAVE";
                    actionBtn.SetEnabled(true);
                    int idx = slotIndex;
                    actionBtn.clicked += () =>
                        SaveManager.Instance.SaveToSlot(idx, $"Save {idx + 1}");
                }
                else
                {
                    actionBtn.text = "EMPTY";
                    actionBtn.SetEnabled(false);
                }
            }
            else
            {
                emptyOverlay.style.display = DisplayStyle.None;
                thumbImage.style.display = DisplayStyle.Flex;
                nameField.style.display = DisplayStyle.Flex;
                tsLabel.style.display = DisplayStyle.Flex;
                deleteBtn.style.display = DisplayStyle.Flex;

                // Thumbnail
                string thumbPath = SaveManager.Instance.GetThumbnailPath(slotIndex);
                if (!string.IsNullOrEmpty(thumbPath))
                {
                    Texture2D thumb =
                        SaveThumbnailCapture.LoadThumbnailFromDisk(thumbPath);
                    if (thumb != null)
                    {
                        thumbImage.style.backgroundImage =
                            new StyleBackground(thumb);
                        loadedThumbnails[slotIndex] = thumb;
                    }
                }

                nameField.value = metadata.saveName;
                nameField.isReadOnly = (currentMode == SaveLoadMode.Load);
                nameField.maxLength = 24;
                tsLabel.text = metadata.timestamp;

                if (currentMode == SaveLoadMode.Save)
                {
                    actionBtn.text = "OVERWRITE";
                    int idx = slotIndex;
                    actionBtn.clicked += () =>
                    {
                        pendingActionSlot = idx;
                        pendingOverwriteName = nameField.value;
                        confirmLabel.text = $"Overwrite Slot {idx + 1}?";
                        SetConfirmVisible(true);
                    };
                }
                else
                {
                    actionBtn.text = "LOAD";
                    int idx = slotIndex;
                    actionBtn.clicked += () =>
                    {
                        SaveManager.Instance.LoadFromSlot(idx);
                        Close();
                    };
                }
                actionBtn.SetEnabled(true);

                int delIdx = slotIndex;
                deleteBtn.clicked += () =>
                {
                    pendingActionSlot = delIdx;
                    pendingOverwriteName = null;
                    confirmLabel.text = $"Delete Slot {delIdx + 1}?";
                    SetConfirmVisible(true);
                };
            }
        }

        // ========== CONFIRMATION ==========

        private void ConfirmYes()
        {
            if (pendingActionSlot < 0) return;

            if (pendingOverwriteName != null)
                SaveManager.Instance.SaveToSlot(pendingActionSlot,
                                                pendingOverwriteName);
            else
                SaveManager.Instance.DeleteSlot(pendingActionSlot);

            SetConfirmVisible(false);
            pendingActionSlot = -1;
            pendingOverwriteName = null;
        }

        private void ConfirmNo()
        {
            SetConfirmVisible(false);
            pendingActionSlot = -1;
            pendingOverwriteName = null;
        }

        // ========== CLEANUP ==========

        private void FreeThumbnails()
        {
            foreach (var kvp in loadedThumbnails)
                if (kvp.Value != null) Destroy(kvp.Value);
            loadedThumbnails.Clear();
        }
    }
}
