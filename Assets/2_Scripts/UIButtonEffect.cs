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
        // �� �ٲٰ� ������ button.image.color = Color.yellow; ���� ������
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        transform.localScale = originalScale;
        // �� �ǵ�����
    }
}

