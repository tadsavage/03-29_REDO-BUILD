using UnityEngine;

[ExecuteInEditMode]
[RequireComponent(typeof(LineRenderer))]
public class IRSensorVisual : MonoBehaviour
{
    private LineRenderer _lineRenderer;
    public float maxDistance = 20f;
    public float beamWidth = 0.01f;
    public LayerMask collisionLayers = ~0;
    public float verticalOffset = 0.1f;
    public float tiltAngle = 35f;

    private void Awake()
    {
        Initialize();
    }

    private void OnEnable()
    {
        Initialize();
    }

    private void Initialize()
    {
        _lineRenderer = GetComponent<LineRenderer>();
        if (_lineRenderer != null)
        {
            _lineRenderer.useWorldSpace = true;
            _lineRenderer.positionCount = 2;
            _lineRenderer.startWidth = beamWidth;
            _lineRenderer.endWidth = beamWidth;
            
            // Set color to red
            _lineRenderer.startColor = Color.red;
            _lineRenderer.endColor = Color.red;
        }
    }

    private void Update()
    {
        if (_lineRenderer == null) return;

        Vector3 origin = transform.position;
        float zSign = 1f;
        
        MeshFilter mf = GetComponent<MeshFilter>();
        if (mf != null && mf.sharedMesh != null)
        {
            Vector3 center = mf.sharedMesh.bounds.center;
            Vector3 extents = mf.sharedMesh.bounds.extents;
            
            // Use bounds center as base world position
            origin = transform.TransformPoint(center);
            
            // Offset to the outer face along the Z axis (depth) and apply vertical offset
            // In this prefab, IR1 is at Z=0.8 and IR2 at Z=-0.8.
            // The red circles are on the outer faces (+Z for IR1, -Z for IR2).
            zSign = Mathf.Sign(center.z);
            float zOffset = zSign * extents.z;
            origin += transform.TransformDirection(new Vector3(0, -verticalOffset, zOffset));
            }

        // Shoot at an angle away from the center
        // We tilt the local down vector by the tiltAngle around the local right axis,
        // using the zSign to determine if we tilt forward or backward.
        // Flipped the sign as per user request (angles were backwards).
        Vector3 direction = Quaternion.AngleAxis(-tiltAngle * zSign, transform.right) * (-transform.up);

        Ray ray = new Ray(origin, direction);
        Vector3 endPoint;

        if (Physics.Raycast(ray, out RaycastHit hit, maxDistance, collisionLayers, QueryTriggerInteraction.Ignore))
        {
            endPoint = hit.point;
        }
        else
        {
            endPoint = origin + direction * maxDistance;
        }

        _lineRenderer.SetPosition(0, origin);
        _lineRenderer.SetPosition(1, endPoint);
        
        // Ensure color is red
        _lineRenderer.startColor = Color.red;
        _lineRenderer.endColor = Color.red;
        
        _lineRenderer.startWidth = beamWidth;
        _lineRenderer.endWidth = beamWidth;
    }
}
