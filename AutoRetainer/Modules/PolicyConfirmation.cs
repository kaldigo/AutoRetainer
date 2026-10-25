namespace AutoRetainer.Modules;

internal sealed record PolicyConfirmation(ulong Character, uint ItemId, string Fingerprint, long Started)
{
    internal bool StillValid(ulong character, uint item, string fingerprint, long now, bool permitted) =>
        permitted && Character!=0 && Character==character && ItemId!=0 && ItemId==item &&
        !string.IsNullOrEmpty(Fingerprint) && Fingerprint==fingerprint && now>=Started && now-Started<=10000;
}

internal static class HandoffLease
{
    internal static bool Owns(ulong owner, ulong connected, ulong current, string callback, long heartbeat, long now, bool locked) =>
        locked && owner!=0 && owner==connected && owner==current && callback=="Steward.InventoryPolicy.Evaluate" &&
        now>=heartbeat && now-heartbeat<=5000;
}
