using UnityEngine;
using UnityEngine.UI;

public class UIDim : MonoBehaviour
{
    [SerializeField] private Image dimImage;

    public void ShowDim()
    {
        gameObject.SetActive(true);
        dimImage.CrossFadeAlpha(0.5f, 0.25f, false); // 0.25�� ���� ������ ��ο���
    }

    public void HideDim()
    {
        dimImage.CrossFadeAlpha(0f, 0.25f, false);
        Invoke(nameof(Disable), 0.3f); // �ִ� ������ ��Ȱ��ȭ
    }

    void Disable()
    {
        gameObject.SetActive(false);
    }
}

