using System.Collections.Generic;
using UnityEngine;

public static class EmployeeNameListLoader
{
    private static EmployeeNameListJson _cached;
    private static bool _loaded;

    public static EmployeeNameListJson Load()
    {
        if (_loaded)
            return _cached;

        _loaded = true;

        TextAsset jsonAsset = Resources.Load<TextAsset>("EmployeeAssets/employee_names");
        if (jsonAsset != null)
        {
            try
            {
                _cached = JsonUtility.FromJson<EmployeeNameListJson>(jsonAsset.text);
                if (_cached != null && HasAnyNames(_cached))
                {
                    Debug.Log("[EmployeeNameListLoader] Loaded names from Resources/EmployeeAssets/employee_names.json");
                    return _cached;
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[EmployeeNameListLoader] Failed to parse JSON, using fallback: {e.Message}");
            }
        }

        _cached = CreateFallback();
        Debug.Log("[EmployeeNameListLoader] Using fallback name lists.");
        return _cached;
    }

    public static void Reload()
    {
        _loaded = false;
        _cached = null;
    }

    private static bool HasAnyNames(EmployeeNameListJson list)
    {
        return (list.maleFirstNames != null && list.maleFirstNames.Count > 0)
            || (list.femaleFirstNames != null && list.femaleFirstNames.Count > 0)
            || (list.neutralFirstNames != null && list.neutralFirstNames.Count > 0)
            || (list.lastNames != null && list.lastNames.Count > 0);
    }

    private static EmployeeNameListJson CreateFallback()
    {
        return new EmployeeNameListJson
        {
            maleFirstNames = new List<string>
            {
                "James", "John", "Robert", "Michael", "David",
                "Richard", "Marcus", "Daniel", "Chris", "Jordan",
                "Tyler", "Brandon", "Kevin", "Brian", "Steven"
            },
            femaleFirstNames = new List<string>
            {
                "Mary", "Patricia", "Jennifer", "Linda", "Barbara",
                "Lisa", "Sarah", "Karen", "Nancy", "Jessica",
                "Emily", "Ashley", "Amanda", "Michelle", "Stephanie"
            },
            neutralFirstNames = new List<string>
            {
                "Alex", "Morgan", "Riley", "Casey", "Taylor",
                "Quinn", "Avery", "Jordan", "Dakota", "Reese"
            },
            lastNames = new List<string>
            {
                "Smith", "Johnson", "Williams", "Brown", "Jones",
                "Garcia", "Miller", "Davis", "Rodriguez", "Martinez",
                "Hernandez", "Lopez", "Gonzalez", "Wilson", "Anderson",
                "Thomas", "Taylor", "Moore", "Jackson", "Martin"
            }
        };
    }
}
