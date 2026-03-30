using UnityEngine;

public class FluorescentFlicker : MonoBehaviour
{
    public Light targetLight;
    public float minIntensity = 0.6f;
    public float maxIntensity = 1.2f;

    private float baseIntensity;

    void Start()
    {
        if (targetLight == null)
            targetLight = GetComponent<Light>();

        baseIntensity = targetLight.intensity;
        StartCoroutine(FlickerRoutine());
    }

    private System.Collections.IEnumerator FlickerRoutine()
    {
        while (true)
        {
            // Random chance to flicker
            if (Random.value < 0.05f)
            {
                // Quick dip
                targetLight.intensity = Random.Range(minIntensity, maxIntensity);
                yield return new WaitForSeconds(Random.Range(0.02f, 0.08f));

                // Pop back
                targetLight.intensity = baseIntensity;
            }

            yield return new WaitForSeconds(Random.Range(0.1f, 0.5f));
        }
    }
}
