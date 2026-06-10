using System;
using UnityEngine;

[Serializable]
public class EmployeeRecord
{
    public string employeeGuid;
    public string employeeName;
    public string employeeId;
    public string employeeIdPrefix;
    public EmployeeGender gender;

    public float fatigue;
    public float safety;
    public float morale;
    public float skill;
    public int skillLevel;

    public string avatarResourceKey;
    public string jobIconResourceKey;

    public EmployeeRecord()
    {
        employeeGuid = Guid.NewGuid().ToString();
    }

    public EmployeeRecord Clone()
    {
        return (EmployeeRecord)MemberwiseClone();
    }
}
