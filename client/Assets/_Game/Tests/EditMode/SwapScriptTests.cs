using System.IO;
using Blastlands.Core.Net;
using Blastlands.Core.Update;
using NUnit.Framework;

namespace Blastlands.Core.Tests
{
    public class SwapScriptTests
    {
        private static UpdateLayout Layout(string folder)
        {
            return new UpdateLayout(Path.Combine(Path.GetTempPath(), folder, "Blastlands"), new GameVersion(26, 9, 20));
        }

        [Test]
        public void AnApostropheInThePathCannotCloseTheString()
        {
            Assert.That(SwapScript.Quote("C:\\Users\\O'Brien"), Is.EqualTo("'C:\\Users\\O''Brien'"));
        }

        [Test]
        public void TypographicQuotesAreEscapedLikeApostrophes()
        {
            Assert.That(SwapScript.Quote("O\u2019Brien"), Is.EqualTo("'O\u2019\u2019Brien'"));
            Assert.That(SwapScript.Quote("\u2018x\u201Ay\u201B"), Is.EqualTo("'\u2018\u2018x\u201A\u201Ay\u201B\u201B'"));
        }

        [Test]
        public void TheScriptWaitsForTheGameItWasStartedFrom()
        {
            string script = SwapScript.PowerShell(4242, Layout("games"), "Blastlands.exe");

            Assert.That(script, Does.Contain("Wait-Process -Id 4242 "));
        }

        [Test]
        public void AGameThatOutlivesTheWaitIsLeftAlone()
        {
            string script = SwapScript.PowerShell(4242, Layout("games"), "Blastlands.exe");

            int wait = script.IndexOf("Wait-Process -Id 4242 ");
            int stillRunning = script.IndexOf("if (Get-Process -Id 4242 -ErrorAction SilentlyContinue) { exit 1 }");
            int moveAside = script.IndexOf("Move-Retrying $install $previous");

            Assert.That(stillRunning, Is.GreaterThan(wait));
            Assert.That(moveAside, Is.GreaterThan(stillRunning));
        }

        [Test]
        public void TheInstallIsMovedAsideBeforeTheNewBuildTakesItsPlace()
        {
            string script = SwapScript.PowerShell(1, Layout("games"), "Blastlands.exe");

            int moveAside = script.IndexOf("Move-Retrying $install $previous");
            int moveIn = script.IndexOf("Move-Retrying $staged $install");
            int rollBack = script.IndexOf("Move-Retrying $previous $install");

            Assert.That(moveAside, Is.GreaterThan(0));
            Assert.That(moveIn, Is.GreaterThan(moveAside));
            Assert.That(rollBack, Is.GreaterThan(moveIn));
        }

        [Test]
        public void EveryPathIsWrittenAsALiteral()
        {
            UpdateLayout layout = Layout("O'Brien's games");

            string script = SwapScript.PowerShell(1, layout, "Blastlands.exe");

            Assert.That(script, Does.Contain("$install = " + SwapScript.Quote(layout.Install)));
            Assert.That(script, Does.Contain("$staged = " + SwapScript.Quote(layout.Staged)));
            Assert.That(script, Does.Contain("$previous = " + SwapScript.Quote(layout.Previous)));
        }
    }
}
