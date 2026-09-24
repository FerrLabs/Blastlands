using System;
using System.Collections;
using System.Collections.Generic;
using Blastlands.Core;
using Blastlands.Core.Lobby;
using Blastlands.Core.Net;
using Blastlands.Core.Update;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Blastlands.Runtime
{
    // The lobby, on screen. Holds a LobbyFlow, renders whichever screen it is on, and
    // turns what the player does into calls on LobbyClient.
    //
    // Two lanes. What the player asked for runs at once, and a second press while it
    // is still in flight is dropped: hammering Join would otherwise queue a dozen and
    // act on whichever came back last, and the lobby throttles per address anyway.
    // The list refresh and the status poll run in the other lane and give way, because
    // a press landing in the three seconds between refreshes must not be swallowed.
    public sealed class LobbyScreens : MonoBehaviour
    {
        private const string MatchScene = "Match";

        // Never derived from the player's name. The lobby's DisplayName rule (2 to 16
        // characters, letters, digits, space, hyphen, underscore) is the player's own
        // name budget already spent, so appending anything to it can overflow the same
        // limit, and a possessive apostrophe is outside the character set regardless of
        // length. The host's name is shown next to this one in the match list, so
        // nothing personal is lost by keeping this fixed.
        private const string DefaultMatchName = "Match";
        private const float SecondsBetweenStatusChecks = 1.5f;

        [SerializeField] private LobbyArt art;
        [SerializeField] private int maxPlayers = 4;

        private readonly LobbyFlow flow = new LobbyFlow();
        private LobbyClient lobby;
        private VersionGate gate;
        private ClientUpdater updater;
        private UpdateBadge badge;
        private CharacterStage stage;
        private Canvas canvas;
        private RectTransform root;
        private LobbyScreen drawn = LobbyScreen.Play;
        private bool dirty = true;
        private bool busy;
        private bool polling;
        private int generation;
        private float sinceStatus;

        private void Awake()
        {
            if (art == null || !art.Complete)
            {
                Debug.LogError("Blastlands lobby: no art assigned, so there is nothing to draw the screens with.");
                enabled = false;
                return;
            }

            lobby = GetComponent<LobbyClient>();
            if (lobby == null)
            {
                lobby = gameObject.AddComponent<LobbyClient>();
            }

            // Told where the lobby is every time, including when the component came
            // with the scene. Its serialized address is a developer's convenience, and
            // leaving it in charge means a build ignores --lobby and the environment
            // and quietly dials whatever was saved in the scene.
            lobby.Use(ClientOptions.Lobby(Environment.GetCommandLineArgs()));

            badge = UpdateBadge.For(Application.version, UpdateVerdict.Unknown, UpdateStage.Idle, default);
            CharacterChoice.Choose(CharacterKits.Shown(CharacterChoice.Current));
            stage = new GameObject("Character stage").AddComponent<CharacterStage>();
            stage.transform.SetParent(transform, false);
            stage.Show(art.Models, CharacterChoice.Current);

            var backdrop = new GameObject("Backdrop").AddComponent<LobbyBackdrop>();
            backdrop.transform.SetParent(transform, false);
            backdrop.Build(art, Camera.main);

            BuildCanvas();
        }

        private void Update()
        {
            WatchBuild();

            if (flow.Screen == LobbyScreen.Play)
            {
                Play();
                return;
            }

            if (flow.ShouldRefresh(Time.unscaledDeltaTime))
            {
                Poll(Listing());
            }

            if (flow.Screen == LobbyScreen.Host || flow.Screen == LobbyScreen.Wait)
            {
                sinceStatus += Time.unscaledDeltaTime;
                if (sinceStatus >= SecondsBetweenStatusChecks)
                {
                    sinceStatus = 0f;
                    Poll(Watching());
                }
            }

            if (dirty || drawn != flow.Screen)
            {
                Draw();
            }
        }

        private void Play()
        {
            MatchHandoff.Leave(flow.Invite);
            enabled = false;
            SceneManager.LoadScene(MatchScene);
        }

        private void Draw()
        {
            for (int i = root.childCount - 1; i >= 0; i--)
            {
                Destroy(root.GetChild(i).gameObject);
            }

            switch (flow.Screen)
            {
                case LobbyScreen.Name:
                    LobbyPages.Name(root, art, flow, Named);
                    break;
                case LobbyScreen.Browse:
                    LobbyPages.Browse(root, art, flow, CharacterChoice.Current, stage.Texture, ModeChoice.Current, Pick, PickMode, Join, Create);
                    break;
                case LobbyScreen.Host:
                    LobbyPages.Room(root, art, flow, true, Begin, AddBot, Leave);
                    break;
                case LobbyScreen.Wait:
                    LobbyPages.Room(root, art, flow, false, null, null, Leave);
                    break;
            }

            LobbyPages.Footer(root, art, badge, InstallUpdate);

            drawn = flow.Screen;
            dirty = false;
        }

        private void WatchBuild()
        {
            if (gate == null)
            {
                gate = FindAnyObjectByType<VersionGate>();
                updater = FindAnyObjectByType<ClientUpdater>();
            }

            UpdateBadge next = UpdateBadge.For(
                Application.version,
                gate == null ? UpdateVerdict.Unknown : gate.Verdict,
                updater == null ? UpdateStage.Idle : updater.Stage,
                gate == null ? default : gate.Release);

            if (next.Action != badge.Action || next.Pressable != badge.Pressable)
            {
                badge = next;
                Redraw();
            }
        }

        private void InstallUpdate()
        {
            if (gate != null && updater != null && badge.Pressable)
            {
                StartCoroutine(updater.Apply(gate.Release));
            }
        }

        private void Named(string typed)
        {
            Redraw();
            if (flow.Named(typed))
            {
                Asked(Listing());
            }
        }

        private void Pick(CharacterKind character)
        {
            CharacterChoice.Choose(character);
            stage.Show(art.Models, character);
            Redraw();
        }

        private void PickMode(GameMode mode)
        {
            ModeChoice.Choose(mode);
            Redraw();
        }

        private void Create()
        {
            Asked(Creating());
        }

        private void Join(MatchListing listing)
        {
            Asked(Joining(listing));
        }

        // Never Start: Unity calls any method with that exact signature as the
        // MonoBehaviour message of the same name, whether or not it means to override
        // it. Named that, this fired on its own the moment the lobby loaded, posting
        // StartMatch with an empty invite before the player had done anything.
        private void Begin()
        {
            Asked(Starting());
        }

        private void AddBot()
        {
            Asked(AddingBot());
        }

        private void Leave()
        {
            flow.Left();
            Redraw();
            Asked(Listing());
        }

        private IEnumerator Listing()
        {
            int mine = generation;

            yield return lobby.List(result =>
            {
                if (Stale(mine))
                {
                    return;
                }

                if (result.Ok)
                {
                    flow.Listed(result.Value);
                }
                else
                {
                    flow.Refused(result.Failure);
                }

                Redraw();
            });
        }

        private IEnumerator Creating()
        {
            yield return lobby.Create(DefaultMatchName, flow.Player, maxPlayers, CharacterChoice.Current, ModeChoice.Current, result =>
            {
                if (result.Ok && result.Value.CanStart)
                {
                    flow.Created(result.Value.Invite, result.Value.HostTicket);
                    flow.Listed(new List<MatchListing> { result.Value.Listing });
                }
                else
                {
                    flow.Refused(result.Ok ? LobbyFailure.Unreadable : result.Failure);
                }

                Redraw();
            });
        }

        private IEnumerator Joining(MatchListing listing)
        {
            yield return lobby.Join(listing.Id, flow.Player, CharacterChoice.Current, listing.Mode, result =>
            {
                if (result.Ok && result.Value.CanConnect)
                {
                    flow.Joined(result.Value);
                }
                else
                {
                    flow.Refused(result.Ok ? LobbyFailure.Unreadable : result.Failure, listing.Id);
                }

                Redraw();
            });
        }

        private IEnumerator Starting()
        {
            yield return lobby.StartMatch(flow.Invite.MatchId, flow.HostTicket, result =>
            {
                if (result.Ok)
                {
                    flow.Running();
                }
                else
                {
                    flow.Refused(result.Failure, flow.Invite.MatchId);
                }

                Redraw();
            });
        }

        private IEnumerator AddingBot()
        {
            yield return lobby.AddBot(flow.Invite.MatchId, flow.HostTicket, result =>
            {
                if (result.Ok)
                {
                    flow.Listed(new List<MatchListing> { result.Value.Listing });
                }
                else
                {
                    flow.Refused(result.Failure, flow.Invite.MatchId);
                }

                Redraw();
            });
        }

        // The host presses start on their own machine. Everybody else finds out here,
        // and a match that vanished while they waited sends them back to the list
        // rather than leaving them on a screen that will never change.
        private IEnumerator Watching()
        {
            string id = flow.Invite.MatchId;
            int mine = generation;

            yield return lobby.Status(id, result =>
            {
                if (Stale(mine))
                {
                    return;
                }

                if (result.Ok)
                {
                    flow.Listed(new List<MatchListing> { result.Value.Listing });
                    if (result.Value.Running)
                    {
                        flow.Running();
                    }
                }
                else if (!result.WorthRetrying)
                {
                    flow.Refused(result.Failure, id);
                }

                Redraw();
            });
        }

        private void Asked(IEnumerator work)
        {
            if (busy)
            {
                return;
            }

            // A poll in flight answers a question about the screen the player has just
            // left, so its answer is dropped when it lands rather than the coroutine
            // being stopped: stopping one skips the using in LobbyClient, which leaves
            // the request undisposed.
            generation++;

            busy = true;
            StartCoroutine(Once(work));
        }

        private void Poll(IEnumerator work)
        {
            if (busy || polling)
            {
                return;
            }

            polling = true;
            StartCoroutine(Polled(work));
        }

        private IEnumerator Once(IEnumerator work)
        {
            yield return work;
            busy = false;
        }

        private IEnumerator Polled(IEnumerator work)
        {
            yield return work;
            polling = false;
        }

        private bool Stale(int mine)
        {
            return mine != generation;
        }

        private void Redraw()
        {
            dirty = true;
        }

        private void BuildCanvas()
        {
            var host = new GameObject("Lobby Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            host.transform.SetParent(transform, false);

            canvas = host.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = host.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            LobbyBackdrop.Shade(host.transform);

            root = LobbyChrome.Stretch(new GameObject("Screen", typeof(RectTransform)), 0f);
            root.SetParent(host.transform, false);
        }
    }
}
