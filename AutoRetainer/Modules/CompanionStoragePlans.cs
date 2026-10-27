using ECommons.Configuration;
using FFXIVClientStructs.FFXIV.Client.Game;
using Newtonsoft.Json;

namespace AutoRetainer.Modules;

internal static partial class CompanionPolicy
{
    private static readonly Dictionary<(ulong,uint),long> entrustSkips=[];
    private static unsafe bool UniqueEntrustAllowed(ulong retainer,uint id,bool report=false)
    {
        if(!Svc.Data.GetExcelSheet<Lumina.Excel.Sheets.Item>().TryGetRow(id,out var item))return false;
        if(!item.IsUnique)return true;
        var selected=RetainerManager.Instance();long? copies=null;
        if(selected!=null&&selected->LastSelectedRetainerId==retainer)
        {
            var nq=CountStored(id,false);var hq=CountStored(id,true);
            if(nq.HasValue&&hq.HasValue)copies=nq.Value+hq.Value;
        }
        if(PolicyStorageRouting.UniqueFits(true,copies))return true;
        if(report&&copies>0)
        {
            var key=(retainer,id);var now=Environment.TickCount64;
            if(!entrustSkips.TryGetValue(key,out var last)||now-last>=60000)
            {
                entrustSkips[key]=now;
                var message=$"Skipped entrusting {item.Name}: this retainer already owns the unique item. Continuing with other items.";
                Svc.Log.Warning(message);Svc.Chat.Print("[AutoRetainer] "+message);
            }
        }
        return false;
    }
    private static bool MigrateStorageRouting(ulong cid,ulong legacyCurrencyRetainer)
    {
        if(!Connected(cid))return false;
        if(C.CompanionCurrencyRetainers.ContainsKey(cid))return true;
        if(legacyCurrencyRetainer!=0&&!C.OfflineData.Any(c=>c.CID==cid&&c.RetainerData.Any(r=>r.RetainerID==legacyCurrencyRetainer)))return false;
        C.CompanionCurrencyRetainers.Add(cid,legacyCurrencyRetainer);
        try{EzConfig.Save();return true;}
        catch{C.CompanionCurrencyRetainers.Remove(cid);throw;}
    }
    private static bool StorageScopeAllows(ulong cid,ulong retainer,uint id)
    {
        var currency=C.CompanionCurrencyRetainers.GetValueOrDefault(cid);
        if(currency==0)return true;
        if(!Svc.Data.GetExcelSheet<Lumina.Excel.Sheets.Item>().TryGetRow(id,out var item))return false;
        return PolicyStorageRouting.Allows(currency,retainer,item.EquipSlotCategory.RowId!=0,!item.IsUntradable&&item.ItemSearchCategory.RowId!=0,item.ItemUICategory.RowId);
    }
    // Read-only query for companion overflow. Ordinary entrust already has a native
    // selected plan; it must not be judged against a second category assignment.
    private static unsafe bool StoragePlanAllows(ulong cid,string request)
    {
        if(cid==0||cid!=Svc.ClientState.LocalContentId)return false;
        var source=JsonConvert.DeserializeObject<Retrieval>(request);
        if(source==null||source.Retainer==0||source.Quantity==0||!C.EnableEntrustManager||C.CompanionGilOnlyRetainers.Contains(source.Retainer))return false;
        var owner=C.OfflineData.FirstOrDefault(c=>c.CID==cid);
        var retainer=owner?.RetainerData.FirstOrDefault(r=>r.RetainerID==source.Retainer);
        if(retainer==null)return false;
        if(!C.AdditionalData.TryGetValue(Utils.GetAdditionalDataKey(cid,retainer.Name,false),out var extra))return false;
        var planId=extra.EntrustPlan;
        var plan=C.EntrustPlans.FirstOrDefault(p=>p.Guid==planId);
        if(plan==null||plan.ManualPlan||!StorageScopeAllows(cid,source.Retainer,source.ItemId))return false;
        if(!UniqueEntrustAllowed(source.Retainer,source.ItemId))return false;
        if(!Svc.Data.GetExcelSheet<Lumina.Excel.Sheets.Item>().TryGetRow(source.ItemId,out var item))return false;
        var category=plan.EntrustCategories.FirstOrDefault(c=>c.ID==item.ItemUICategory.RowId);
        var explicitItem=plan.EntrustItems.Contains(source.ItemId);
        if(!explicitItem&&category==null&&!plan.Duplicates)return false;
        var allowed=plan.GetAllowedInventories();
        if(!allowed.Contains((InventoryType)source.Container))return false;
        var manager=InventoryManager.Instance();if(manager==null)return false;
        foreach(var type in allowed)
        {var inv=manager->GetInventoryContainer(type);if(inv==null||!inv->IsLoaded)return false;}
        var identity=ReadIdentity((InventoryType)source.Container,source.Slot);
        if(identity==null||identity.ItemId!=source.ItemId||identity.Quantity!=source.Quantity||((InventoryItem.ItemFlags)identity.Flags).HasFlag(InventoryItem.ItemFlags.HighQuality)!=source.Hq)return false;
        long? stored=null;
        if(!explicitItem&&category==null&&plan.Duplicates&&RetainerManager.Instance()!=null&&RetainerManager.Instance()->LastSelectedRetainerId==source.Retainer)
            stored=CountStored(source.ItemId,source.Hq);
        return PolicyStorageRouting.Selects(explicitItem,plan.EntrustItemsAmountToKeep.GetValueOrDefault(source.ItemId),category?.AmountToKeep,
            Utils.GetItemCount(allowed,source.ItemId),plan.Duplicates,plan.DuplicatesMultiStack,stored,item.StackSize);
    }
}
