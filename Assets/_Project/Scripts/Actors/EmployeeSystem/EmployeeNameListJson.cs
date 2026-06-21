using System;
using System.Collections.Generic;

[Serializable]
public class EmployeeNameListJson
{
    public List<string> maleFirstNames = new();
    public List<string> femaleFirstNames = new();
    public List<string> neutralFirstNames = new();
    public List<string> lastNames = new();
}
