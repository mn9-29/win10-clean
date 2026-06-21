namespace WinForge.Engine
{
    public class EngineItem
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public string Desc { get; set; }
        public string Category { get; set; }   // one of: apps, privacy, services, gaming, performance, network, updates, ui, maintenance, cleanup, system, install
        public bool Work { get; set; }
        public bool Gaming { get; set; }
        public bool Basic { get; set; }
        public string[] Commands { get; set; }
    }
}
