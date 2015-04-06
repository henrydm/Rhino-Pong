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
        public static Vector3d BladeSize { get; set; }
        public static double SpeedBladePlayer { get; set; }
        public static double FPS { get; set; }
        public static IALevel IALevel { get; set; }
        static Settings()
        {
            GameBoardWith = 150.0;
            GameBoardHieght = 100.0;
            BallRadius = 2.0;
            BladeSize = new Vector3d(5, 20, 5);
            SpeedBladePlayer = 400.0;
            GamePointsToVictory = 3;
            FPS = 60.0;
            AnimationDurationMillis = 300;
            IALevel = IALevel.Easy;
        }
    }
}
