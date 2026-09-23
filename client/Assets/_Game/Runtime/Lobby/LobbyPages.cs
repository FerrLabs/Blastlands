using System;
using Blastlands.Core;
using Blastlands.Core.Lobby;
using TMPro;
using UnityEngine;

namespace Blastlands.Runtime
{
    // One method per screen, each building the whole screen from scratch. The lobby
    // redraws a handful of rows on an event, not a frame, so rebuilding is simpler than
    // keeping widgets in sync and cannot drift from the state it is showing.
    public static class LobbyPages
    {
        private static readonly Vector2 WideButton = new Vector2(420f, 90f);
        private static readonly Vector2 RowSize = new Vector2(860f, 84f);
        private static readonly Vector2 CharacterButton = new Vector2(210f, 80f);

        public static TMP_InputField Name(RectTransform root, LobbyArt art, LobbyFlow flow, Action<string> submit)
        {
            RectTransform panel = LobbyChrome.Panel(root, art, "Name", new Vector2(900f, 460f));
            LobbyChrome.Label(panel, art, "BLASTLANDS", true, new Vector2(0f, 150f), 800f);
            LobbyChrome.Label(panel, art, "Who are you?", false, new Vector2(0f, 70f), 800f);

            TMP_InputField field = LobbyChrome.Field(panel, art, "Your name", new Vector2(0f, -10f), new Vector2(560f, 90f));
            field.text = flow.Player;

            LobbyChrome.Press(panel, art, "Continue", new Vector2(0f, -140f), WideButton, () => submit(field.text));
            Notice(panel, art, flow, new Vector2(0f, -210f));
            return field;
        }

        public static void Browse(
            RectTransform root,
            LobbyArt art,
            LobbyFlow flow,
            CharacterKind picked,
            Action<CharacterKind> pick,
            Action<MatchListing> join,
            Action create)
        {
            RectTransform panel = LobbyChrome.Panel(root, art, "Browse", new Vector2(1000f, 1000f));
            LobbyChrome.Label(panel, art, "MATCHES", true, new Vector2(0f, 450f), 900f);
            Characters(panel, art, picked, pick, new Vector2(0f, 370f));
            LobbyChrome.Press(panel, art, "Host a match", new Vector2(0f, -380f), WideButton, create);
            Notice(panel, art, flow, new Vector2(0f, -445f));

            if (flow.Matches.Count == 0)
            {
                LobbyChrome.Label(panel, art, "Nobody is hosting. Be the first.", false, new Vector2(0f, -40f), 800f);
                return;
            }

            float top = 200f;
            for (int i = 0; i < flow.Matches.Count && i < 6; i++)
            {
                MatchListing listing = flow.Matches[i];
                var offset = new Vector2(0f, top - i * (RowSize.y + 12f));
                string text = listing.Name + "   " + listing.Host + "   " + listing.Occupancy;

                if (listing.IsFull)
                {
                    RectTransform row = LobbyChrome.Panel(panel, art, "Row " + i, RowSize);
                    row.anchoredPosition = offset;
                    LobbyChrome.Label(row, art, text + "   full", false, Vector2.zero, RowSize.x - 40f);
                    continue;
                }

                MatchListing chosen = listing;
                LobbyChrome.Press(panel, art, text, offset, RowSize, () => join(chosen));
            }
        }

        private static void Characters(
            RectTransform panel, LobbyArt art, CharacterKind picked, Action<CharacterKind> pick, Vector2 offset)
        {
            float step = CharacterButton.x + 20f;
            float left = -step * (CharacterKits.All.Count - 1) / 2f;

            for (int i = 0; i < CharacterKits.All.Count; i++)
            {
                CharacterKind character = CharacterKits.All[i];
                var at = new Vector2(offset.x + left + i * step, offset.y);
                LobbyChrome.Press(panel, art, character.ToString(), at, CharacterButton, () => pick(character))
                    .interactable = character != picked;
            }

            LobbyChrome.Label(panel, art, Describes(picked), false, offset + new Vector2(0f, -70f), 900f);
        }

