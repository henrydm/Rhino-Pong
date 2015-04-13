using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Media;
using System.Threading;
using System.Windows.Forms;
using Gma.UserActivityMonitor;
using Rhino;
using Rhino.Display;
using Rhino.Geometry;
using Timer = System.Windows.Forms.Timer;

namespace RhinoPong
{
    internal class Pong
    {
        #region Definitions
        private bool _stpedFromInside;
        private readonly SoundPlayer _player;
        public bool ShowFps { get; set; }
        public bool SoundEnabled { get; set; }
        private bool _restart;
        enum State { None, ColisionWallUp, ColisionWallDown, ColisionLeftBlade, ColisionRightBlade, PlayerLost, IALost }
        readonly DisplayMaterial _material;
        readonly BackgroundWorker _bw;
        bool _playing;
        int _playerPoints, _iaPoints, _bigTextHeight;
        double _frameRenderMilliseconds, _currentFps;
        Mesh _bladeLeft, _bladeRight, _limitBottom, _limitTop, _ball;
        String _bigText;
        Color _bigTextColor;
        BoundingBox _bounding;

        private Point3d _bladeLeftInitialPosition,
            _bladeRightInitialPosition,
            _ballInitialPosition,
            _bladeLeftPosition,
            _bladeRightPosition,
            _ballPosition;
        Vector3d _ballDirection;
        Transform _bladeTransformLetft, _bladeTransformRight, _ballTransform;
        internal event EventHandler OnStopGame;

        #endregion

        //Constructor
        internal Pong()
        {


            CreateBlades();
            CreatePlayGround();
            CreateBall();
            CreateAABB();


            _material = new DisplayMaterial();
            _player = new SoundPlayer();
            _bw = new BackgroundWorker { WorkerSupportsCancellation = true };
            _bw.DoWork += GameLoop;
            _bw.RunWorkerCompleted += GameFinish;
        }

        private void SetUpScene()
        {
            DisplayPipeline.CalculateBoundingBox += DisplayPipeline_CalculateBoundingBox;
            DisplayPipeline.DrawOverlay += DisplayPipeline_PostDrawObjects;
            HookManager.KeyDown += OnKeyDown;
            HookManager.KeyUp += OnKeyUp;
            foreach (var view in RhinoDoc.ActiveDoc.Views)
                view.ActiveViewport.ZoomBoundingBox(_bounding);

        }


        private void SetDownScene()
        {
            _playing = false;
            DisplayPipeline.CalculateBoundingBox -= DisplayPipeline_CalculateBoundingBox;
            DisplayPipeline.DrawOverlay -= DisplayPipeline_PostDrawObjects;
            HookManager.KeyDown -= OnKeyDown;
            RhinoDoc.ActiveDoc.Views.Redraw();

            if (OnStopGame != null && _stpedFromInside) OnStopGame(this, null);
        }

