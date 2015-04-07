using Rhino.Geometry;

namespace RhinoPong
{
    internal static class Settings
    {
        public static double GameBoardWith { get; set; }
        public static double GameBoardHieght { get; set; }
        public static double BallRadius { get; set; }
        public static int GamePointsToVictory { get; set; }
        public static double AnimationDurationMillis { get; set; }
        public static double AnimationFps { get; set; }
        public static Vector3d BladeSize { get; set; }
        public static double SpeedBladePlayer { get; set; }
        public static double Fps { get; set; }
        public static IALevel IALevel { get; set; }
        static Settings()
        {
            GameBoardWith = 150.0;
            GameBoardHieght = 100.0;
            BallRadius = 1.0;
            BladeSize = new Vector3d(5, 20, 5);
            SpeedBladePlayer = 50.0;
            GamePointsToVictory = 3;
            AnimationFps = 40;
            Fps = 60.0;
            AnimationDurationMillis = 1200;
            IALevel = IALevel.Medium;
        }
    }
}
