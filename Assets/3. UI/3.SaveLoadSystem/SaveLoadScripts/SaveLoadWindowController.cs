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

        private VisualElement root, overlay, modalPanel;
        private Label titleLabel;
        private Button tabSaveButton, tabLoadButton, closeButton;
        private ScrollView slotScrollView;
        private VisualElement slotContainer;
        private VisualElement confirmOverlay;
        private Label confirmLabel;
        private Button confirmYesButton, confirmNoButton;

        private SaveLoadMode currentMode = SaveLoadMode.Save;
        private bool isOpen = false;
        private int pendingActionSlot = -1;

        private List<VisualElement> slotElements = new List<VisualElement>();
        private Dictionary<int, Texture2D> loadedThumbnails = new Dictionary<int, Texture2D>();

        public bool IsOpen => isOpen;

        // ========== LIFECYCLE ==========

        private void OnEnable()
        {
            root = saveLoadDocument.rootVisualElement;

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

            tabSaveButton.RegisterCallback<ClickEvent>(evt =>
            {
                evt.StopPropagation();
                SwitchMode(SaveLoadMode.Save);
            });
            tabLoadButton.RegisterCallback<ClickEvent>(evt =>
            {
                evt.StopPropagation();
                SwitchMode(SaveLoadMode.Load);
            });
            closeButton.clicked += Close;
            confirmYesButton.clicked += OnConfirmYes;
            confirmNoButton.clicked += OnConfirmNo;

            overlay.RegisterCallback<ClickEvent>(evt =>
            {
                if (evt.target == overlay) Close();
            });

            root.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode == KeyCode.Escape && isOpen)
                {
                    Close();
                    evt.StopPropagation();
                }
            });

            if (SaveManager.Instance != null)
            {
                SaveManager.Instance.OnSaveCompleted += (_) => { if (isOpen) RefreshAllSlots(); };
                SaveManager.Instance.OnLoadCompleted += (_) => { /* window already closed */ };
                SaveManager.Instance.OnSlotDeleted += (_) => { if (isOpen) RefreshAllSlots(); };
            }

            overlay.style.display = DisplayStyle.None;
            confirmOverlay.style.display = DisplayStyle.None;
        }

        private void OnDisable() => FreeThumbnails();

        // ========== PUBLIC API ==========
        public void Open(SaveLoadMode mode)
        {
            Debug.Log($"[SaveLoadWindow] Open → {mode}");
            currentMode = mode;
            isOpen = true;
            pendingActionSlot = -1;
            overlay.style.display = DisplayStyle.Flex;
            confirmOverlay.style.display = DisplayStyle.None;
            UpdateTabVisuals();

            try
            {
                RefreshAllSlots();
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[SaveLoadWindow] RefreshAllSlots failed: {e}");
            }

            tabSaveButton.SetEnabled(true);
            tabLoadButton.SetEnabled(true);
            root.Focus();
        }

        public void Close()
        {
            isOpen = false;
            pendingActionSlot = -1;
            overlay.style.display = DisplayStyle.None;
            confirmOverlay.style.display = DisplayStyle.None;
            FreeThumbnails();
        }

        // ========== TAB SWITCHING ==========

        private void SwitchMode(SaveLoadMode mode)
        {
            Debug.Log($"[SaveLoadWindow] SwitchMode → {mode}");
            currentMode = mode;
            pendingActionSlot = -1;
            confirmOverlay.style.display = DisplayStyle.None;
            UpdateTabVisuals();

            try
            {
                RefreshAllSlots();
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[SaveLoadWindow] RefreshAllSlots failed: {e}");
            }

            // Guarantee tabs stay clickable no matter what
            tabSaveButton.SetEnabled(true);
            tabLoadButton.SetEnabled(true);
        }

        private void UpdateTabVisuals()
        {
            titleLabel.text = currentMode == SaveLoadMode.Save
                ? "SAVE GAME" : "LOAD GAME";

            tabSaveButton.EnableInClassList("tab-active", currentMode == SaveLoadMode.Save);
            tabSaveButton.EnableInClassList("tab-inactive", currentMode != SaveLoadMode.Save);
            tabLoadButton.EnableInClassList("tab-active", currentMode == SaveLoadMode.Load);
            tabLoadButton.EnableInClassList("tab-inactive", currentMode != SaveLoadMode.Load);
        }

        // ========== SLOT RENDERING ==========

        private void RefreshAllSlots()
        {
            slotContainer.Clear();
            slotElements.Clear();
            FreeThumbnails();

            if (SaveManager.Instance == null)
            {
                Debug.LogError("[SaveLoadWindow] SaveManager.Instance is null! " +
                               "Add a SaveManager component to your scene.");
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
                if (thumbPath != null)
                {
                    Texture2D thumb = SaveThumbnailCapture.LoadThumbnailFromDisk(thumbPath);
                    if (thumb != null)
                    {
                        thumbImage.style.backgroundImage = new StyleBackground(thumb);
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
                        confirmLabel.text = $"Overwrite Slot {idx + 1}?";
                        confirmOverlay.style.display = DisplayStyle.Flex;
                        confirmOverlay.userData = nameField.value;
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
                    confirmLabel.text = $"Delete Slot {delIdx + 1}?";
                    confirmOverlay.style.display = DisplayStyle.Flex;
                    confirmOverlay.userData = (string)null;
                };
            }
        }

        // ========== CONFIRMATION ==========

        private void OnConfirmYes()
        {
            if (pendingActionSlot < 0) return;
            string saveName = confirmOverlay.userData as string;

            if (saveName != null)
                SaveManager.Instance.SaveToSlot(pendingActionSlot, saveName);
            else
                SaveManager.Instance.DeleteSlot(pendingActionSlot);

            confirmOverlay.style.display = DisplayStyle.None;
            pendingActionSlot = -1;
        }

        private void OnConfirmNo()
        {
            confirmOverlay.style.display = DisplayStyle.None;
            pendingActionSlot = -1;
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
