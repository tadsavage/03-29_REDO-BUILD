using UnityEngine;

public class BuildingData : MonoBehaviour
{
    [SerializeField] private ObjDataSO objDataSO;

    public ObjDataSO Data => objDataSO;

    public void Delete()
    {
        Destroy(gameObject);
    }
}