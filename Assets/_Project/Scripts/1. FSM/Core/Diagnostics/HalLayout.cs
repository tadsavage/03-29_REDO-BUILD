#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using GameCore.Labor;

namespace GameCore.Diagnostics
{
    [Serializable] public class HalStep { public int phase, id, x, y, rot; public string data; }
    [Serializable] public class HalPack { public string name; public List<HalStep> steps = new(); }
    [Serializable] public class HalEquipment { public int id, x, y, rot, count; public string countSetting, label; }

    /// <summary>A building HAL 9000 can start a company in. Stored as JSON under Resources/HalLayouts so more can be dropped in
    /// (Small Layout, Large Layout...) and picked from the HAL 9000 tab. Phases: 0 foundation, 1 walls and doors, 2 shipping lane,
    /// 3 guard shack, 4 racks (with aisle data in `data`).</summary>
    [Serializable]
    public class HalLayout
    {
        public string name, description;
        public List<HalStep> steps = new();
        public List<HalEquipment> equipment = new();
        /// <summary>Optional add-ons the planner can build (and later remove), e.g. a second dock door with its lane.</summary>
        public List<HalPack> packs = new();

        public const string Folder = "HalLayouts";

        public static List<string> Names()
        {
            var list = Resources.LoadAll<TextAsset>(Folder).Select(a => a.name).OrderBy(n => n).ToList();
            return list;
        }

        public static HalLayout Load(string name)
        {
            var asset = Resources.Load<TextAsset>(Folder + "/" + name);
            if (asset == null)
            {
                var all = Resources.LoadAll<TextAsset>(Folder);
                asset = all.FirstOrDefault();
                if (asset == null) return null;
            }
            var layout = JsonUtility.FromJson<HalLayout>(asset.text);
            if (layout != null && string.IsNullOrEmpty(layout.name)) layout.name = asset.name;
            return layout;
        }
    }

    /// <summary>Writes whatever is standing in the running warehouse out as a layout file, so a hand-edited building can become
    /// "Small Layout", "Large Layout" and so on. Foundation, walls and doors, floor tiles, the guard shack and anything else become
    /// build steps; racks keep their aisle data; machines and waypoints become equipment spots.</summary>
    public static class HalLayoutExporter
    {
        public static string Export(string name, out string message)
        {
            message = "";
            if (string.IsNullOrWhiteSpace(name)) { message = "Type a name first."; return null; }
            var layout = new HalLayout { name = name.Trim(), description = "Saved from a running game " + DateTime.Now.ToString("yyyy-MM-dd HH:mm") };
            int skipped = 0;
            foreach (var po in PlacedObjectRegistry.All)
            {
                if (po == null || po.data == null) continue;
                var d = po.data;
                if (po.GetComponent<EmployeeIdentity>() != null || po.GetComponent<TruckController>() != null) { skipped++; continue; }
                string cat = d.category ?? "";
                if (cat == "Worker" || cat == "Staff" || cat == "Vehicle Traffic") { skipped++; continue; }
                if (d.id == 34) continue;   // the floor tile a foundation lays by itself
                int rot = po.rotation;
                if (cat == "Vehicles" || cat == "Waypoints")
                {
                    layout.equipment.Add(new HalEquipment { id = d.id, x = po.gridX, y = po.gridY, rot = rot, count = 1, countSetting = "", label = d.objName });
                    continue;
                }
                int phase = cat == "Grounds" || cat == "Foundation" ? 0 : cat == "Walls" ? 1 : cat == "Floor" ? 2 : cat == "Loss Prevention" ? 3 : cat == "Racking" ? 4 : 5;
                string data = phase == 4 ? RackSaveCodec.Encode(po) : "";
                layout.steps.Add(new HalStep { phase = phase, id = d.id, x = po.gridX, y = po.gridY, rot = rot, data = data });
            }
            layout.steps = layout.steps.OrderBy(s => s.phase).ThenBy(s => s.y).ThenBy(s => s.x).ToList();

            // Keep the add-on packs of whichever layout is selected, so a copy of Standard still knows how to build a second door.
            var current = HalLayout.Load(PlaytestBot.Ensure().S.layoutName);
            if (current != null) layout.packs = current.packs;

            string dir = System.IO.Path.Combine(Application.dataPath, "_Project", "Resources", HalLayout.Folder);
            System.IO.Directory.CreateDirectory(dir);
            string file = System.IO.Path.Combine(dir, layout.name + ".json");
            System.IO.File.WriteAllText(file, JsonUtility.ToJson(layout, true));
#if UNITY_EDITOR
            UnityEditor.AssetDatabase.Refresh();
#endif
            message = $"Saved \"{layout.name}\": {layout.steps.Count} build steps, {layout.equipment.Count} equipment spots ({skipped} workers/trucks left out).";
            return file;
        }
    }
}
#endif
