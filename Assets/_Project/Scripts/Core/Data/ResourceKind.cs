namespace Nucleaire.Data
{
    /// <summary>Nature physique d'une ressource : détermine où elle est stockée et quel transmetteur la transporte.</summary>
    public enum ResourceKind
    {
        Item,   // solide, compté en unités
        Fluid,  // liquide, en mB
        Gas,    // gaz, en mB
        Energy, // énergie, en J
    }

    public static class ResourceKindExtensions
    {
        public static string Unit(this ResourceKind kind) => kind switch
        {
            ResourceKind.Item => "",
            ResourceKind.Fluid => " mB",
            ResourceKind.Gas => " mB",
            ResourceKind.Energy => " J",
            _ => "",
        };

        public static string DisplayName(this ResourceKind kind) => kind switch
        {
            ResourceKind.Item => "Solide",
            ResourceKind.Fluid => "Liquide",
            ResourceKind.Gas => "Gaz",
            ResourceKind.Energy => "Énergie",
            _ => kind.ToString(),
        };
    }
}
