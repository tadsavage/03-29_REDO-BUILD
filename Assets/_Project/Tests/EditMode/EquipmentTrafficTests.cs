using System;
using NUnit.Framework;
using UnityEngine;
using GameCore.Actors;

public class EquipmentTrafficTests
{
    private GameObject _go;
    private GameObject _blockerObject;
    private MHEOperatorSlot _blocker;
    private EquipmentTrafficAgent _agent;
    private Func<float> _savedRandom;

    [SetUp]
    public void SetUp()
    {
        _savedRandom = EquipmentTrafficAgent.RandomSource;
        _go = new GameObject("TestMachine");
        _agent = _go.AddComponent<EquipmentTrafficAgent>();
        _blockerObject = new GameObject("BlockingVehicle");
        _blocker = _blockerObject.AddComponent<MHEOperatorSlot>();
    }

    [TearDown]
    public void TearDown()
    {
        EquipmentTrafficAgent.RandomSource = _savedRandom;
        UnityEngine.Object.DestroyImmediate(_go);
        UnityEngine.Object.DestroyImmediate(_blockerObject);
    }

    private void Hold(float seconds, float step = 0.5f)
    {
        for (float t = 0f; t < seconds - 0.001f; t += step) _agent.NoteHeld(_blocker, null, step);
    }

    [Test]
    public void DefaultHonkChanceIsQuarterAndConfigurable()
    {
        Assert.AreEqual(0.25f, _agent.HonkChance, 1e-5f);
        _agent.HonkChance = 0.6f;
        Assert.AreEqual(0.6f, _agent.HonkChance, 1e-5f);
        _agent.HonkChance = 4f;
        Assert.AreEqual(1f, _agent.HonkChance, 1e-5f);
    }

    [Test]
    public void ShortHoldIsWaitingNotBlockedAndNeverRolls()
    {
        Hold(1.5f);
        Assert.AreEqual(TrafficState.Waiting, _agent.State);
        Assert.AreEqual(0, _agent.RollCount);
    }

    [Test]
    public void BlockedRollsOncePerFiveSeconds()
    {
        EquipmentTrafficAgent.RandomSource = () => 0.99f;
        Hold(2f);
        Assert.AreEqual(TrafficState.Blocked, _agent.State);
        int start = _agent.RollCount;
        Hold(10f);
        Assert.AreEqual(2, _agent.RollCount - start, "one roll per 5 s while blocked");
        Assert.AreEqual(0, _agent.HonkCount);
    }

    [Test]
    public void LowRollHonksAndShowsRedLines()
    {
        EquipmentTrafficAgent.RandomSource = () => 0.0f;
        Hold(2f);
        Hold(5f);
        Assert.GreaterOrEqual(_agent.HonkCount, 1);
        Assert.IsTrue(_agent.FxShowing, "red frustration lines should be visible after a honk");
        Assert.GreaterOrEqual(_agent.FxLineCount, 3);
    }

    [Test]
    public void BlockedByWallDoesNotHonk()
    {
        EquipmentTrafficAgent.RandomSource = () => 0f;
        for (int i = 0; i < 40; i++) _agent.NoteHeld(null, "wall", 0.5f);
        Assert.AreEqual(TrafficState.Blocked, _agent.State);
        Assert.AreEqual(0, _agent.RollCount);
        Assert.AreEqual(0, _agent.HonkCount);
        Assert.IsFalse(_agent.FxShowing);
    }

    [Test]
    public void SolidHoldDoesNotAdvanceVehicleHonkTimer()
    {
        EquipmentTrafficAgent.RandomSource = () => 0f;
        for (int i = 0; i < 40; i++) _agent.NoteHeld(null, "wall", 0.5f);
        Hold(4f);
        Assert.AreEqual(0, _agent.RollCount);
        Hold(1f);
        Assert.AreEqual(1, _agent.RollCount);
        Assert.AreEqual(1, _agent.HonkCount);
    }

    [Test]
    public void ChanceBoundaryIsRespected()
    {
        EquipmentTrafficAgent.RandomSource = () => 0.25f;
        Assert.IsFalse(_agent.RollHonk());
        EquipmentTrafficAgent.RandomSource = () => 0.2499f;
        Assert.IsTrue(_agent.RollHonk());
    }

