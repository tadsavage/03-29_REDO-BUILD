using UnityEngine;

namespace GameCore.Inventory
{
    /// <summary>
    /// Inspector-assignable ScriptableObject holding the hover/click sound effect clips for the
    /// VENDORS tab, so clips can be swapped without touching code.
    /// </summary>
    [CreateAssetMenu(fileName = "VendorUiSfx", menuName = "Warehouse/UI/Vendor UI SFX Config")]
    public class VendorUiSfxConfig : ScriptableObject
    {
        [SerializeField] private AudioClip rowHoverClip;
        [SerializeField] private AudioClip orderButtonHoverClip;
        [SerializeField] private AudioClip orderButtonClickClip;
        [SerializeField, Range(0f, 1f)] private float volume = 0.7f;

        public AudioClip RowHoverClip => rowHoverClip;
        public AudioClip OrderButtonHoverClip => orderButtonHoverClip;
        public AudioClip OrderButtonClickClip => orderButtonClickClip;
        public float Volume => volume;
    }
}
