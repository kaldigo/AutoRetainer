namespace AutoRetainer.Modules;

internal static class PolicyStandalone
{
    // Ventures use the native scheduler. Item-moving requests may continue, but
    // no missing policy, detached character or new profile permits consumption.
    public static bool Allows(string action,bool ordinaryRetainerSource=true)
        =>action is "Entrust" or "Gil"||action=="Retrieve"&&ordinaryRetainerSource;
    public static bool VisitManualRetainer(bool selected,bool ventureDue,bool withdrawGil,bool strategyDue)
        =>selected&&(ventureDue||withdrawGil||strategyDue);
    public static bool VentureDue(long remaining,int compensation,uint venture,bool quick,bool planned)
        =>remaining<=compensation&&(venture!=0||quick||planned);
    public static bool FinishesSoon(long remaining)=>remaining<5*60;
}
