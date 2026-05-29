using UnityEngine;
using System.Collections;

public class BuildingDestructionEffect : MonoBehaviour
{
    private Vector3 _originalPos;
    private float _timer;
    
    private float _duration;
    private float _sinkAmount;
    private float _vibrationAmount;
    private float _vibrationSpeed;

    private bool _isComplete;

    public void Initialize(float duration, float sinkAmount, float vibrationAmount, float vibrationSpeed)
    {
        _duration = duration;
        _sinkAmount = sinkAmount;
        _vibrationAmount = vibrationAmount;
        _vibrationSpeed = vibrationSpeed;
        
        _originalPos = transform.position;
        _timer = 0f;
        _isComplete = false;

        // Play dust FX at start
        if (FXPool.Instance != null)
        {
            FXPool.Instance.Play("dust", transform.position);
        }
    }

    private void Update()
    {
        if (_isComplete) return;

        _timer += Time.deltaTime;
        float normalizedTime = Mathf.Clamp01(_timer / _duration);

        // Sink effect
        Vector3 sinkOffset = Vector3.down * (_sinkAmount * normalizedTime);
        
        // Vibration effect (Sims 3 style)
        // Using sine waves with different frequencies for X and Z to make it look jittery
        float jitterX = Mathf.Sin(Time.time * _vibrationSpeed) * _vibrationAmount;
        float jitterZ = Mathf.Cos(Time.time * _vibrationSpeed * 1.1f) * _vibrationAmount;
        
        // Decay vibration as it sinks? Or keep it constant. The request said "vibrate a little".
        // Let's keep it constant but slightly fade it out at the very end.
        float fade = 1.0f - Mathf.Pow(normalizedTime, 4); 
        Vector3 vibrationOffset = new Vector3(jitterX, 0, jitterZ) * fade;

        transform.position = _originalPos + sinkOffset + vibrationOffset;

        if (_timer >= _duration)
        {
            Complete();
        }
    }

    private void Complete()
    {
        _isComplete = true;
        gameObject.SetActive(false);
        // The component will be destroyed when the object is re-enabled/undone or just cleaned up
    }

    public void Abort()
    {
        _isComplete = true;
        transform.position = _originalPos;
        Destroy(this);
    }
}