    [Test]
    public void WaitingForGapNeverBecomesBlockedOrHonks()
    {
        EquipmentTrafficAgent.RandomSource = () => 0f;
        for (int i = 0; i < 100; i++) _agent.NoteWaiting();
        Assert.AreNotEqual(TrafficState.Blocked, _agent.State);
        Assert.AreEqual(0, _agent.HonkCount);
    }

    [Test]
    public void ClearResetsHoldClock()
    {
        Hold(3f);
        _agent.NoteClear(true);
        Assert.AreEqual(TrafficState.Free, _agent.State);
        Assert.AreEqual(0f, _agent.HeldReal);
    }

    [Test]
    public void ReportedBlockageMarksBlockedWithReason()
    {
        _agent.MarkReported("no safe reverse");
        Assert.AreEqual(TrafficState.Blocked, _agent.State);
        Assert.AreEqual("no safe reverse", _agent.ReportedReason);
    }

    [Test]
    public void PriorityOrdersReachOverStockerOverJack()
    {
        Assert.Greater(EquipmentTraffic.Priority(EquipmentRole.ReachTruck), EquipmentTraffic.Priority(EquipmentRole.DockStocker));
        Assert.Greater(EquipmentTraffic.Priority(EquipmentRole.DockStocker), EquipmentTraffic.Priority(EquipmentRole.Jack));
    }

    [Test]
    public void ManeuverLeaseIsExclusive()
    {
        var a = new GameObject("A").AddComponent<MHEOperatorSlot>();
        var b = new GameObject("B").AddComponent<MHEOperatorSlot>();
        try
        {
            Assert.IsTrue(EquipmentTraffic.TryBeginManeuver(a));
            Assert.IsFalse(EquipmentTraffic.TryBeginManeuver(b), "second machine must wait while the first maneuvers");
            Assert.IsTrue(EquipmentTraffic.IsManeuvering(a));
            EquipmentTraffic.EndManeuver(a);
            Assert.IsFalse(EquipmentTraffic.IsManeuvering(a));
        }
        finally
        {
            EquipmentTraffic.EndManeuver(a);
            UnityEngine.Object.DestroyImmediate(a.gameObject);
            UnityEngine.Object.DestroyImmediate(b.gameObject);
        }
    }

    [Test]
    public void SelectorReverseOptionsAreHalfToOneMetre()
    {
        foreach (float d in EquipmentTraffic.SelectorReverse) Assert.That(d, Is.InRange(0.5f, 1.0f));
    }

    [Test]
    public void VehicleSpacingStopsAtOneMetreAndAllowsBackingAway()
    {
        var first = CreateVehicle("FirstVehicle", Vector3.zero);
        var second = CreateVehicle("SecondVehicle", new Vector3(0f, 0f, 3.1f));
        try
        {
            var firstSlot = first.GetComponent<MHEOperatorSlot>();
            var secondSlot = second.GetComponent<MHEOperatorSlot>();
            Assert.IsTrue(MachineClearance.StepBlocked(firstSlot, new Vector3(0f, 0f, 0.2f),
                first.transform.rotation, out string what));
            var blocker = MachineClearance.LastMachineHit;
            Assert.AreSame(secondSlot, blocker);
            Assert.AreEqual(second.name, what);
            Assert.IsTrue(MachineClearance.JackStepBlocked(firstSlot, new Vector3(0f, 0f, 0.2f),
                first.transform.rotation, out _), "reach-truck clearance must apply the same spacing buffer");

            first.transform.position = new Vector3(0f, 0f, 0.3f);
            Assert.IsFalse(MachineClearance.StepBlocked(firstSlot, new Vector3(0f, 0f, 0.1f),
                first.transform.rotation, out _),
                "a vehicle already inside the buffer must be able to increase its gap");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(first);
            UnityEngine.Object.DestroyImmediate(second);
        }
    }

    private static GameObject CreateVehicle(string name, Vector3 position)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.position = position;
        go.transform.localScale = new Vector3(1f, 1f, 2f);
        go.AddComponent<MHEOperatorSlot>();
        go.AddComponent<PlacedObject>();
        return go;
    }
}
