using UnityEngine;

public enum ItemType { Heal, Etc }   // 필요하면 더 추가

public abstract class ItemSO : ScriptableObject
{
    public string displayName;
    public Sprite icon;
    public int maxStack = 20;
    public ItemType type = ItemType.Etc;

    /// <summary>사용 성공 시 true 반환(1개 소모할지 UI가 결정)</summary>
    public abstract bool Use(GameObject user);
}
