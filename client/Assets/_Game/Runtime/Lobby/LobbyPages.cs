using System;
using Blastlands.Core;
using Blastlands.Core.Lobby;
using Blastlands.Core.Update;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Blastlands.Runtime
{
    // One method per screen, each building the whole screen from scratch. The lobby
    // redraws a handful of rows on an event, not a frame, so rebuilding is simpler than
    // keeping widgets in sync and cannot drift from the state it is showing.
    public static class LobbyPages
    {
        private static readonly Vector2 WideButton = new Vector2(420f, 90f);
        private static readonly Vector2 RowSize = new Vector2(760f, 84f);
        private static readonly Vector2 ArrowButton = new Vector2(90f, 80f);
        private const float RosterCentre = -470f;
        private const float ListCentre = 300f;

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

        public static void Footer(RectTransform root, LobbyArt art, UpdateBadge badge, Action install)
        {
            TMP_Text version = LobbyChrome.Label(root, art, badge.Version, false, Vector2.zero, 320f);
            version.fontSize = 28f;
            version.alignment = TextAlignmentOptions.MidlineRight;
            Corner(version.rectTransform, new Vector2(-28f, 20f));

            if (!badge.Offers)
            {
                return;
            }

            Button press = LobbyChrome.Press(root, art, badge.Action, Vector2.zero, new Vector2(440f, 72f), install);
            press.interactable = badge.Pressable;
            Corner((RectTransform)press.transform, new Vector2(-28f, 72f));

            TMP_Text label = press.GetComponentInChildren<TMP_Text>(true);
            label.fontSize = 34f;

            var mark = new GameObject("Update icon", typeof(RectTransform), typeof(Image));
            mark.transform.SetParent(press.transform, false);

            var rect = (RectTransform)mark.transform;
            rect.anchorMin = new Vector2(0f, 0.5f);
            rect.anchorMax = new Vector2(0f, 0.5f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = new Vector2(18f, 0f);
            rect.sizeDelta = new Vector2(40f, 40f);

            Image icon = mark.GetComponent<Image>();
            icon.sprite = UpdateIcon.Sprite;
            icon.color = new Color(1f, 1f, 1f, badge.Pressable ? 1f : 0.5f);
            icon.raycastTarget = false;
        }

        private static void Corner(RectTransform rect, Vector2 offset)
        {
            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(1f, 0f);
            rect.anchoredPosition = offset;
        }

        public static void Browse(
            RectTransform root,
            LobbyArt art,
            LobbyFlow flow,
            CharacterKind picked,
            Texture portrait,
            GameMode mode,
            Action<CharacterKind> pick,
            Action<GameMode> pickMode,
            Action<MatchListing> join,
            Action create)
        {
            RectTransform panel = LobbyChrome.Panel(root, art, "Browse", new Vector2(1500f, 900f));
            LobbyChrome.Label(panel, art, "MATCHES", true, new Vector2(ListCentre, 390f), RowSize.x);
            Roster(panel, art, picked, portrait, pick);
            Modes(panel, art, mode, pickMode, new Vector2(ListCentre, -245f));
            LobbyChrome.Press(panel, art, "Host a match", new Vector2(ListCentre, -330f), WideButton, create);
            Notice(panel, art, flow, new Vector2(0f, -405f));

            if (flow.Matches.Count == 0)
            {
                LobbyChrome.Label(panel, art, "Nobody is hosting. Be the first.", false, new Vector2(ListCentre, 0f), 680f);
                return;
            }

            float top = 260f;
            for (int i = 0; i < flow.Matches.Count && i < 5; i++)
            {
                MatchListing listing = flow.Matches[i];
                var offset = new Vector2(ListCentre, top - i * (RowSize.y + 12f));
                string text = listing.Name + "   " + listing.Host + "   " + Named(listing.Mode) + "   " + listing.Occupancy;

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

        private static void Modes(RectTransform panel, LobbyArt art, GameMode mode, Action<GameMode> pick, Vector2 at)
        {
            LobbyChrome.Press(panel, art, "<", at + new Vector2(-250f, 0f), ArrowButton, () => pick(GameModeTokens.Step(mode, -1)));
            LobbyChrome.Label(panel, art, "Mode: " + Named(mode), false, at, 400f);
            LobbyChrome.Press(panel, art, ">", at + new Vector2(250f, 0f), ArrowButton, () => pick(GameModeTokens.Step(mode, 1)));
        }

        public static string Named(GameMode mode)
        {
            switch (mode)
            {
                case GameMode.Classic:
                    return "Classic";
                case GameMode.ClassicBlinded:
                    return "Classic Blinded";
                default:
                    return "Arena";
            }
        }

        private static void Roster(
            RectTransform panel, LobbyArt art, CharacterKind picked, Texture portrait, Action<CharacterKind> pick)
        {
            CharacterKind shown = CharacterKits.Shown(picked);

            LobbyChrome.Portrait(panel, portrait, new Vector2(RosterCentre, 60f), new Vector2(360f, 540f));

            LobbyChrome.Press(panel, art, "<", new Vector2(RosterCentre - 240f, -270f), ArrowButton,
                () => pick(CharacterKits.Step(shown, -1)));
            LobbyChrome.Label(panel, art, shown.ToString(), true, new Vector2(RosterCentre, -270f), 330f);
            LobbyChrome.Press(panel, art, ">", new Vector2(RosterCentre + 240f, -270f), ArrowButton,
                () => pick(CharacterKits.Step(shown, 1)));

            TMP_Text about = LobbyChrome.Label(panel, art, Describes(shown), false, new Vector2(RosterCentre, -355f), 460f);
            if (about != null)
            {
                about.fontSize = 30f;
                about.rectTransform.sizeDelta = new Vector2(460f, 110f);
            }
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
            LobbyChrome.Label(panel, art, Named(listing.Mode) + "   " + listing.Occupancy + " players", false, new Vector2(0f, 80f), 800f);

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

            return new MatchListing(flow.Invite.MatchId, "Match", flow.Player, 1, 0, 1, flow.Invite.Mode);
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
