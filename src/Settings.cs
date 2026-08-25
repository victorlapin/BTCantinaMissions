namespace BTCantinaMissions
{
    public class Settings
    {
        public string PlanetTag = "planet_other_cantina";
        public int SlotsPerBoard = 4;
        public BoardRefreshType BoardRefresh = BoardRefreshType.Monthly;
        public int MaxActiveTasks = 3;
        public bool NotifyOnProgress = true;
        public bool NotifyOnReady = true;
        public bool DebugLogging = false;
        public bool DumpStateOnSave = false;
    }

    public enum BoardRefreshType
    {
        Monthly
    }
}