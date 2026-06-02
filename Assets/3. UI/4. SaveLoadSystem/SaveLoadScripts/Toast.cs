using UnityEngine;
using UnityEngine.UIElements;

public class UIToast : MonoBehaviour
{
    private static Label toast;
    private static float timer;

    private void Awake()
    {
        var doc = GetComponent<UIDocument>();
        toast = doc.rootVisualElement.Q<Label>("ToastLabel");
        toast.style.opacity = 0;
    }

    private void Update()
    {
        if (timer > 0f)
        {
            timer -= Time.deltaTime;
            if (timer <= 0f)
                toast.style.opacity = 0;
        }
    }

    private void OnDestroy()
    {
        // Clear static reference when the component is destroyed (e.g. on Stop)
        // to prevent accessing destroyed UI elements in subsequent frames or runs.
        if (toast != null)
        {
            toast = null;
        }
    }

    public static void Show(string msg, float duration = 1.5f)
    {
        // Check if toast is assigned and still has a valid panel (is not destroyed)
        if (toast == null || toast.panel == null)
            return;

        toast.text = msg;
        toast.style.opacity = 1;
        timer = duration;
    }
}