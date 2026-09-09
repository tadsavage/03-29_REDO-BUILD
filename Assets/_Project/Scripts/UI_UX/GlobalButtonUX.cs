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

        // The documents hooked above can OUTLIVE this component — TopBar's UIDocument is
        // DontDestroyOnLoad, this GameObject is not — and a callback left pointing at a destroyed
        // instance keeps being invoked, with a destroyed AudioSource behind it. That is what broke
        // every close button in the game; see OnAnyButtonClicked.
        foreach (var root in _hookedRoots)
        {
            if (root == null) continue;
            root.UnregisterCallback<ClickEvent>(OnAnyButtonClicked, TrickleDown.TrickleDown);
            root.UnregisterCallback<FocusOutEvent>(OnAnyButtonBlurred, TrickleDown.TrickleDown);
            root.UnregisterCallback<MouseOverEvent>(OnAnyButtonHoverEnter);
            root.UnregisterCallback<MouseOutEvent>(OnAnyButtonHoverLeave);
        }
        _hookedRoots.Clear();
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

        // Select cursor over every Button in the game, the same "one place" reasoning as the rest of
        // this class — per Tad's explicit call. MouseEnterEvent/MouseLeaveEvent were tried first and
        // silently never fired here: those two don't propagate at all in UI Toolkit (no bubble, no
        // trickle-down capture either — they only ever reach a handler registered directly on the
        // element itself), so a ROOT registration for them is a no-op no matter which phase you ask
        // for. MouseOverEvent/MouseOutEvent are the bubbling counterparts (same relationship as DOM's
        // mouseenter/mouseleave vs. mouseover/mouseout) and correctly reach a root-level handler on
        // their way up from whatever was actually entered/left, registered normally (bubble phase).
        root.RegisterCallback<MouseOverEvent>(OnAnyButtonHoverEnter);
        root.RegisterCallback<MouseOutEvent>(OnAnyButtonHoverLeave);
    }

    private void OnAnyButtonHoverEnter(MouseOverEvent evt)
    {
        if (evt.target is Button) CustomCursorService.SetHoveringInteractable(true);
    }

    private void OnAnyButtonHoverLeave(MouseOutEvent evt)
    {
        if (evt.target is Button) CustomCursorService.SetHoveringInteractable(false);
    }

    /// <summary>
    /// EVERYTHING in here is decoration, so it is wrapped: an exception thrown from a UI Toolkit
    /// callback aborts the REST of that event's propagation path, and this callback is registered
    /// TrickleDown on the document ROOT — meaning it runs before every panel's own handler. A throw
    /// here therefore silently eats the click for every button in the game.
    ///
    /// That is not hypothetical. A stale AudioSource reference (see PlayClick) made this line throw
    /// on every click, and the visible symptom was the close buttons on panels 2/3/4 doing nothing —
    /// nothing about the sound, and nothing in the console tying it to those panels. A cosmetic
    /// feature must never be able to swallow input, whatever goes wrong inside it.
    /// </summary>
    private void OnAnyButtonClicked(ClickEvent evt)
    {
        if (evt.target is not Button button) return;

        try
        {
            PlayClick();

            ApplyTransition(button);
            if (!_originalBackgrounds.ContainsKey(button))
                _originalBackgrounds[button] = button.style.backgroundColor;

            button.style.backgroundColor = new StyleColor(highlightColor);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[GlobalButtonUX] Click decoration failed on '{button.name}' — the click " +
                             $"itself still went through. {e.GetType().Name}: {e.Message}");
        }
    }

    /// <summary>
    /// Plays the click, re-acquiring the AudioSource if it has gone away.
    ///
    /// The old code null-checked the CLIP but not the SOURCE. `_sfxSource` is a component on this
    /// GameObject, so it dies with it — and a callback registered on a surviving document root can
    /// still reach this method afterwards, at which point the field is a destroyed-object reference
    /// that throws on use rather than a plain null.
    /// </summary>
    private void PlayClick()
    {
        if (clickSfx == null) return;

        // `== null` on a UnityEngine.Object is destroyed-aware, which a plain null check is not.
        if (_sfxSource == null)
        {
            _sfxSource = gameObject.AddComponent<AudioSource>();
            _sfxSource.playOnAwake = false;
        }

        _sfxSource.PlayOneShot(clickSfx, clickVolume * AudioManager.GameVolumeLevel);
    }

    private void OnAnyButtonBlurred(FocusOutEvent evt)
    {
        if (evt.target is not Button button) return;
        if (!_originalBackgrounds.TryGetValue(button, out var original)) return;

        // Guarded for the same reason as the click handler — this one runs on the way to whatever
        // else cares about focus leaving.
        try
        {
            ApplyTransition(button);
            button.style.backgroundColor = original;
            _originalBackgrounds.Remove(button);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[GlobalButtonUX] Blur restore failed on '{button.name}'. " +
                             $"{e.GetType().Name}: {e.Message}");
        }
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
