using UnityEngine;

public class CellIndicatorController : MonoBehaviour
{
    [SerializeField] private GameObject indicatorQuad;
    [SerializeField] private PlacementGrid grid;
    [SerializeField] private float yOffset = 0.1f;
    Vector2 lastPos = Vector2.zero;

    public void ShowAtCell(Vector2Int cell)
    {
        if (lastPos != cell)
        {
            PlayCellChangeSoundEffect();
        }
        Vector3 cellOffset = new Vector3(0, yOffset, 0); // Set the Y position to the desired offset
        indicatorQuad.transform.position = grid.GetCellCenter(cell) + cellOffset;
        indicatorQuad.SetActive(true);
        
        lastPos = cell;
    }

    public void Hide()
    {   
        indicatorQuad.SetActive(false);
    }
    private void PlayCellChangeSoundEffect()
    {
        Debug.Log("Playing cell change sound effect");
        AudioManager.Play("ValidPlace");
    }
}