        private void CreateBall()
        {
            var xInterval = new Interval(-Settings.BallRadius, Settings.BallRadius);
            var yInterval = new Interval(-Settings.BallRadius, Settings.BallRadius);
            var zInterval = new Interval(-Settings.BallRadius / 2, Settings.BallRadius / 2);

            _ball = Mesh.CreateFromBox(new Box(Plane.WorldXY, xInterval, yInterval, zInterval), 1, 1, 1);
            _ballInitialPosition = Point3d.Origin;
            _ballPosition = Point3d.Origin;
            _ballTransform = Transform.Identity;

        }
        private void CreateAABB()
        {
            _bounding = _bladeLeft.GetBoundingBox(false);
            _bounding.Union(_bladeRight.GetBoundingBox(false));
            _bounding.Union(_limitBottom.GetBoundingBox(false));
            _bounding.Union(_limitTop.GetBoundingBox(false));
            _bounding.Union(_ball.GetBoundingBox(false));
        }
        private void CreatePlayGround()
        {
            var xInterval = new Interval(-Settings.GameBoardWith / 2d, Settings.GameBoardWith / 2d);
            var yInterval = new Interval(-Settings.GameBoardHieght / 2d - Settings.BladeSize.X / 2d, -Settings.GameBoardHieght / 2d + Settings.BladeSize.X / 2d);
            var zInterval = new Interval(-Settings.BladeSize.Z / 2, Settings.BladeSize.Z / 2);

            _limitBottom = Mesh.CreateFromBox(new Box(Plane.WorldXY, xInterval, yInterval, zInterval), 1, 1, 1);
            _limitTop = _limitBottom.DuplicateMesh();
            _limitTop.Translate(Vector3d.YAxis * Settings.GameBoardHieght);
        }
        private void CreateBlades()
        {
            var xInterval = new Interval(-Settings.GameBoardWith / 2d - Settings.BladeSize.X / 2, -Settings.GameBoardWith / 2d + Settings.BladeSize.X / 2);
            var yInterval = new Interval(-Settings.BladeSize.Y / 2, Settings.BladeSize.Y / 2);
            var zInterval = new Interval(-Settings.BladeSize.Z / 2, Settings.BladeSize.Z / 2);

            _bladeLeft = Mesh.CreateFromBox(new Box(Plane.WorldXY, xInterval, yInterval, zInterval), 1, 1, 1);
            _bladeRight = _bladeLeft.DuplicateMesh();
            _bladeRight.Translate(new Vector3d(Settings.GameBoardWith, 0, 0));

            _bladeLeftInitialPosition = _bladeLeft.GetBoundingBox(true).Center;
            _bladeRightInitialPosition = _bladeRight.GetBoundingBox(true).Center;

            _bladeLeftPosition = new Point3d(_bladeLeftInitialPosition);
            _bladeRightPosition = new Point3d(_bladeRightInitialPosition);

            _bladeTransformLetft = Transform.Identity;
            _bladeTransformRight = Transform.Identity;
            _ballTransform = Transform.Identity;
        }

        public void StartGame()
        {
            SetUpScene();
            _bw.RunWorkerAsync();
            //_timerIncSpeed.Start();
            _keyDown = Keys.None;
            SoundEnabled = true;
            RhinoDoc.ActiveDoc.Views.Redraw();
        }

        public void ResetGame()
        {
            _playerPoints = 0;
            _iaPoints = 0;
            ResetPositions();
            _restart = true;
            _playing = false;
        }
        public void StopGame()
        {
            _restart = false;
            _playing = false;
        }
        private void StopFromInside()
        {
            _stpedFromInside = true;
            StopGame();
        }


        private void PlaySound(State state)
        {
            switch (state)
            {
                case State.ColisionWallDown:
                    PlaySoundPong(Sounds.pong3);
                    break;
                case State.ColisionWallUp:
                    PlaySoundPong(Sounds.pong3);
                    break;
                case State.ColisionLeftBlade:
                    PlaySoundPong(Sounds.pong2);
                    break;
                case State.ColisionRightBlade:
                    PlaySoundPong(Sounds.pong2);
                    break;
                case State.PlayerLost:
                    PlaySoundPong(Sounds.ComputerPoint);
                    break;
                case State.IALost:
                    PlaySoundPong(Sounds.PlayerPoint);
                    break;
            }
        }
        private void GameLoop(object sender, DoWorkEventArgs e)
        {
            _playing = true;
            _ballDirection = -Vector3d.XAxis;
            PlaySoundPong(Sounds.initSound);
            var sw = new Stopwatch();

            while (_playing)
            {
                sw.Restart();

                var state = MoveBall();
                PlaySound(state);
                if (state == State.IALost)
                {
                    _playerPoints++;
                    e.Result = state;
                    break;
                }
                if (state == State.PlayerLost)
                {
                    _iaPoints++;
                    e.Result = state;
                    break;
                }


                RightBladeIA();

                Redraw();

                if (sw.ElapsedMilliseconds < Settings.FrameRenderMillisecondsMax)
                {
                    Thread.Sleep(Convert.ToInt32(Settings.FrameRenderMillisecondsMax - sw.ElapsedMilliseconds));
                }


                _frameRenderMilliseconds = sw.ElapsedMilliseconds;




                _currentFps = 1000.0 / _frameRenderMilliseconds;
            }
        }

