using UnityEngine;

// 플레이어 패링 성공 시, 충돌한 오브젝트에서 이 컴포넌트를 찾아 호출하면 됨.
public class ParryReceiverTubo : MonoBehaviour
{
    public BossTuboController tubo;
    public void OnParried()
    {
        tubo?.OnParried();
    }
}
