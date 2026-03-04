namespace FoodMatch.Core
{
    /// <summary>
    /// Tập trung toàn bộ hằng số của game vào 1 chỗ.
    /// Tránh dùng magic number rải rác khắp codebase.
    /// </summary>
    public static class GameConstants
    {
        // ─── Tray ─────────────────────────────────────────────────────────────
        public const int MIN_LAYERS = 1;
        public const int MAX_LAYERS = 4;
        public const int MIN_BACKUP_CAPACITY = 5;
        public const int MAX_BACKUP_CAPACITY = 7;

        /// <summary>Số ô còn trống trên khay thừa khi bắt đầu cảnh báo.</summary>
        public const int BACKUP_WARNING_THRESHOLD = 1;

        // ─── Food ─────────────────────────────────────────────────────────────
        /// <summary>Số món trong 1 set phải chia hết cho 3.</summary>
        public const int FOOD_SET_SIZE = 3;

        // ─── Customer ─────────────────────────────────────────────────────────
        public const int MAX_CUSTOMERS_ON_SCREEN = 2;

        // ─── Animation Durations (giây) ───────────────────────────────────────
        public const float FOOD_MOVE_DURATION = 0.35f;
        public const float FOOD_JUMP_HEIGHT = 1.5f;
        public const float LAYER_REVEAL_DURATION = 0.25f;
        public const float CUSTOMER_ENTER_DURATION = 0.4f;
        public const float CUSTOMER_EXIT_DURATION = 0.3f;
        public const float WIN_SEQUENCE_DELAY = 0.5f;

        // ─── Scale Values ─────────────────────────────────────────────────────
        /// <summary>Scale của food ở layer 1 (bị khóa, nhỏ hơn layer 0).</summary>
        public const float LOCKED_LAYER_SCALE = 0.85f;

        // ─── PlayerPrefs Keys ─────────────────────────────────────────────────
        public const string PREF_CURRENT_LEVEL = "CurrentLevel";
        public const string PREF_SOUND_ON = "SoundOn";
        public const string PREF_MUSIC_ON = "MusicOn";
        public const string PREF_VIBRATION_ON = "VibrationOn";

        // ─── Panel / UI States ────────────────────────────────────────────────
        public const string STATE_LOADING = "Loading";
        public const string STATE_LOGO = "Logo";
        public const string STATE_HOME = "Home";
        public const string STATE_GAME = "Game";

        // ─── Tags & Layers ────────────────────────────────────────────────────
        public const string TAG_FOOD = "Food";
        public const string TAG_CUSTOMER = "Customer";
    }
}