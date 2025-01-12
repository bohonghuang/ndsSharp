namespace ndsSharp.Core.Plugins.HGSS;

public class HGSSPlugin : BasePlugin
{
    public override string[] GameCodes => ["IPKE", "IPGE"];
    
    public override PluginFileTypeAssociation[] FileTypeAssociations { get; } =
    [
        new("matrix", pathMatches: ["a/0/4/1"]),
    ];
}