        private void Redraw()
        {
            RhinoDoc.ActiveDoc.Views.ActiveView.Redraw();
            return;

        }

        private void GameFinish(object sender, RunWorkerCompletedEventArgs e)
        {
            //Player Win
            if (_playerPoints >= Settings.GamePointsToVictory)
            {
                ShowPlayerWinAndShutDown();
            }

            //Computer Win
            else if (_iaPoints >= Settings.GamePointsToVictory)
            {
                ShowIAWinAndShutDown();
            }

            else
            {
                ResetPositions();

                if ((e.Result is State))
                {
                    //Goal
                    switch ((State)e.Result)
                    {
                        case State.IALost:
                            ShowPlayerGoalAndRestart();
                            return;
                        case State.PlayerLost:
                            ShowIAGoalAndRestart();
                            return;
                    }
                }

                if (_restart)
                {
                    _restart = false;
                    _bw.RunWorkerAsync();

                }
                else
                {
                    SetDownScene();
                }
            }
        }

        private void ShowPlayerGoalAndRestart()
        {

            var bw = new BackgroundWorker();
            bw.DoWork += (o, e) => ShowAnimation("+1", Color.DarkGreen, Settings.AnimationDurationMillis);
            bw.RunWorkerCompleted += (o, e) => _bw.RunWorkerAsync();
            bw.RunWorkerAsync();
        }
        private void ShowIAGoalAndRestart()
        {
            var bw = new BackgroundWorker();
            bw.DoWork += (o, e) => ShowAnimation("-1", Color.Black, Settings.AnimationDurationMillis);
            bw.RunWorkerCompleted += (o, e) => _bw.RunWorkerAsync();
            bw.RunWorkerAsync();
        }
        private void ShowPlayerWinAndShutDown()
        {
            var bw = new BackgroundWorker();
            bw.DoWork += (o, e) => ShowAnimation("Player Win", Color.DarkOrange, 3000.0);
            bw.RunWorkerCompleted += (o, e) => SetDownScene();
            bw.RunWorkerAsync();
        }
        private void ShowIAWinAndShutDown()
        {
            var bw = new BackgroundWorker();
            bw.DoWork += (o, e) => ShowAnimation("Computer Win", Color.Sienna, 3000.0);
            bw.RunWorkerCompleted += (o, e) => SetDownScene();
            bw.RunWorkerAsync();
        }

        private void ShowAnimation(String text, Color color, double durationinMillis)
        {

            _bigText = text;
            _bigTextColor = color;


            var frameTime = durationinMillis / Settings.AnimationFps;
            var maxHeight = Convert.ToDouble(RhinoDoc.ActiveDoc.Views.ActiveView.Bounds.Height);
            var framesNumber = Settings.AnimationFps * durationinMillis / 1000.0;
            var currentFrame = 0.0;
            var sw = new Stopwatch();
            while (currentFrame < framesNumber)
            {
                sw.Restart();
                var heightperecentage = currentFrame / framesNumber;
                _bigTextHeight = Convert.ToInt32(maxHeight * heightperecentage);
                RhinoDoc.ActiveDoc.Views.Redraw();
                var spent = sw.ElapsedMilliseconds;

                if (spent < frameTime)
                {
                    Thread.Sleep(Convert.ToInt32(frameTime - spent));
                }
                currentFrame++;
            }


            _bigText = null;
        }