        private static string Describes(CharacterKind character)
        {
            switch (character)
            {
                case CharacterKind.Demolisher:
                    return "Demolisher starts with one more tile of reach.";
                case CharacterKind.Runner:
                    return "Runner starts two speed steps faster.";
                case CharacterKind.Grenadier:
                    return "Grenadier starts with bombs that flare at the tip of each arm.";
                case CharacterKind.Sapper:
                    return "Sapper starts with bombs that go through soft blocks.";
                default:
                    return "Pick who you play. Until then your seat decides.";
            }
        }

        public static void Room(
            RectTransform root, LobbyArt art, LobbyFlow flow, bool hosting, Action start, Action addBot, Action leave)
        {
            RectTransform panel = LobbyChrome.Panel(root, art, "Room", new Vector2(900f, 700f));
            LobbyChrome.Label(panel, art, hosting ? "YOUR MATCH" : "WAITING", true, new Vector2(0f, 240f), 800f);

            MatchListing listing = Current(flow);
            LobbyChrome.Label(panel, art, listing.Name, false, new Vector2(0f, 150f), 800f);
            LobbyChrome.Label(panel, art, listing.Occupancy + " players", false, new Vector2(0f, 80f), 800f);

            if (hosting)
            {
                if (!listing.IsFull)
                {
                    LobbyChrome.Press(panel, art, "Add bot", new Vector2(0f, -10f), WideButton, addBot);
                }

                LobbyChrome.Press(panel, art, "Start", new Vector2(0f, -120f), WideButton, start);
            }
            else
            {
                LobbyChrome.Label(panel, art, "Waiting for the host to start.", false, new Vector2(0f, -65f), 800f);
            }

            LobbyChrome.Press(panel, art, "Leave", new Vector2(0f, -230f), WideButton, leave);
            Notice(panel, art, flow, new Vector2(0f, -300f));
        }

        private static MatchListing Current(LobbyFlow flow)
        {
            foreach (MatchListing listing in flow.Matches)
            {
                if (listing.Id == flow.Invite.MatchId)
                {
                    return listing;
                }
            }

            return new MatchListing(flow.Invite.MatchId, "Match", flow.Player, 1, 0, 1);
        }

        // One line, in the player's terms. Everything the lobby refuses ends up here,
        // which is why LobbyFailure is an enum: the wording is decided in one place
        // rather than wherever the call happened to fail.
        private static void Notice(RectTransform panel, LobbyArt art, LobbyFlow flow, Vector2 offset)
        {
            if (!flow.HasNotice)
            {
                return;
            }

            TMP_Text line = LobbyChrome.Label(panel, art, Says(flow.Notice), false, offset, 820f);
            if (line != null)
            {
                line.color = new Color(0.90f, 0.35f, 0.25f);
            }
        }

        private static string Says(LobbyFailure failure)
        {
            switch (failure)
            {
                case LobbyFailure.InvalidName:
                    return "That name will not do. Two to sixteen characters.";
                case LobbyFailure.MatchFull:
                    return "That match filled up. Pick another.";
                case LobbyFailure.MatchAlreadyStarted:
                    return "That match started without you.";
                case LobbyFailure.MatchNotFound:
                    return "That match is gone.";
                case LobbyFailure.NoCapacity:
                    return "No room for another match right now. Try again shortly.";
                case LobbyFailure.TooManyMatches:
                    return "You are already hosting. Close that one first.";
                case LobbyFailure.RateLimited:
                    return "Too fast. Wait a moment.";
                case LobbyFailure.NotEnoughPlayers:
                    return "Nobody has joined yet.";
                case LobbyFailure.ClientTooOld:
                case LobbyFailure.InvalidVersion:
                    return "This build is too old for the lobby. Restart to update.";
                case LobbyFailure.Unreachable:
                    return "No answer from the lobby.";
                case LobbyFailure.InvalidPlayerCount:
                    return "That many players is not a match.";
                case LobbyFailure.Unauthorized:
                    return "The lobby did not take your word for that.";
                case LobbyFailure.InvalidRequest:
                    return "The lobby refused that. Check your name and try again.";
                default:
                    return "The lobby said something this build did not understand.";
            }
        }
    }
}
