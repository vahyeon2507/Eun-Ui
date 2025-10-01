using System;
public interface IParryStreakProvider
{
    int CurrentParryStreak { get; }
    event Action<int> OnParryStreakChanged;
}
