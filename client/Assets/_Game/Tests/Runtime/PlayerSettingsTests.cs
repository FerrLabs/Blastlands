using NUnit.Framework;
using UnityEditor;

namespace Blastlands.Runtime.Tests
{
    // A project setting rather than code, guarded because it is the kind of thing that
    // flips back in a settings pass and says nothing when it does.
    public class PlayerSettingsTests
    {
        [Test]
        public void TheGameKeepsRunningWhenItsWindowLosesFocus()
        {
            // Off, Unity stops ticking the moment the window is not focused. Measured
            // while it was off: 53 seconds of wall clock produced two frames and one
            // simulation tick.
            //
            // For four people round one screen that is arguable. For anything networked
            // it is not: a client that stops simulating while the server keeps going
            // comes back to a state it cannot reconcile, which lands on #14 and #15. The
            // headless server of #13 has no focus to lose and needs this on regardless.
            //
            // It is also why driving the editor from a script used to yield a static
            // first frame: the board renders, because Bind runs on frame one, and then
            // nothing moves.
            Assert.That(
                PlayerSettings.runInBackground,
                Is.True,
                "the build now pauses when it loses focus, which desyncs any networked match");
        }
    }
}
