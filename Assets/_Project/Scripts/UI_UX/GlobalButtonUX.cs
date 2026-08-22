using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

/// <summary>
/// Applies one shared house behaviour to EVERY UI Toolkit <see cref="Button"/> in the game,
/// instead of leaving each panel to wire (or forget to wire) its own click feedback:
///
///  • Plays <see cref="clickSfx"/> (click_004) on click.
///  • Smoothly transitions the button's background to <see cref="highlightColor"/>, and keeps
///    it highlighted until the button loses focus (clicking elsewhere, tabbing away, or the
///    panel closing) rather than the ordinary momentary hover/active flash a Button gets for free.
///
/// Hooked in once per <see cref="UIDocument"/> root rather than per Button: UI Toolkit's
/// Click/FocusOut events bubble up through the visual tree regardless of when a descendant
/// Button was added, so registering on the root also catches buttons a panel builds later at
/// runtime (Shift Manager, Contracts, Work Queue, New Item, and Purchasing are all built as
/// children of TopBar's root well after this script's Awake runs).
///
/// Also re-scans on every scene load, so panels created after a scene change are covered too.
/// </summary>
public class GlobalButtonUX : MonoBehaviour
{
    [SerializeField] private AudioClip clickSfx;
    [SerializeField, Range(0f, 1f)] private float clickVolume = 1f;

    [Tooltip("Background color a button transitions to on click, and holds until it loses focus.")]
    [SerializeField] private Color highlightColor = new Color(0.35f, 0.55f, 0.95f, 0.55f);

    [Tooltip("Seconds the background color takes to transition to/from the highlight.")]
    [SerializeField] private float transitionSeconds = 0.12f;

    private AudioSource _sfxSource;

    // Remembers each currently-highlighted button's ORIGINAL background so it can be restored
    // exactly on blur, regardless of whatever color that particular button/panel already used.
    private readonly Dictionary<Button, StyleColor> _originalBackgrounds = new();

    // Roots already wired, so a re-scan (e.g. after a scene load) never double-registers the
    // same document.
    private readonly HashSet<VisualElement> _hookedRoots = new();

    private void Awake()
    {
        _sfxSource = gameObject.AddComponent<AudioSource>();
        _sfxSource.playOnAwake = false;

        HookAllDocuments();
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode) => HookAllDocuments();

    private void HookAllDocuments()
    {
        foreach (var doc in FindObjectsByType<UIDocument>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            HookRoot(doc.rootVisualElement);
    }

    private void HookRoot(VisualElement root)
    {
        if (root == null || !_hookedRoots.Add(root)) return;

        // TrickleDown so a Button under a title bar that captures/stops pointer events for
        // dragging (several panels do this) still sees the click on the way down.
        root.RegisterCallback<ClickEvent>(OnAnyButtonClicked, TrickleDown.TrickleDown);
        root.RegisterCallback<FocusOutEvent>(OnAnyButtonBlurred, TrickleDown.TrickleDown);
    }

    private void OnAnyButtonClicked(ClickEvent evt)
    {
        if (evt.target is not Button button) return;

        if (clickSfx != null) _sfxSource.PlayOneShot(clickSfx, clickVolume);

        ApplyTransition(button);
        if (!_originalBackgrounds.ContainsKey(button))
            _originalBackgrounds[button] = button.style.backgroundColor;

        button.style.backgroundColor = new StyleColor(highlightColor);
    }

    private void OnAnyButtonBlurred(FocusOutEvent evt)
    {
        if (evt.target is not Button button) return;
        if (!_originalBackgrounds.TryGetValue(button, out var original)) return;

        ApplyTransition(button);
        button.style.backgroundColor = original;
        _originalBackgrounds.Remove(button);
    }

    /// <summary>Makes the next background-color write animate instead of snap. Set every time
    /// rather than once, since some panels' Adopt/StyleSquare helpers reassign inline styles
    /// (including transition) after this script last touched the button.</summary>
    private void ApplyTransition(Button button)
    {
        button.style.transitionProperty = new List<StylePropertyName> { new StylePropertyName("background-color") };
        button.style.transitionDuration = new List<TimeValue> { new TimeValue(transitionSeconds, TimeUnit.Second) };
    }
}
