using NUnit.Framework;

namespace Blastlands.Core.Tests
{
    public class CharacterTokenTests
    {
        [TestCase(CharacterKind.Demolisher, "demolisher")]
        [TestCase(CharacterKind.Runner, "runner")]
        [TestCase(CharacterKind.Grenadier, "grenadier")]
        [TestCase(CharacterKind.Sapper, "sapper")]
        public void EachCharacterTravelsAsTheTokenTheLobbyReads(CharacterKind character, string token)
        {
            Assert.That(CharacterTokens.Write(character), Is.EqualTo(token));
            Assert.That(CharacterTokens.TryRead(token, out CharacterKind read), Is.True);
            Assert.That(read, Is.EqualTo(character));
        }

        [Test]
        public void EveryCharacterOnTheRosterHasAToken()
        {
            foreach (CharacterKind character in CharacterKits.All)
            {
                Assert.That(CharacterTokens.Write(character), Is.Not.Null, character.ToString());
            }
        }

        [Test]
        public void NoChoiceSendsNothing()
        {
            Assert.That(CharacterTokens.Write(CharacterKind.None), Is.Null);
        }

        [Test]
        public void ARememberedValueThisBuildDoesNotKnowIsNoChoice()
        {
            foreach (string token in new[] { null, "", "none", "hoarder", "Runner", "runner " })
            {
                Assert.That(CharacterTokens.TryRead(token, out CharacterKind read), Is.False, token ?? "null");
                Assert.That(read, Is.EqualTo(CharacterKind.None), token ?? "null");
            }
        }
    }
}
