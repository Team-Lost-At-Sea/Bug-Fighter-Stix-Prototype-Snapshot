public static class RenderOrder
{
    public static class Layers
    {
        // Current project sorting layers from TagManager:
        public const string Default = "Default";
        public const string Background = "Background";
        public const string WorldGameplay = "WorldGameplay";

        // Optional future layers (create in Project Settings before using):
        public const string GameplayOverlay = "GameplayOverlay";
        public const string UI = "UI";
        public const string UIOverlay = "UIOverlay";
    }

    public static class Bands
    {
        public const int BaseMin = 0;
        public const int BaseMax = 49;
        public const int InteractiveMin = 50;
        public const int InteractiveMax = 99;
        public const int DebugMin = 900;
    }

    public static class UI
    {
        public const int Hud = 100;
        public const int InputDisplay = 110;
        public const int AnnouncerOverlay = 200;
        public const int PauseBackdrop = 300;
        public const int PauseMenu = 310;
        public const int Modal = 400;
        public const int ScreenFade = 500;
    }

    public static class World
    {
        public const int StageBackdrop = -100;
        public const int Fighters = 20;
        public const int Projectiles = 90;
        public const int DebugBoxes = 900;
    }

    public static class CharacterSelect
    {
        public const int Portrait = -10;
        public const int PortraitMask = -9;
        public const int Frame = 0;
        public const int HoverGlow = 20;
        public const int Cursor = 100;
    }
}
