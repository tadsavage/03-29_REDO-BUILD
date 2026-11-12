using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.AI;
using GameCore.Actors;
using GameCore.Inventory;

namespace GameCore.Navigation
{
    /// <summary>
    /// REACHABILITY AUDIT: for every pick location in the warehouse, can a pallet jack (following aisle traffic) and a reach truck get to it
    /// on the grid, and what does the NavMesh say for the same trip? No game time needed: run it in Play with the clock paused.
    ///
    /// The static layout only: parked machines and floor pallets are cleared from the dynamic layer first, so the answer is about the
    /// building, not about where something happens to be standing. A disagreement is the point of the exercise:
    ///   - grid REFUSES, NavMesh says complete  -> the NavMesh is snapping the goal (or ignoring the machine's length / aisle direction)
    ///   - grid OK, NavMesh says partial/invalid -> the NavMesh has a hole the grid does not (the carve / edge-of-floor problems)
    /// </summary>
    public static class NavAudit
    {
        public const float JackParkOffset = 2.1f;   // OrderSelectionTaskDriver.ParkOffset: jack centre this far from the pick face
        private const float LaneMouthX = -5f, LaneMouthZ = 43f;   // a staging-lane cell on this layout: the way out for a finished pallet

        private sealed class Row
        {
            public string Address;
            public int Aisle;
            public Vector3 Park, Anchor;
            public string JackGrid, JackBack, RtGrid, JackNav, RtNav;
            public bool JackGridOk, JackBackOk, RtGridOk, JackNavOk, RtNavOk;
            public float JackMoved, JackSnap, RtMoved, RtSnap;
        }

