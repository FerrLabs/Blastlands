namespace Blastlands.Core
{
    // How far ahead a bot thinks when it judges a tile. This is what separates the
    // skill levels now that every level escapes a blast it is standing in: reaction
    // delay stopped telling them apart the day the escape window landed, because a bot
    // that plans its way out does not need to be quick about it.
    public enum BotPlanning : byte
    {
        // A tile is fine if the fire has not reached it by the time the bot arrives.
        // That is how a bot walks into a pocket with one mouth and stands there while
        // its own bomb closes it.
        OnArrival,

        // The escape window, but only while fleeing. Errands are still taken on arrival
        // safety, so this one still corners itself on the way to a power-up.
        WhenFleeing,

        // The window everywhere. Every tile it walks to, for any reason, is one it can
        // still leave.
        Always
    }
}