        private void ResetPositions()
        {
            _ballPosition = new Point3d(_ballInitialPosition);
            _ballTransform = Transform.Identity;

            _bladeLeftPosition = new Point3d(_bladeLeftInitialPosition);
            _bladeTransformLetft = Transform.Identity;

            _bladeRightPosition = new Point3d(_bladeRightInitialPosition);
            _bladeTransformRight = Transform.Identity;
        }

        private void PlaySoundPong(UnmanagedMemoryStream stream)
        {
            if (!SoundEnabled) return;
            _player.Stream = stream;
            _player.Play();
        }

        private void MoveBlades(bool down, GeometryBase geo)
        {
            //Left Blade
            if (geo == _bladeLeft)
            {
                //Margin Down
                if (down && _bladeLeftPosition.Y - Settings.BladeSizeHalf.Y < -Settings.GameBoardHalfSizeY + Settings.BladeSize.X)
                {
                    _bladeLeftPosition.Y = -Settings.GameBoardHalfSizeY + Settings.BladeSizeHalf.X + Settings.BladeSizeHalf.Y;
                    _bladeTransformLetft = Transform.Translation(_bladeLeftPosition - _bladeLeftInitialPosition);
                    return;
                }
                //Margin Up
                if (!down && _bladeLeftPosition.Y + Settings.BladeSizeHalf.Y > Settings.GameBoardHalfSizeY - Settings.BladeSize.X)
                {
                    _bladeLeftPosition.Y = Settings.GameBoardHalfSizeY - Settings.BladeSizeHalf.X - Settings.BladeSizeHalf.Y;
                    _bladeTransformLetft = Transform.Translation(_bladeLeftPosition - _bladeLeftInitialPosition);
                    return;
                }


                var motion = (_frameRenderMilliseconds * Settings.SpeedBladePlayer);

                if (down)
                    _bladeLeftPosition.Y -= motion;
                else
                    _bladeLeftPosition.Y += motion;

                _bladeTransformLetft = Transform.Translation(_bladeLeftPosition - _bladeLeftInitialPosition);

            }
            //Right Blade
            else if (geo == _bladeRight)
            {
                //Margin Down
                if (down && _bladeRightPosition.Y - Settings.BladeSizeHalf.Y < -Settings.GameBoardHalfSizeY + Settings.BladeSize.X)
                {
                    _bladeRightPosition.Y = -Settings.GameBoardHalfSizeY + Settings.BladeSizeHalf.X + Settings.BladeSizeHalf.Y;
                    _bladeTransformRight = Transform.Translation(_bladeRightPosition - _bladeRightInitialPosition);
                    return;
                }
                //Margin Up
                if (!down && _bladeRightPosition.Y + Settings.BladeSizeHalf.Y > Settings.GameBoardHalfSizeY - Settings.BladeSize.X)
                {
                    _bladeRightPosition.Y = Settings.GameBoardHalfSizeY - Settings.BladeSizeHalf.X - Settings.BladeSizeHalf.Y;
                    _bladeTransformRight = Transform.Translation(_bladeRightPosition - _bladeRightInitialPosition);
                    return;
                }
                
                var motion = (_frameRenderMilliseconds * Settings.IALevel.SpeedBladeIA);
                if (down)
                    _bladeRightPosition.Y -= motion;
                else
                    _bladeRightPosition.Y += motion;

                _bladeTransformRight = Transform.Translation(_bladeRightPosition - _bladeRightInitialPosition);
            }

        }

