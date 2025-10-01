using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class UIButtonEffect : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
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
        // 버튼 호버 사운드 재생
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayMenuOpen();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        transform.localScale = originalScale;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        // 버튼 클릭 사운드 초고속 재생 (지연 최소화)
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayUISFXInstant(AudioManager.Instance.buttonClickSFX);
    }
}