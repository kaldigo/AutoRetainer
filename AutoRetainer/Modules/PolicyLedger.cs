namespace AutoRetainer.Modules;

internal sealed class PolicyLedger
{
    public PolicyLedger(){}
    [System.Text.Json.Serialization.JsonRequired]
    public int Version { get; set; }=1;
    [System.Text.Json.Serialization.JsonRequired]
    public Dictionary<ulong,string> Owners { get; set; }=[];
    [System.Text.Json.Serialization.JsonRequired]
    public HashSet<ulong> Faults { get; set; }=[];
    [System.Text.Json.Serialization.JsonRequired]
    public HashSet<ulong> Pending { get; set; }=[];
    internal void Validate()
    {
        if(Version!=1 || Owners==null || Faults==null || Pending==null ||
            Owners.Any(p=>p.Key==0||p.Value!="Steward.InventoryPolicy.Evaluate") || Faults.Contains(0) || Pending.Contains(0))
            throw new System.IO.InvalidDataException("Invalid Steward policy ledger; automation cannot start");
    }
}
