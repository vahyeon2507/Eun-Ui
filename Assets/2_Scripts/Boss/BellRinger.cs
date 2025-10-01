using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class BellRinger : MonoBehaviour
{
    [Header("Animator")]
    public Animator animator;
    [Tooltip("���� �︮�� Ʈ���� �̸�(�ִϸ����� Ʈ����)")]
    public string ringTrigger = "Ring";
    [Tooltip("���� �︱ ���� ���� �̸�(����). �����ϸ� �� ���°� ���� ������ ringing ����")]
    public string ringStateName = "";
    [Tooltip("�ּ� ���� �ð�(��). �ʹ� ª���� ���� �����")]
    public float minBlockDuration = 0.1f;
    [Tooltip("�ִ� ���� �ð�(��). ������ġ")]
    public float maxBlockDuration = 1.0f;

    public bool IsRinging { get; private set; }

    Coroutine _co;

    public bool TryRing(float leadTime)
    {
        if (IsRinging) return false;
        if (animator == null) animator = GetComponent<Animator>();
        if (animator) { animator.ResetTrigger(ringTrigger); animator.SetTrigger(ringTrigger); }
        if (_co != null) StopCoroutine(_co);
        _co = StartCoroutine(Run(leadTime));
        return true;
    }

    IEnumerator Run(float leadTime)
    {
        IsRinging = true;

        // �ּ� ����Ÿ�Ӹ�ŭ�� �ݵ�� ����
        float t = 0f;
        float minTime = Mathf.Max(minBlockDuration, leadTime);
        while (t < minTime) { t += Time.deltaTime; yield return null; }

        // ringStateName�� �����ߴٸ�, �ش� ���°� ���� ������(�Ǵ� �ִ� �ð�����) ����
        if (!string.IsNullOrEmpty(ringStateName) && animator != null)
        {
            float t2 = 0f;
            while (t2 < maxBlockDuration)
            {
                var st = animator.GetCurrentAnimatorStateInfo(0);
                if (!st.IsName(ringStateName)) break;
                t2 += Time.deltaTime;
                yield return null;
            }
        }

        IsRinging = false;
        _co = null;
    }
}
