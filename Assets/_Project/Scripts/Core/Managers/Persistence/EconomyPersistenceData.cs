using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Snapshot of economy state: money, daily spending, hourly costs, vendor payments.
/// Ensures consistent financial tracking across save/load boundaries.
/// </summary>
[System.Serializable]
public class EconomyPersistenceData
{
    [System.Serializable]
    public class MoneySnapshot
    {
        public int capital = 0;
        public int spentToday = 0;
        public int spentTodayHourlyOnly = 0;
        public SerializableDictionary<string, int> spentTodayByObjectCategory = new();
    }

    [System.Serializable]
    public class HourlySnapshot
    {
        public SerializableDictionary<string, int> hourlyByGLLine = new();
        public SerializableDictionary<string, float> fractionalByGLLine = new();
    }

    [System.Serializable]
    public class PayrollSnapshot
    {
        public List<string> employeesPaidToday = new();  // employee GUIDs already paid in this in-game day
    }

    public MoneySnapshot money = new();
    public HourlySnapshot hourly = new();
    public PayrollSnapshot payroll = new();
}

/// <summary>
/// Helper class for serializing Dictionary to JSON.
/// Unity's JsonUtility doesn't support Dictionary directly.
/// </summary>
[System.Serializable]
public class SerializableDictionary<TKey, TValue>
{
    [System.Serializable]
    public class KeyValuePair
    {
        public TKey key;
        public TValue value;
    }

    public List<KeyValuePair> items = new();

    public void Add(TKey key, TValue value)
    {
        items.Add(new KeyValuePair { key = key, value = value });
    }

    public Dictionary<TKey, TValue> ToDictionary()
    {
        var dict = new Dictionary<TKey, TValue>();
        foreach (var item in items)
            dict[item.key] = item.value;
        return dict;
    }

    public static SerializableDictionary<TKey, TValue> FromDictionary(Dictionary<TKey, TValue> dict)
    {
        var result = new SerializableDictionary<TKey, TValue>();
        if (dict != null)
        {
            foreach (var kvp in dict)
                result.Add(kvp.Key, kvp.Value);
        }
        return result;
    }
}
