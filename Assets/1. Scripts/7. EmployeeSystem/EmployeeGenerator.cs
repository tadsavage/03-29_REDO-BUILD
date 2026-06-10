using System;
using UnityEngine;

public static class EmployeeGenerator
{
    public static EmployeeRecord Generate(EmployeeGender gender = EmployeeGender.Random, string idPrefix = "WHSE")
    {
        if (gender == EmployeeGender.Random)
            gender = RandomGender();

        EmployeeNameListJson names = EmployeeNameListLoader.Load();

        string firstName = PickFirstName(names, gender);
        string lastName = PickRandom(names.lastNames, "Barnes");
        int idNum = UnityEngine.Random.Range(1, 101);

        EmployeeRecord record = new EmployeeRecord
        {
            employeeName = $"{firstName} {lastName}",
            employeeIdPrefix = idPrefix,
            employeeId = $"{idPrefix} {idNum:D3}",
            gender = gender,
            fatigue = UnityEngine.Random.Range(10f, 95f),
            safety = UnityEngine.Random.Range(20f, 95f),
            morale = UnityEngine.Random.Range(30f, 100f),
            skill = UnityEngine.Random.Range(15f, 90f),
            skillLevel = UnityEngine.Random.Range(1, 6)
        };

        return record;
    }

    public static EmployeeRecord GenerateFromTemplate(EmployeeData template)
    {
        if (template == null)
            return Generate();

        EmployeeGender gender = EmployeeGender.Random;
        if (template.employeeName != null)
        {
            string lower = template.employeeName.ToLower();
            if (lower.StartsWith("mr. ") || lower.EndsWith(" m"))
                gender = EmployeeGender.Male;
            else if (lower.StartsWith("ms. ") || lower.EndsWith(" f"))
                gender = EmployeeGender.Female;
        }

        EmployeeRecord record = Generate(gender, template.employeeIdPrefix);
        return record;
    }

    private static EmployeeGender RandomGender()
    {
        float roll = UnityEngine.Random.value;
        return roll < 0.45f ? EmployeeGender.Male : (roll < 0.90f ? EmployeeGender.Female : EmployeeGender.Neutral);
    }

    private static string PickFirstName(EmployeeNameListJson names, EmployeeGender gender)
    {
        if (gender == EmployeeGender.Male)
            return PickRandom(names.maleFirstNames, "James");
        if (gender == EmployeeGender.Female)
            return PickRandom(names.femaleFirstNames, "Jennifer");
        return PickRandom(names.neutralFirstNames, "Alex");
    }

    private static string PickRandom(System.Collections.Generic.List<string> list, string fallback)
    {
        if (list == null || list.Count == 0)
            return fallback;
        return list[UnityEngine.Random.Range(0, list.Count)];
    }
}
