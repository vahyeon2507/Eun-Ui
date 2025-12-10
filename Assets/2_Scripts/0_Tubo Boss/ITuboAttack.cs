using UnityEngine;

/// <summary>
/// 투보의 개별 공격 프리팹(포인트 어택, 스트라이크 투사체 등)이
/// 그로기 순간에 즉시 취소될 수 있도록 최소 인터페이스.
/// </summary>
public interface ITuboAttack
{
    /// 컨트롤러가 그로기 진입 시 호출. 즉시 공격을 중단/정리해야 한다.
    void CancelAttack();
}
