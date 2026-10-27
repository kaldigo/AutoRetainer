using AutoRetainer.Modules;
using AutoRetainerAPI.Configuration;
using System.Diagnostics;

static class SettingsChecks
{
    public static void Run(Action<bool,string> check)
    {
        var plan=new InventoryManagementSettings();var defaults=new InventoryManagementSettings();
        foreach(var field in typeof(InventoryManagementSettings).GetFields())
        {
            if(field.FieldType==typeof(bool))field.SetValue(plan,!(bool)field.GetValue(plan)!);
            else if(field.FieldType==typeof(int))field.SetValue(plan,1337);
            else if(field.FieldType==typeof(string))field.SetValue(plan,"Preserve settings");
            else if(field.GetValue(plan) is List<uint> list)list.Add(42);
            else if(field.GetValue(plan) is HashSet<uint> set)set.Add(42);
        }
        plan.IMAutoVendorHard=Enumerable.Range(1,22766).Select(i=>(uint)i).ToList();
        check(ReferenceEquals(plan,CompanionSettings.WithInheritedProtection(plan,defaults)),"Empty inherited protection preserves original plan fast path");
        defaults.IMProtectList.AddRange([4868,42]);
        var effective=CompanionSettings.WithInheritedProtection(plan,defaults);
        check(effective.IMProtectList.SequenceEqual(new uint[]{42,4868}),"Inherited and local protection are combined without duplicates");
        check(plan.IMProtectList.SequenceEqual(new uint[]{42})&&defaults.IMProtectList.SequenceEqual(new uint[]{4868,42}),"Resolving protection does not mutate saved lists");
        foreach(var field in typeof(InventoryManagementSettings).GetFields().Where(f=>f.Name!="IMProtectList"))
            check(Equals(field.GetValue(plan),field.GetValue(effective)),"Effective plan preserves field "+field.Name);
        defaults.IMProtectList.Add(99);
        check(CompanionSettings.WithInheritedProtection(plan,defaults).IMProtectList.Contains(99),"New global protection is immediately visible");
        plan.IMProtectList.Add(100);defaults.IMProtectList.Remove(4868);
        effective=CompanionSettings.WithInheritedProtection(plan,defaults);
        check(effective.IMProtectList.Contains(100)&&!effective.IMProtectList.Contains(4868),"Local additions and global removals are immediately visible");
        plan.IMDry=true;plan.IMAutoVendorHardStackLimit=7;
        effective=CompanionSettings.WithInheritedProtection(plan,defaults);
        check(effective.IMDry&&effective.IMAutoVendorHardStackLimit==7,"Demo mode and stack limits are read live");
        var before=GC.GetAllocatedBytesForCurrentThread();var timer=Stopwatch.StartNew();
        for(var i=0;i<1000;i++)CompanionSettings.WithInheritedProtection(plan,defaults);
        var allocated=GC.GetAllocatedBytesForCurrentThread()-before;
        check(allocated<4*1024*1024,"1000 protection resolutions must not clone 22766 candidate IDs each time");
        Console.WriteLine($"1000 managed protection reads, 22766 candidates: {timer.Elapsed.TotalMilliseconds:F1} ms; {allocated:N0} bytes allocated (standalone test).");
    }
}
