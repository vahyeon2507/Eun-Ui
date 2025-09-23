using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class UIButtonEffect : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private Button button;
    private Vector3 originalScale;

    void Awake()
    {
        button = GetComponent<Button>();
        originalScale = transform.localScale;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        transform.localScale = originalScale * 1.1f;
        // 색 바꾸고 싶으면 button.image.color = Color.yellow; 같은 식으로
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        transform.localScale = originalScale;
        // 색 되돌리기
    }
}

