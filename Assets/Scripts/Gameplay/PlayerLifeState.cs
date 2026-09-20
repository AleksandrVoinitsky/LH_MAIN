namespace LH.Main.Unity.Gameplay
{
    public enum PlayerLifeState { Alive, Wounded, Dead, Extracted, Disconnected }

    public static class PlayerLifeStateExtensions
    {
        public static bool IsTerminal(this PlayerLifeState state)
        {
            return state == PlayerLifeState.Dead || state == PlayerLifeState.Extracted || state == PlayerLifeState.Disconnected;
        }
    }
}
