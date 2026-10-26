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

internal static class PolicyDialog
{
    internal static bool Matches(ushort owner,ushort parent,ushort host) => owner!=0 && (parent==owner || host==owner);
}

internal sealed class PolicyCommitGate
{
    internal bool Submitted { get; private set; }
    internal void Reset()=>Submitted=false;
    internal bool Submit(bool permitted)
    {
        if(Submitted || !permitted)return false;
        Submitted=true;return true;
    }
}

internal enum PolicyAck { Waiting, Completed, Failed }
internal static class PolicyTransferAcknowledgement
{
    internal static PolicyAck Check(PolicyAck source,long? before,long? after,uint quantity)
    {
        if(source==PolicyAck.Failed||before==null||after==null||quantity==0||after<before||after>before+quantity)return PolicyAck.Failed;
        return source==PolicyAck.Completed && after==before+quantity?PolicyAck.Completed:PolicyAck.Waiting;
    }
}
internal static class PolicyAcknowledgement
{
    internal static PolicyAck Check(long before,long? current,bool dialogVisible,long submitted,long now)
    {
        if(before<=0 || current==null || now<submitted || now-submitted>10000)return PolicyAck.Failed;
        if(current==before-1 && !dialogVisible)return PolicyAck.Completed;
        if(current!=before && current!=before-1)return PolicyAck.Failed;
        return PolicyAck.Waiting;
    }
}
