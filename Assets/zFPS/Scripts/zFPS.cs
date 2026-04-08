namespace Assets.DigitalSoftware.zFPS.Scripts
{
    using UnityEditor;
    using UnityEngine;
    using UnityEngine.UI;

    public class zFPS : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField, Tooltip("The interval for calculating the fps"), Range(0.0f, 2.0f)] private float _samplingIntervalInSeconds = 0.25f;

        // fps
        private int _countFrames = 0;
        private float _lastMeasureAt = 0.0f;
        const string displayFPS = "{0} FPS";
        [Header("Internal references")]
        [SerializeField] private Text _FPSMeasureText;


        // local cache
        private string _cachedCurrentDisplayStringFps;

        private void Start()
        {
            _lastMeasureAt = Time.realtimeSinceStartup;
        }

        // once per frame
        private void Update()
        {
            // grab time
            var currentTime = Time.realtimeSinceStartup;

            _countFrames++;

            float fpsMeasurePeriod = currentTime - _lastMeasureAt;

            // see if time has passed (less fluctuation) to show short-period average - while still be flexible with regards to Tick 
            if (fpsMeasurePeriod > _samplingIntervalInSeconds)
            {
                var currentFps = _countFrames / fpsMeasurePeriod; // time since last compared, using frames done since, get a nice average

                string localCachedCurrentDisplayString = string.Format(displayFPS, (int)currentFps);

                // if info differs from previous, push it to Text object
                if (localCachedCurrentDisplayString != _cachedCurrentDisplayStringFps)
                {
                    _cachedCurrentDisplayStringFps = localCachedCurrentDisplayString;
                    _FPSMeasureText.text = _cachedCurrentDisplayStringFps;
                }

                _countFrames = 0;
                _lastMeasureAt = currentTime;
            }
        
        }
    }
}
