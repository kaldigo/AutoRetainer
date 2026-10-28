namespace AutoRetainer.Modules;

internal static class PolicyStorageRouting
{
    public static bool OwnsCompletedRun(ulong character,ulong owner,long capturedRevision,long currentRevision,bool enabled)=>
        character!=0&&character==owner&&capturedRevision==currentRevision&&enabled;
    public static bool UniqueFits(bool unique,long? stored)=>!unique||stored==0;
    public static bool Allows(ulong currencyRetainer,ulong target,bool equipment,bool marketable,uint category)
    {
        if(currencyRetainer==0)return true;
        if(target==0)return false;
        var housing=category is 57 or >=64 and <=80 or 82 or 95;
        var currencyItem=!equipment&&!housing&&(!marketable||category==100);
        return currencyItem?target==currencyRetainer:target!=currencyRetainer;
    }
    public static bool Selects(bool explicitItem,int itemKeep,int? categoryKeep,long carried,bool duplicates,bool multiStack,long? stored,uint stackSize)
    {
        if(explicitItem||categoryKeep.HasValue)return carried>System.Math.Max(0,explicitItem?itemKeep:categoryKeep.Value);
        return duplicates&&stored>0&&(multiStack||stored<stackSize);
    }
}
