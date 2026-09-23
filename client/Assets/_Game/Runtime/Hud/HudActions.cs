using Blastlands.Core;
using UnityEngine;

namespace Blastlands.Runtime
{
    public sealed class HudActions
    {
        private readonly int playerIndex;
        private readonly CanvasGroup group;
        private readonly HudActionButton bomb;
        private readonly HudActionButton dash;
        private readonly HudActionButton shove;
        private readonly HudActionButton ability;
        private readonly HudArt art;
        private CharacterKind dressedAs = CharacterKind.None;

        private HudActions(
            int playerIndex,
            CanvasGroup group,
            HudArt art,
            HudActionButton bomb,
            HudActionButton dash,
            HudActionButton shove,
            HudActionButton ability)
        {
            this.playerIndex = playerIndex;
            this.group = group;
            this.art = art;
            this.bomb = bomb;
            this.dash = dash;
            this.shove = shove;
            this.ability = ability;
        }

        public static HudActions Build(HudArt art, Transform area, int playerIndex, InputDeviceKind device, float inset, float scale)
        {
            RectTransform root = HudPlacement.Area(area, "Actions", new Rect(0f, 0f, 1f, 1f));
            Color accent = MatchPalette.ForPlayer(playerIndex);
            var margin = new Vector2(inset, inset + 34f * scale);

            HudActionButton bomb = HudActionButton.Build(
                art, root, HudAction.Bomb, art.Bombs, device, accent, margin, scale * 1.6f, true);
            HudActionButton dash = HudActionButton.Build(
                art, root, HudAction.Dash, art.Speed, device, accent, margin + new Vector2(262f * scale, 0f), scale * 1.1f, false);
            HudActionButton shove = HudActionButton.Build(
                art, root, HudAction.Shove, art.Shove, device, accent, margin + new Vector2(36f * scale, 262f * scale), scale, false);

            HudActionButton ability = HudActionButton.Build(
                art, root, HudAction.Ability, null, device, accent, margin + new Vector2(298f * scale, 262f * scale), scale, false);
            ability.Reveal(false);

            return new HudActions(playerIndex, root.GetComponent<CanvasGroup>(), art, bomb, dash, shove, ability);
        }

        public void Render(MatchState state)
        {
            PlayerState player = state.Players[playerIndex];
            MatchSettings settings = state.Settings;

            int capacity = Mathf.Max(1, player.CarryCapacity);
            bomb.Show(player.BombsHeld / (float)capacity, player.BombsHeld > 0, player.BombsHeld.ToString());
            dash.Show(Recovered(player.DashCooldownRemaining, settings.DashCooldownTicks + settings.DashTicks), player.DashCooldownRemaining <= 0, null);
            shove.Show(Recovered(player.PushCooldownRemaining, settings.Push.CooldownTicks), player.PushCooldownRemaining <= 0, null);
            RenderAbility(player, settings);

            bool won = state.Outcome == RoundOutcome.Winner && state.WinnerId == player.Id;
            group.alpha = PanelMood.AlphaFor(player.Alive, state.Outcome, won);
        }

        private void RenderAbility(PlayerState player, MatchSettings settings)
        {
            bool has = settings.Rules.AllowsCharacters && Abilities.Has(player.Character);
            if (player.Character != dressedAs)
            {
                dressedAs = player.Character;
                ability.Dress(art.AbilityOf(dressedAs), NameOf(dressedAs));
            }

            ability.Reveal(has);
            if (has)
            {
                int total = settings.Abilities.CooldownFor(player.Character);
                ability.Show(Recovered(player.AbilityCooldownRemaining, total), player.AbilityCooldownRemaining <= 0, null);
            }
        }

        private static string NameOf(CharacterKind character)
        {
            return character == CharacterKind.Demolisher ? "Trigger" : "Ability";
        }

        public static float Recovered(int remaining, int total)
        {
            if (total <= 0 || remaining <= 0)
            {
                return 1f;
            }

            return 1f - Mathf.Clamp01(remaining / (float)total);
        }
    }
}
