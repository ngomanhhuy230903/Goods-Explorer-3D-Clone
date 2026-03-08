public enum GameState
{
    None = -1,  // ← sentinel, tránh Init bị block bởi default value = 0
    Init,
    Menu,
    LoadLevel,
    Play,
    Pause,
    Win,
    Lose
}