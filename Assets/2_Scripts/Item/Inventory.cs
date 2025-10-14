using System;
using System.Collections.Generic;
using UnityEngine;

public class Inventory : MonoBehaviour
{
    [Serializable]
    public class Slot
    {
        public ItemSO item;
        public int count;

        public bool IsEmpty => item == null || count <= 0;

        public void Clear()
        {
            item = null;
            count = 0;
        }
    }

    [Header("용량/초기화")]
    [SerializeField] int capacity = 16;

    [Header("소유자(플레이어) - 힐 시 전달용")]
    public GameObject owner;

    // 내부 저장
    [SerializeField] List<Slot> slots = new List<Slot>();
    public IReadOnlyList<Slot> Slots => slots;

    // 변경 알림(UI가 구독)
    public event Action OnChanged;

    void Awake()
    {
        if (slots.Count < capacity)
        {
            for (int i = slots.Count; i < capacity; i++)
                slots.Add(new Slot());
        }
    }

    // 아이템 추가 (스택 가능)
    public bool Add(ItemSO item, int amount = 1)
    {
        if (item == null || amount <= 0) return false;

        // 같은 아이템 스택 먼저 채우기(HealItemSO도 SO 단위로 스택)
        for (int i = 0; i < slots.Count; i++)
        {
            var s = slots[i];
            if (!s.IsEmpty && s.item == item)
            {
                s.count += amount;
                OnChanged?.Invoke();
                return true;
            }
        }

        // 빈 슬롯에 새로 넣기
        for (int i = 0; i < slots.Count; i++)
        {
            var s = slots[i];
            if (s.IsEmpty)
            {
                s.item = item;
                s.count = amount;
                OnChanged?.Invoke();
                return true;
            }
        }

        return false; // 꽉 찼음
    }

    public void RemoveAt(int index, int amount = 1)
    {
        if (index < 0 || index >= slots.Count) return;

        var s = slots[index];
        if (s.IsEmpty) return;

        s.count -= amount;
        if (s.count <= 0) s.Clear();

        OnChanged?.Invoke();
    }

    // 특정 타입 보유 여부(예: Heal만 보이게 필터할 때 사용)
    public bool HasItemType(ItemType t)
    {
        foreach (var s in slots)
        {
            if (s.IsEmpty || s.item == null) continue;
            if (s.item.type == t) return true;
        }
        return false;
    }

    // index 슬롯 사용 시도 (힐 등)
    public bool TryUseAt(int index, GameObject user = null)
    {
        if (index < 0 || index >= slots.Count) return false;
        var s = slots[index];
        if (s.IsEmpty || s.item == null) return false;

        var who = user != null ? user : owner;
        // ItemSO.Use(GameObject user)가 true면 실제로 소비된 것
        bool used = s.item.Use(who);
        if (!used) return false;

        s.count--;
        if (s.count <= 0) s.Clear();
        OnChanged?.Invoke();
        return true;
    }

    // 첫 번째 지정 타입 아이템 사용 (단축키용)
    public bool TryUseFirst(ItemType type, GameObject user = null)
    {
        for (int i = 0; i < slots.Count; i++)
        {
            var s = slots[i];
            if (s.IsEmpty || s.item == null) continue;
            if (s.item.type == type)
                return TryUseAt(i, user);
        }
        return false;
    }
}
