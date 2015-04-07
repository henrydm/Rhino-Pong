namespace RhinoPong
{

    internal class IALevel
    {
        internal enum Level { Easy, Medium, Hard, Impossible }
        internal double VerticalBladeTolerance { get; set; }
        internal double SpeedBladeIA { get; set; }
        internal double SpeedBall { get; set; }
        internal bool StopOnReleaseBall { get; set; }
        internal bool StartOnMiddleScreen { get; set; }
        

        internal static IALevel Easy
        {
            get
            {
                return new IALevel
                {
                    VerticalBladeTolerance = Settings.BallRadius*2,
                    SpeedBladeIA = 40,
                    SpeedBall = 80,
                    StopOnReleaseBall = true,
                    StartOnMiddleScreen= true,
                };
            }
        }
        internal static IALevel Medium
        {
            get
            {
                return new IALevel
                {
                    VerticalBladeTolerance = Settings.BallRadius,
                    SpeedBladeIA = 70,
                    SpeedBall = 100,
                    StopOnReleaseBall = true,
                    StartOnMiddleScreen = true,
                };
            }
        }
        internal static IALevel Hard
        {
            get
            {
                return new IALevel
                {
                    VerticalBladeTolerance = Settings.BallRadius * 0.5,
                    SpeedBladeIA = 90,
                    SpeedBall = 130,
                    StopOnReleaseBall = true,
                    StartOnMiddleScreen = true,
                };
            }
        }
        internal static IALevel Impossible
        {
            get
            {
                return new IALevel
                {
                    VerticalBladeTolerance = Settings.GameBoardHieght * 0.002,
                    SpeedBladeIA = 120,
                    SpeedBall = 150,
                    StopOnReleaseBall = true,
                    StartOnMiddleScreen = false,
                };
            }
        }
    }
}