        public static string Run(bool writeFile = true)
        {
            var planner = NavGridService.EnsurePlanner();
            if (planner == null) return "NavAudit: no PlacementGrid in the scene.";
            var grid = planner.Grid;
            grid.ClearDynamic();   // the building only

            MHEOperatorSlot jack = null, rt = null;
            foreach (var m in MachineClearance.AllMachines())
            {
                if (m == null) continue;
                if (m.IsPalletJack && jack == null) jack = m;
                else if (!m.IsPalletJack && m.name.StartsWith("RT") && rt == null) rt = m;
            }
            if (jack == null || rt == null) return "NavAudit: needs at least one pallet jack and one reach truck in the scene.";

            var jackProfile = NavGridBuilder.ProfileOf(jack, canReverse: true, followAisleTraffic: true);
            var rtProfile = NavGridBuilder.ProfileOf(rt, canReverse: true, followAisleTraffic: false);
            // Both machines start from the SAME open-floor point (a staging-lane cell). Their parking spots sit on the floor's north edge,
            // where the NavMesh (eroded by the agent radius) has no mesh at all, so a NavMesh answer from there is always "start off-mesh".
            float y = jack.transform.position.y;
            Vector3 laneMouth = new Vector3(LaneMouthX, y, LaneMouthZ);
            Vector3 jackStart = laneMouth, rtStart = laneMouth;
            int jackHeading = NavGrid.East, rtHeading = NavGrid.East;

            int mheType = 0; bool haveMhe = false;
            for (int i = 0; i < NavMesh.GetSettingsCount(); i++)
            {
                var s = NavMesh.GetSettingsByIndex(i);
                if (NavMesh.GetSettingsNameFromID(s.agentTypeID) == "MHE") { mheType = s.agentTypeID; haveMhe = true; }
            }

            var rows = new List<Row>();
            foreach (var loc in LocationRegistry.All)
            {
                if (loc == null) continue;
                string a = loc.Address;
                if (string.IsNullOrEmpty(a) || a.Length < 8 || !int.TryParse(a.Substring(0, 2), out int aisle)) continue;
                if (a[6] != '0' && a[6] != '1') continue;   // pick levels only (numeric level char)

                Vector3 slot = loc.transform.position;
                Transform anchorT = loc.transform.Find("LocApproachAnchor");
                Vector3 toAisle = anchorT != null ? anchorT.position - slot : loc.transform.forward;
                toAisle.y = 0f;
                if (toAisle.sqrMagnitude < 1e-4f) continue;
                toAisle.Normalize();
                var row = new Row { Address = a, Aisle = aisle };
                row.Park = new Vector3(slot.x, jackStart.y, slot.z) + toAisle * JackParkOffset;
                row.Anchor = anchorT != null ? new Vector3(anchorT.position.x, rtStart.y, anchorT.position.z) : row.Park;

                // Jack: there, and back out to the lane.
                var jr = planner.PlanNearest(jackProfile, jackStart, jackHeading, row.Park, -1, 2, out row.JackMoved);
                row.JackGridOk = jr.Ok; row.JackGrid = jr.Ok ? "ok" : jr.Status.ToString();
                if (jr.Ok)
                {
                    var last = jr.Steps[jr.Steps.Count - 1];
                    var back = planner.Plan(jackProfile, grid.CellCenter(last.X, last.Z, jackStart.y), last.Heading, laneMouth, -1);
                    row.JackBackOk = back.Ok; row.JackBack = back.Ok ? "ok" : back.Status.ToString();
                }
                else row.JackBack = "-";

                // Reach truck.
                var rr = planner.PlanNearest(rtProfile, rtStart, rtHeading, row.Anchor, -1, 2, out row.RtMoved);
                row.RtGridOk = rr.Ok; row.RtGrid = rr.Ok ? "ok" : rr.Status.ToString();

                // The NavMesh's answer for the same two trips.
                if (haveMhe)
                {
                    row.JackNav = NavmeshAnswer(mheType, jackStart, row.Park, out row.JackSnap, out row.JackNavOk);
                    row.RtNav = NavmeshAnswer(mheType, rtStart, row.Anchor, out row.RtSnap, out row.RtNavOk);
                }
                else { row.JackNav = row.RtNav = "no MHE agent type"; }
                rows.Add(row);
            }

            string report = Format(rows, jackStart, rtStart);
            if (writeFile)
            {
                try
                {
                    string path = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Application.dataPath) ?? ".", "Logs", "NavAudit.txt");
                    System.IO.File.WriteAllText(path, report);
                    report += $"\n(written to {path})";
                }
                catch (System.Exception e) { report += "\n(could not write Logs/NavAudit.txt: " + e.Message + ")"; }
            }
            return report;
        }

        // complete + how far the goal had to be snapped onto the mesh: "complete (snapped 1.2 m)". Partial / invalid / no mesh are failures.
        private static string NavmeshAnswer(int agentType, Vector3 from, Vector3 to, out float snap, out bool ok)
        {
            snap = 0f; ok = false;
            var f = new NavMeshQueryFilter { agentTypeID = agentType, areaMask = NavMesh.AllAreas };
            if (!NavMesh.SamplePosition(from, out var h0, 4f, f)) return "start off-mesh";
            if (!NavMesh.SamplePosition(to, out var h1, 4f, f)) return "goal has no mesh within 4 m";
            snap = Vector3.Distance(new Vector3(to.x, 0f, to.z), new Vector3(h1.position.x, 0f, h1.position.z));
            var path = new NavMeshPath();
            if (!NavMesh.CalculatePath(h0.position, h1.position, f, path)) return "no path";
            ok = path.status == NavMeshPathStatus.PathComplete;
            return path.status == NavMeshPathStatus.PathComplete ? "complete" : path.status.ToString();
        }

        private static string Format(List<Row> rows, Vector3 jackStart, Vector3 rtStart)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"NAV AUDIT - {rows.Count} pick locations (levels 0/1), static layout, both machines starting at the lane point ({jackStart.x:0.0},{jackStart.z:0.0}) facing east");
            int jg = 0, jb = 0, rg = 0, jn = 0, rn = 0, jgNo_jnYes = 0, jgYes_jnNo = 0, rgNo_rnYes = 0, rgYes_rnNo = 0;
            foreach (var r in rows)
            {
                if (r.JackGridOk) jg++; if (r.JackBackOk) jb++; if (r.RtGridOk) rg++; if (r.JackNavOk) jn++; if (r.RtNavOk) rn++;
                if (!r.JackGridOk && r.JackNavOk) jgNo_jnYes++;
                if (r.JackGridOk && !r.JackNavOk) jgYes_jnNo++;
                if (!r.RtGridOk && r.RtNavOk) rgNo_rnYes++;
                if (r.RtGridOk && !r.RtNavOk) rgYes_rnNo++;
            }
            sb.AppendLine($"JACK       grid reaches {jg}/{rows.Count} (and gets back out to the lane from {jb});  NavMesh complete {jn}/{rows.Count}");
            sb.AppendLine($"REACH TRUCK grid reaches {rg}/{rows.Count};  NavMesh complete {rn}/{rows.Count}");
            sb.AppendLine($"DISAGREE  jack: grid refuses but NavMesh complete = {jgNo_jnYes};  grid ok but NavMesh not complete = {jgYes_jnNo}");
            sb.AppendLine($"DISAGREE  reach truck: grid refuses but NavMesh complete = {rgNo_rnYes};  grid ok but NavMesh not complete = {rgYes_rnNo}");

            // How far each system had to shift the goal: the grid moves it to the nearest cell the machine fits in; the NavMesh snaps it onto
            // the mesh. The final approach to the real spot is then a scripted move of exactly this distance.
            float jm = 0, js = 0, rm = 0, rs = 0, jmMax = 0, jsMax = 0, rmMax = 0, rsMax = 0;
            foreach (var r in rows)
            {
                jm += r.JackMoved; js += r.JackSnap; rm += r.RtMoved; rs += r.RtSnap;
                jmMax = Mathf.Max(jmMax, r.JackMoved); jsMax = Mathf.Max(jsMax, r.JackSnap); rmMax = Mathf.Max(rmMax, r.RtMoved); rsMax = Mathf.Max(rsMax, r.RtSnap);
            }
            float n = Mathf.Max(1, rows.Count);
            sb.AppendLine($"GOAL SHIFT (metres, avg / max)  jack: grid {jm / n:0.00}/{jmMax:0.0}, NavMesh snap {js / n:0.00}/{jsMax:0.0};  reach truck: grid {rm / n:0.00}/{rmMax:0.0}, NavMesh snap {rs / n:0.00}/{rsMax:0.0}");

            sb.AppendLine("\nPER AISLE (jack grid ok / jack back-out ok / jack NavMesh ok / rt grid ok / rt NavMesh ok, of N)");
            var byAisle = new SortedDictionary<int, int[]>();
            foreach (var r in rows)
            {
                if (!byAisle.TryGetValue(r.Aisle, out var c)) byAisle[r.Aisle] = c = new int[6];
                c[0]++; if (r.JackGridOk) c[1]++; if (r.JackBackOk) c[2]++; if (r.JackNavOk) c[3]++; if (r.RtGridOk) c[4]++; if (r.RtNavOk) c[5]++;
            }
            foreach (var kv in byAisle) sb.AppendLine($"  aisle {kv.Key:00}: {kv.Value[1]}/{kv.Value[2]}/{kv.Value[3]} | {kv.Value[4]}/{kv.Value[5]} of {kv.Value[0]}");

            sb.AppendLine("\nFAILURES AND DISAGREEMENTS (address: jack grid | back-out | jack NavMesh(snap m) || rt grid | rt NavMesh(snap m))");
            int shown = 0;
            foreach (var r in rows)
            {
                bool bad = !r.JackGridOk || !r.JackBackOk || !r.JackNavOk || !r.RtGridOk || !r.RtNavOk;
                if (!bad) continue;
                if (shown++ >= 80) { sb.AppendLine("  ... (more in the file)"); break; }
                sb.AppendLine($"  {r.Address}: {r.JackGrid}{(r.JackMoved > 0.01f ? $" (goal moved {r.JackMoved:0.0})" : "")} | {r.JackBack} | {r.JackNav}({r.JackSnap:0.0}) || {r.RtGrid}{(r.RtMoved > 0.01f ? $" (goal moved {r.RtMoved:0.0})" : "")} | {r.RtNav}({r.RtSnap:0.0})");
            }
            if (shown == 0) sb.AppendLine("  none");
            return sb.ToString();
        }
    }
}