        private State MoveBall()
        {
            var colision = GetBallColision();

            switch (colision)
            {
                case State.None:
                    break;
                case State.ColisionWallUp:
                    if (_ballDirection.Y > 0) _ballDirection.Y *= -1;
                    break;
                case State.ColisionWallDown:
                    if (_ballDirection.Y < 0) _ballDirection.Y *= -1;
                    break;
                case State.ColisionLeftBlade:

                    _ballDirection = _ballPosition - _bladeLeftPosition;
                    _ballDirection.Unitize();

                    break;
                case State.ColisionRightBlade:

                    _ballDirection = _ballPosition - _bladeRightPosition;
                    _ballDirection.Unitize();
                    break;
            }

            var motion = _frameRenderMilliseconds * Settings.IALevel.SpeedBall;

            _ballPosition.Transform(Transform.Translation(_ballDirection * motion));


            _ballTransform = Transform.Translation(_ballPosition - _ballInitialPosition);

            return colision;
        }
        private void RightBladeIA()
        {
            var verticalDistance = Math.Abs(_ballPosition.Y - _bladeRightPosition.Y);

            if (Settings.IALevel.StopOnReleaseBall && _ballDirection.X < 0) return;
            if (Settings.IALevel.StartOnMiddleScreen && _ballPosition.X < 0) return;

            if (verticalDistance < Settings.IALevel.VerticalBladeTolerance) return;


            var moveDown = _ballPosition.Y < _bladeRightPosition.Y;
            MoveBlades(moveDown, _bladeRight);
        }

        private Keys _keyDown;

        private void ProcessKeyDown(bool xView)
        {
            var sw = new Stopwatch();

            while (_keyDown != Keys.None)
            {
                sw.Restart();

                if (xView)
                {
                    switch (_keyDown)
                    {
                        case Keys.Up:
                            MoveBlades(false, _bladeLeft);
                            break;
                        case Keys.Down:
                            MoveBlades(true, _bladeLeft);
                            break;
                    }
                }
                else
                {
                    switch (_keyDown)
                    {
                        case Keys.Left:
                            MoveBlades(false, _bladeLeft);
                            break;
                        case Keys.Right:
                            MoveBlades(true, _bladeLeft);
                            break;
                    }
                }

                if (_keyDown == Keys.Escape)
                {
                    StopFromInside();
                    SetDownScene();
                }



                var elapsed = sw.ElapsedMilliseconds;
                var diff = Settings.FrameRenderMillisecondsMax - elapsed;
                if (diff > 0)
                    Thread.Sleep(Convert.ToInt32(diff));


            }
        }
        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            var supressKey = false;

            if (e.KeyCode == Keys.Up || e.KeyCode == Keys.Down || e.KeyCode == Keys.Left || e.KeyCode == Keys.Right || e.KeyCode == Keys.Escape)
            {
                supressKey = true;

                var useLateralKeys = true;
                if (RhinoDoc.ActiveDoc.Views.ActiveView.ActiveViewport.IsPerspectiveProjection)
                {
                    var vec = RhinoDoc.ActiveDoc.Views.ActiveView.ActiveViewport.CameraDirection;
                    var x = Math.Abs(vec.X);
                    var y = Math.Abs(vec.Y);

                    if (x > y) useLateralKeys = false;
                }

                if (_keyDown == Keys.None)
                {
                    _keyDown = e.KeyCode;
                    var bw = new BackgroundWorker();
                    bw.DoWork += (o, ea) => ProcessKeyDown(useLateralKeys);
                    bw.RunWorkerAsync();
                }
            }

