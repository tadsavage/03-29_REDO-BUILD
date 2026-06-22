using UnityEngine;
using UnityEngine.UIElements;
using SaveLoadSystem;

/// <summary>
/// Persists a DraggableWindow's position to PlayerPrefs under "{prefix}_X"/"{prefix}_Y" —
/// restored as soon as it's constructed, saved immediately when a drag ends, and again
/// whenever the game saves/quicksaves. Mirrors DevHudWindow's hand-rolled position
/// persistence so every draggable panel behaves the same way.
///
/// Call Tick() once per frame from the owning MonoBehaviour's Update() to keep retrying
/// the SaveManager subscription until it exists (SaveManager.Instance may not be ready yet
/// when the panel's OnEnable runs).
/// </summary>
public class DraggableWindowPersistence
{
    private readonly VisualElement _panel;
    private readonly string _keyX, _keyY;
    private bool _subscribed;

    public DraggableWindowPersistence(VisualElement panel, DraggableWindow dragger, string prefsKeyPrefix)
    {
        _panel = panel;
        _keyX = prefsKeyPrefix + "_X";
        _keyY = prefsKeyPrefix + "_Y";
        if (dragger != null) dragger.OnDragEnd += Save;
        Restore();
    }

    public void Tick()
    {
        if (_subscribed || SaveManager.Instance == null) return;
        SaveManager.Instance.OnSaveCompleted += _ => Save();
        _subscribed = true;
    }

    public void Save()
    {
        if (_panel == null) return;
        var rs = _panel.resolvedStyle;
        // resolvedStyle.left/top are 0 before the panel has been laid out — guard against
        // writing zeros over a valid saved position.
        if (rs.left == 0f && rs.top == 0f) return;
        PlayerPrefs.SetFloat(_keyX, rs.left);
        PlayerPrefs.SetFloat(_keyY, rs.top);
        PlayerPrefs.Save();
    }

    private void Restore()
    {
        if (_panel == null || !PlayerPrefs.HasKey(_keyX)) return;
        _panel.style.position = Position.Absolute;
        _panel.style.left = PlayerPrefs.GetFloat(_keyX);
        _panel.style.top = PlayerPrefs.GetFloat(_keyY);
        _panel.style.right = StyleKeyword.Auto;
        _panel.style.bottom = StyleKeyword.Auto;
    }
}
