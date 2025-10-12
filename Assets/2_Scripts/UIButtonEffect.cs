using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System.Collections;

public class UIButtonEffect : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    [Header("애니메이션 설정")]
    public float hoverScale = 1.1f;
    public float clickScale = 0.95f;
    public float animationSpeed = 8f;
    
    [Header("색상 효과")]
    public bool useColorEffect = false;
    public Color hoverColor = Color.yellow;
    
    private Button button;
    private Image buttonImage;
    private Vector3 originalScale;
    private Color originalColor;
    private bool isHovering = false;
    private bool isPressed = false;
    
    private Coroutine currentAnimation;

    void Awake()
    {
        Initialize();
    }

    void Start()
    {
        Initialize();
    }
    
    void Initialize()
    {
        if (button == null)
        {
            button = GetComponent<Button>();
            originalScale = transform.localScale;
        }
        
        if (buttonImage == null && useColorEffect)
        {
            buttonImage = GetComponent<Image>();
            if (buttonImage != null)
                originalColor = buttonImage.color;
        }
        
        // 버튼이 비활성화되어 있으면 효과 비활성화
        if (button != null && !button.interactable)
        {
            enabled = false;
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (button != null && !button.interactable) return;
        
        isHovering = true;
        AnimateToScale(originalScale * hoverScale);
        
        if (useColorEffect && buttonImage != null)
        {
            buttonImage.color = hoverColor;
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (button != null && !button.interactable) return;
        
        isHovering = false;
        if (!isPressed)
        {
            AnimateToScale(originalScale);
            
            if (useColorEffect && buttonImage != null)
            {
                buttonImage.color = originalColor;
            }
        }
    }
    
    public void OnPointerDown(PointerEventData eventData)
    {
        if (button != null && !button.interactable) return;
        
        isPressed = true;
        AnimateToScale(originalScale * clickScale);
    }
    
    public void OnPointerUp(PointerEventData eventData)
    {
        if (button != null && !button.interactable) return;
        
        isPressed = false;
        
        if (isHovering)
        {
            AnimateToScale(originalScale * hoverScale);
        }
        else
        {
            AnimateToScale(originalScale);
            
            if (useColorEffect && buttonImage != null)
            {
                buttonImage.color = originalColor;
            }
        }
    }
    
    void AnimateToScale(Vector3 targetScale)
    {
        if (currentAnimation != null)
        {
            StopCoroutine(currentAnimation);
        }
        
        currentAnimation = StartCoroutine(ScaleAnimation(targetScale));
    }
    
    IEnumerator ScaleAnimation(Vector3 targetScale)
    {
        Vector3 startScale = transform.localScale;
        float elapsedTime = 0f;
        float duration = 1f / animationSpeed;
        
        while (elapsedTime < duration)
        {
            elapsedTime += Time.unscaledDeltaTime;
            float progress = elapsedTime / duration;
            
            // EaseOut 효과
            progress = 1f - Mathf.Pow(1f - progress, 3f);
            
            transform.localScale = Vector3.Lerp(startScale, targetScale, progress);
            yield return null;
        }
        
        transform.localScale = targetScale;
        currentAnimation = null;
    }
    
    void OnDisable()
    {
        // 비활성화될 때 원래 상태로 복원
        if (currentAnimation != null)
        {
            StopCoroutine(currentAnimation);
            currentAnimation = null;
        }
        
        transform.localScale = originalScale;
        
        if (useColorEffect && buttonImage != null)
        {
            buttonImage.color = originalColor;
        }
        
        isHovering = false;
        isPressed = false;
    }
    
    [ContextMenu("효과 테스트")]
    public void TestEffect()
    {
        OnPointerEnter(null);
        Invoke(nameof(TestEffectEnd), 1f);
    }
    
    void TestEffectEnd()
    {
        OnPointerExit(null);
    }
}