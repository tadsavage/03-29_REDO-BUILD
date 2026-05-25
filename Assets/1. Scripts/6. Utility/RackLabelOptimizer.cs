using UnityEngine;

public class RackLabelOptimizer : MonoBehaviour
{
    [SerializeField] private float highDetailDistance = 25f;
    [SerializeField] private float updateInterval = 0.2f;

    private float _timer;
    private Transform _transform;

    private void Awake()
    {
        _transform = transform;
    }

    private void Update()
    {
        _timer += Time.deltaTime;
        if (_timer >= updateInterval)
        {
            _timer = 0f;
            OptimizeLabels();
        }
    }

    private void OptimizeLabels()
    {
        var labels = RackLabelDisplay.AllLabels;
        Vector3 camPos = _transform.position;
        float distSq = highDetailDistance * highDetailDistance;

        for (int i = 0; i < labels.Count; i++)
        {
            if (labels[i] == null) continue;
            
            float dSq = (labels[i].transform.position - camPos).sqrMagnitude;
            labels[i].SetDetailLevel(dSq <= distSq);
        }
    }
}