            e.SuppressKeyPress = supressKey;




        }
        private void OnKeyUp(object sender, KeyEventArgs e)
        {
            _keyDown = Keys.None;
        }
        private State GetBallColision()
        {
            var ballMinX = _ballPosition.X - Settings.BallRadius;
            var ballMaxX = _ballPosition.X + Settings.BallRadius;
            var ballMinY = _ballPosition.Y - Settings.BallRadius;
            var ballMaxY = _ballPosition.Y + Settings.BallRadius;


            var minBladeLeftX = _bladeLeftPosition.X - Settings.BladeSizeHalf.X;
            var maxBladeLeftX = _bladeLeftPosition.X + Settings.BladeSizeHalf.X;

            var minBladeLeftY = _bladeLeftPosition.Y - Settings.BladeSizeHalf.Y;
            var maxBladeLeftY = _bladeLeftPosition.Y + Settings.BladeSizeHalf.Y;

            var minBladeRightX = _bladeRightPosition.X - Settings.BladeSizeHalf.X;
            var maxBladeRightX = _bladeRightPosition.X + Settings.BladeSizeHalf.X;

            var minBladeRightY = _bladeRightPosition.Y - Settings.BladeSizeHalf.Y;
            var maxBladeRightY = _bladeRightPosition.Y + Settings.BladeSizeHalf.Y;



            if (_ballDirection.X < 0 && ballMinX <= maxBladeLeftX)
            {
                //Left Blade Collision
                if (Between(ballMinY, minBladeLeftY, maxBladeLeftY) || Between(ballMaxY, minBladeLeftY, maxBladeLeftY))
                    if (ballMaxX > minBladeLeftX)
                        return State.ColisionLeftBlade;

                //IA Goal
                if (ballMaxX <minBladeLeftX -10)
                    return State.PlayerLost;
            }


            else if (_ballDirection.X > 0 && ballMaxX >= minBladeRightX)
            {
                //Right Blade Collision
                if (Between(ballMinY, minBladeRightY, maxBladeRightY) || Between(ballMaxY, minBladeRightY, maxBladeRightY))
                    if (ballMinX < maxBladeRightX)
                    return State.ColisionRightBlade;

                //Player Goal 
                if (ballMinX > maxBladeRightX+10)
                    return State.IALost;
            }


            //Wall Colision
            if (ballMaxY >= Settings.GameBoardHalfSizeY - Settings.BladeSizeHalf.X)
                return State.ColisionWallUp;
            if (ballMinY <= -Settings.GameBoardHalfSizeY + Settings.BladeSizeHalf.X)
                return State.ColisionWallDown;

            //No Colision
            return State.None;

        }


        private static bool Between(double test, double min, double max)
        {
            return test >= min && test <= max;
        }


        private void DisplayPipeline_CalculateBoundingBox(object sender, CalculateBoundingBoxEventArgs e)
        {
            e.IncludeBoundingBox(_bounding);
        }

        private void DisplayPipeline_PostDrawObjects(object sender, DrawEventArgs e)
        {
            //Walls
            e.Display.DrawMeshShaded(_limitBottom, _material);
            e.Display.DrawMeshShaded(_limitTop, _material);

            //Left Blade
            e.Display.PushModelTransform(_bladeTransformLetft);
            e.Display.DrawMeshShaded(_bladeLeft, _material);
            e.Display.PopModelTransform();

            //Right Blade
            e.Display.PushModelTransform(_bladeTransformRight);
            e.Display.DrawMeshShaded(_bladeRight, _material);
            e.Display.PopModelTransform();

            //Ball
            e.Display.PushModelTransform(_ballTransform);
            e.Display.DrawMeshShaded(_ball, _material);
            e.Display.PopModelTransform();

            //FPS
            if (ShowFps)
            {
                var fpsStr = String.Format("FPS:{0}", Math.Round(_currentFps, 1));
                e.Display.Draw2dText(fpsStr, Color.Firebrick, new Point2d(30, 50), false, 30);
            }

            var scoreHeigth = Convert.ToInt32(e.Viewport.Bounds.Height * 0.1);

            //Player Score
            e.Display.Draw2dText(_playerPoints.ToString(), Color.ForestGreen, new Point2d(e.Viewport.Bounds.Width / 2.0 - scoreHeigth, scoreHeigth / 2.0), true, scoreHeigth, "Impact");

            //Player Score
            e.Display.Draw2dText(_iaPoints.ToString(), Color.ForestGreen, new Point2d(e.Viewport.Bounds.Width / 2.0 + scoreHeigth, scoreHeigth / 2.0), true, scoreHeigth, "Impact");

            if (_bigText != null)
            {
                var mid = new Point2d(e.Viewport.Bounds.Width / 2.0, e.Viewport.Bounds.Height / 2.0);
                e.Display.Draw2dText(_bigText, _bigTextColor, mid, true, _bigTextHeight, "Impact");
            }


        }
    }
}
