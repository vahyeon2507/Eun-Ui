using System;

/// <summary>
/// 패링 연속 성공 횟수를 제공하는 인터페이스
/// </summary>
public interface IParryStreakProvider
{
    /// <summary>
    /// 현재 패링 연속 성공 횟수
    /// </summary>
    int CurrentParryStreak { get; }
    
    /// <summary>
    /// 패링 연속 성공 횟수가 변경될 때 발생하는 이벤트
    /// </summary>
    event Action<int> OnParryStreakChanged;
}