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

    public static void Show(string msg, float duration = 1.5f)
    {
        if (toast == null)
            return;
        Debug.Log($"[UIToast] {msg} SHOULD BE SEEN?");
        toast.text = msg;
        toast.style.opacity = 1;
        timer = duration;
    }
}