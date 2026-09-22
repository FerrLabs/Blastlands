using System.Collections.Generic;

namespace Blastlands.Core.Lobby
{
    public enum LobbyScreen : byte
    {
        Name,
        Browse,
        Host,
        Wait,
        Play
    }

    // Which screen the player is on and how every answer from the lobby moves them.
    //
    // Engine-free so the rules can be tested without a scene, and so the screens stay
    // what they should be: prefabs that render this and call back into it. A match that
    // was listed a second ago and is full now is the ordinary case here, not an error
    // path bolted on: a refusal that means the listing went stale drops that row and
    // returns to the list rather than leaving the player waiting on a match that will
    // never start.
    public sealed class LobbyFlow
    {
        public const float SecondsBetweenRefreshes = 3f;

        private readonly List<MatchListing> matches = new List<MatchListing>();
        private float sinceRefresh;

        public LobbyScreen Screen { get; private set; } = LobbyScreen.Name;

        public string Player { get; private set; } = string.Empty;

        public IReadOnlyList<MatchListing> Matches
        {
            get { return matches; }
        }

        public MatchInvite Invite { get; private set; }

        // Proves to the lobby that this client is the one that created the match, which
        // is what starting it takes. Not the same ticket as the invite's: that one is
        // for the game server.
        public string HostTicket { get; private set; } = string.Empty;

        public LobbyFailure Notice { get; private set; }

        public bool HasNotice { get; private set; }

        public bool Named(string raw)
        {
            if (!DisplayName.IsAcceptable(raw))
            {
                Complain(LobbyFailure.InvalidName);
                return false;
            }

            Player = DisplayName.Clean(raw);
            Clear();
            Move(LobbyScreen.Browse);
            return true;
        }

        public void Listed(IReadOnlyList<MatchListing> listed)
        {
            matches.Clear();
            if (listed != null)
            {
                matches.AddRange(listed);
            }

            sinceRefresh = 0f;
        }

        public void Created(MatchInvite invite, string hostTicket)
        {
            Invite = invite;
            HostTicket = hostTicket ?? string.Empty;
            Clear();
            Move(LobbyScreen.Host);
        }

        public void Joined(MatchInvite invite)
        {
            Invite = invite;
            HostTicket = string.Empty;
            Clear();
            Move(LobbyScreen.Wait);
        }

        // The match is running: the host pressed start, or the status poll saw it. Only
        // worth anything with an invite in hand, because the endpoint and the ticket are
        // what connecting takes.
        public bool Running()
        {
            if (!Invite.CanConnect || (Screen != LobbyScreen.Host && Screen != LobbyScreen.Wait))
            {
                return false;
            }

            Move(LobbyScreen.Play);
            return true;
        }

        public void Refused(LobbyFailure failure)
        {
            Refused(failure, null);
        }

        public void Refused(LobbyFailure failure, string matchId)
        {
            Complain(failure);

            if (!string.IsNullOrEmpty(matchId) && LobbyFailures.MeansTheListingWentStale(failure))
            {
                Forget(matchId);
            }

            if (failure == LobbyFailure.InvalidName)
            {
                Move(LobbyScreen.Name);
                return;
            }

            // A match that filled up, vanished or started without us is not somewhere to
            // wait: the list is. Everything else leaves the player where they are, with
            // the notice, because retrying from the same screen is the sane answer.
            if (LobbyFailures.MeansTheListingWentStale(failure) && Screen != LobbyScreen.Name)
            {
                // The invite goes with it. A match that filled up or vanished is not
                // one to dial, and a screen that hands the invite over on its way into
                // a match would do exactly that.
                Forget();
                Move(LobbyScreen.Browse);
            }
        }

        public void Left()
        {
            Forget();
            Clear();
            Move(LobbyScreen.Browse);
        }

        // Ticked by whatever drives the screen. The list goes stale on its own, since
        // matches are created and filled by other people.
        public bool ShouldRefresh(float seconds)
        {
            if (Screen != LobbyScreen.Browse)
            {
                return false;
            }

            sinceRefresh += seconds;
            if (sinceRefresh < SecondsBetweenRefreshes)
            {
                return false;
            }

            sinceRefresh = 0f;
            return true;
        }

        public void Clear()
        {
            HasNotice = false;
        }

        private void Forget()
        {
            Invite = default;
            HostTicket = string.Empty;
        }

        private void Forget(string matchId)
        {
            for (int i = matches.Count - 1; i >= 0; i--)
            {
                if (matches[i].Id == matchId)
                {
                    matches.RemoveAt(i);
                }
            }
        }

        private void Complain(LobbyFailure failure)
        {
            Notice = failure;
            HasNotice = true;
        }

        private void Move(LobbyScreen screen)
        {
            Screen = screen;
            sinceRefresh = 0f;
        }
    }
}
