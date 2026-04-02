using UnityEngine;

public class PreviewController : MonoBehaviour
{
    private GameObject _currentPreview;


    //show the preview of the object being placed
    public void Show(ObjDataSO data)
    {
        if (_currentPreview != null)
            Destroy(_currentPreview);

        _currentPreview = Instantiate(data.prefab);
        _currentPreview.SetActive(true);
    }

    // Move the preview to the current mouse position
    public void MoveTo(Vector3 worldPos)
    {
        if (_currentPreview != null)
            _currentPreview.transform.position = worldPos;
    }
    public void UpdateGhostPosition(Vector3 snappedPos)
    {
        // Move ghost
    }
    // Hide the preview when placement is finalized or cancelled
    public void Hide()
    {
        if (_currentPreview != null)
            _currentPreview.SetActive(false);
    }
    public void Rotate(float angle)
    {
        if (_currentPreview != null)
            _currentPreview.transform.rotation = Quaternion.Euler(0f, angle, 0f);
        AudioManager.Play("Rotate");
    }
}
