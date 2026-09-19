namespace LH.Main.Unity.Server
{
    public enum GameServerState
    {
        Idle,
        Reserved,
        Running,
        Failed
    }

    public static class GameServerStateJson
    {
        public static string ToJsonValue(GameServerState state)
        {
            return state switch
            {
                GameServerState.Idle => "idle",
                GameServerState.Reserved => "reserved",
                GameServerState.Running => "running",
                GameServerState.Failed => "failed",
                _ => "failed"
            };
        }
    }
}
