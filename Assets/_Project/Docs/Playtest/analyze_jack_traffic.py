#!/usr/bin/env python3
"""Summarise JackTrafficRecorder runs for a nav A/B (written 2026-11-12).

Reads Logs/JackTraffic/run<N>_samples.csv and run<N>_log.txt (the recorder writes both; Logs/ is git-ignored and
lives on the machine that ran Unity) and prints, per arm, the mean and the min-max across that arm's runs.

    python analyze_jack_traffic.py --a 1-6 --b 11-16
    python analyze_jack_traffic.py --a 1-6 --b 11-16 --dir "C:/path/to/Logs/JackTraffic" --skip 40

Run numbers are the labels: the recorder does not store which nav it ran on, so number the arms (see NavAB_Checklist.md).

Metrics (per run, then averaged over the arm). These are THIS script's definitions: the 2026-11-10 / 2026-11-12 numbers in the other notes were
made with an analyze.py that is not in the repo, so re-run the baseline arm with this same script rather than comparing across scripts.
  picking %   samples where a selector is off the jack (picking on foot) / samples with a selector attached to a jack
  held %      samples with held > 0 (the driver's waiting-in-a-queue clock) / samples with a selector attached
  corridor %  of the HELD samples, the share inside the dock/staging corridor (z 48-59)
  stuck s     seconds of "aboard and not moving" episodes of 5 s or more (an episode = a jack with a selector aboard that moved < 0.1 m)
  events      counts of log lines: JackSolid, JackStall, GridNav (errors), giving way, step off / walk, re-issued drives
Samples before --skip real seconds are ignored (the selectors walk to their jacks first).
"""
import argparse, csv, os, re, statistics, sys
from collections import defaultdict

CORRIDOR_Z = (48.0, 59.0)
EVENTS = {
    "JackSolid": r"\[JackSolid\]",
    "JackStall": r"\[JackStall\]",
    "GridNav": r"\[GridNav\]",
    "giving way": r"giving way",
    "step off/walk": r"step(ped)? off|walks? to|walking",
    "re-issued": r"re-issuing the drive",
}


def parse_range(text):
    out = []
    for part in text.split(","):
        part = part.strip()
        if "-" in part:
            lo, hi = part.split("-")
            out.extend(range(int(lo), int(hi) + 1))
        elif part:
            out.append(int(part))
    return out


def truthy(v):
    return str(v).strip().lower() in ("true", "1")


def analyze_run(directory, n, skip):
    path = os.path.join(directory, f"run{n}_samples.csv")
    if not os.path.exists(path):
        return None
    rows = []
    with open(path, newline="", encoding="utf-8", errors="replace") as f:
        for r in csv.DictReader(f):
            try:
                rows.append({
                    "real": float(r["real"]), "jack": r["jack"], "x": float(r["x"]), "z": float(r["z"]),
                    "driver": r["driver"].strip(), "off": truthy(r["offJack"]),
                    "held": float(r["held"]) if r["held"].strip() else 0.0,
                })
            except (ValueError, KeyError):
                continue
    rows = [r for r in rows if r["real"] >= skip]
    attached = [r for r in rows if r["driver"]]
    res = {"samples": len(attached)}
    if not attached:
        res.update(picking=0.0, held=0.0, corridor=0.0, stuck=0.0)
    else:
        res["picking"] = 100.0 * sum(1 for r in attached if r["off"]) / len(attached)
        held = [r for r in attached if r["held"] > 0]
        res["held"] = 100.0 * len(held) / len(attached)
        in_corr = [r for r in held if CORRIDOR_Z[0] <= r["z"] <= CORRIDOR_Z[1]]
        res["corridor"] = 100.0 * len(in_corr) / len(held) if held else 0.0
        # stuck: per jack, consecutive attached + aboard samples that stay within 0.1 m of where the episode began
        by_jack = defaultdict(list)
        for r in attached:
            by_jack[r["jack"]].append(r)
        stuck = 0.0
        for samples in by_jack.values():
            samples.sort(key=lambda r: r["real"])
            start = None
            for r in samples:
                aboard = not r["off"]
                if start is not None and aboard and abs(r["x"] - start["x"]) < 0.1 and abs(r["z"] - start["z"]) < 0.1:
                    last = r
                    continue
                if start is not None and last["real"] - start["real"] >= 5.0:
                    stuck += last["real"] - start["real"]
                start, last = (r, r) if aboard else (None, None)
            if start is not None and last["real"] - start["real"] >= 5.0:
                stuck += last["real"] - start["real"]
        res["stuck"] = stuck
    log = os.path.join(directory, f"run{n}_log.txt")
    text = open(log, encoding="utf-8", errors="replace").read() if os.path.exists(log) else ""
    for name, pat in EVENTS.items():
        res[name] = len(re.findall(pat, text))
    return res


def summarise(label, runs):
    print(f"\n== {label}: runs {', '.join(str(n) for n, _ in runs)} ({len(runs)} runs)")
    if not runs:
        print("   no run files found")
        return {}
    keys = ["picking", "held", "corridor", "stuck"] + list(EVENTS)
    summary = {}
    for k in keys:
        vals = [r[k] for _, r in runs]
        summary[k] = vals
        unit = "%" if k in ("picking", "held", "corridor") else (" s" if k == "stuck" else "")
        print(f"   {k:<14} mean {statistics.mean(vals):7.1f}{unit}   range {min(vals):.1f} - {max(vals):.1f}{unit}")
    return summary


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--a", required=True, help="run numbers of arm A, e.g. 1-6")
    ap.add_argument("--b", default="", help="run numbers of arm B, e.g. 11-16")
    ap.add_argument("--dir", default=os.path.join("Logs", "JackTraffic"))
    ap.add_argument("--skip", type=float, default=40.0, help="ignore the first N real seconds of each run (start walk)")
    args = ap.parse_args()

    arms = {}
    for label, spec in (("A", args.a), ("B", args.b)):
        if not spec:
            continue
        runs = []
        for n in parse_range(spec):
            r = analyze_run(args.dir, n, args.skip)
            if r is None:
                print(f"(run {n}: no file in {args.dir})", file=sys.stderr)
            else:
                runs.append((n, r))
        arms[label] = summarise(label, runs)

    if "A" in arms and "B" in arms and arms["A"] and arms["B"]:
        print("\n== VERDICT HELPER (a clear result needs the B range to sit clear of the A range)")
        for k, better in (("picking", "higher"), ("held", "lower"), ("stuck", "lower"), ("corridor", "lower")):
            a, b = arms["A"][k], arms["B"][k]
            overlap = not (min(b) > max(a) or max(b) < min(a))
            move = statistics.mean(b) - statistics.mean(a)
            good = (move > 0) if better == "higher" else (move < 0)
            print(f"   {k:<9} B-A mean {move:+7.1f}   ranges {'OVERLAP (no conclusion)' if overlap else ('CLEAR: B better' if good else 'CLEAR: B worse')}")


if __name__ == "__main__":
    main()
