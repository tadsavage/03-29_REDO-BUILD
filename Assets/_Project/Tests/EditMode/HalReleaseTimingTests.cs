using NUnit.Framework;
using GameCore.Diagnostics;

namespace GameCore.Tests
{
    /// <summary>HAL's release rule: an order goes to the floor at  appointment start - (calculated time + buffer).</summary>
    public class HalReleaseTimingTests
    {
        private const long Day = 1440;

        [Test]
        public void ReleasesExactlyAtAppointmentMinusWorkMinusBuffer()
        {
            long appt = 2 * Day + 8 * 60;               // day 3, 08:00
            float turn = 4.5f, buffer = 2f;             // 4.5 h of work + 2 h  =>  due at day 3 01:30
            long due = appt - 6 * 60 - 30;
            Assert.IsFalse(PlaytestBot.ReleaseIsDue(appt, due - 1, turn, buffer), "one minute early");
            Assert.IsTrue(PlaytestBot.ReleaseIsDue(appt, due, turn, buffer), "exactly on time");
            Assert.IsTrue(PlaytestBot.ReleaseIsDue(appt, due + 90, turn, buffer), "later is still due");
        }

        [Test]
        public void ALongerJobReleasesEarlier_AndTheBufferShiftsItOneForOne()
        {
            long appt = 2 * Day + 12 * 60;
            long now = 2 * Day + 6 * 60;                // trailer 6 h away
            Assert.IsFalse(PlaytestBot.ReleaseIsDue(appt, now, 3f, 2f), "3 h + 2 h = 5 h: not yet");
            Assert.IsTrue(PlaytestBot.ReleaseIsDue(appt, now, 4f, 2f), "4 h + 2 h = 6 h: now");
            Assert.IsFalse(PlaytestBot.ReleaseIsDue(appt, now, 4f, 1f), "a smaller buffer waits");
            Assert.IsTrue(PlaytestBot.ReleaseIsDue(appt, now, 3f, 3f), "a bigger buffer releases");
        }

        [Test]
        public void AnOverdueOrderIsAlwaysDue_AndNegativeInputsAreIgnored()
        {
            long appt = Day + 8 * 60;
            Assert.IsTrue(PlaytestBot.ReleaseIsDue(appt, appt + 120, 1f, 2f), "the trailer is already here");
            Assert.IsFalse(PlaytestBot.ReleaseIsDue(appt, appt - 600, -5f, -5f), "negative hours count as zero");
        }
    }
}
