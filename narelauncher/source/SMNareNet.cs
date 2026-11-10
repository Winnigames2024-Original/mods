namespace NareLauncher.Core
{
    // Минимальный NareNet только для адреса сервера (NareHost использует Where/HostOnly).
    public static class NareNet
    {
        public static string Where
        {
            get { string v = (Settings.Current.NareNetHost ?? "").Trim(); return v.Length == 0 ? "151.245.217.104:2445" : v; }
        }
        public static string HostOnly
        {
            get { string w = Where; int c = w.LastIndexOf(':'); return c > 0 ? w.Substring(0, c) : w; }
        }
    }
}
