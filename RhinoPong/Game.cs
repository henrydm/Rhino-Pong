using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.Threading;
using System.Windows.Forms;
using Gma.UserActivityMonitor;
using Rhino;
using Rhino.Display;
using Rhino.Geometry;
using Timer = System.Windows.Forms.Timer;

namespace RhinoPong
{
    internal class Game
    {
        readonly DisplayMaterial _material;
        BoundingBox _bounding;
        private bool _playing;
        private int _playerPoints, _iaPoints;
        Brep _bladeLeft, _bladeRight, _limitBottom, _limitTop, _ball;
        private enum State { None, ColisionWall, ColisionLeftBlade, ColisionRightBlade, PlayerLost, IALost }

        private String BigText;
        private int BigTextHeight;
        private Color BigTextColor;

        Point3d _bladeLeftInitialPosition, _bladeRightInitialPosition, _ballInitialPosition, _bladeLeftPosition, _bladeRightPosition, _ballPosition;
        Vector3d _ballDirection;

        BackgroundWorker _bw;
        Timer _timerIncSpeed;

        private Transform _bladeTransformLetft, _bladeTransformRight, _ballTransform;


        //Constructor
        internal Game()
        {
            CreateBlades();
            CreatePlayGround();
            CreateBall();
            CreateAABB();

            _material = new DisplayMaterial();

            //_timerIncSpeed = new Timer { Interval = Settings.TimeSpeedIncrement };
            //_timerIncSpeed.Tick += (o, e) =>
            //{
            //    var newSpeed = _ballSpeed + Settings.SpeedIncrement;
            //    if (newSpeed > Settings.SpeedLimit) return;
            //    _ballSpeed = newSpeed;
            //};


            _bw = new BackgroundWorker { WorkerSupportsCancellation = true };
            _bw.DoWork += GameLoop;
            _bw.RunWorkerCompleted += GameFinish;
        }

        private void SetUpScene()
        {
            DisplayPipeline.CalculateBoundingBox += DisplayPipeline_CalculateBoundingBox;
            DisplayPipeline.PostDrawObjects += DisplayPipeline_PostDrawObjects;
            HookManager.KeyDown += OnKeyDown;
        }
        private void SetDownScene()
        {
            _playing = false;
            DisplayPipeline.CalculateBoundingBox -= DisplayPipeline_CalculateBoundingBox;
            DisplayPipeline.PostDrawObjects -= DisplayPipeline_PostDrawObjects;
            HookManager.KeyDown -= OnKeyDown;
        }

        private void CreateBall()
        {
            var xInterval = new Interval(-Settings.BallRadius, Settings.BallRadius);
            var yInterval = new Interval(-Settings.BallRadius, Settings.BallRadius);
            var zInterval = new Interval(-Settings.BallRadius / 2.0, Settings.BallRadius / 2.0);
            _ball = new Box(Plane.WorldXY, xInterval, yInterval, zInterval).ToBrep();
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

            _limitBottom = new Box(Plane.WorldXY, xInterval, yInterval, zInterval).ToBrep();
            _limitTop = _limitBottom.DuplicateBrep();
            _limitTop.Translate(Vector3d.YAxis * Settings.GameBoardHieght);
        }
        private void CreateBlades()
        {
            var xInterval = new Interval(-Settings.GameBoardWith / 2d - Settings.BladeSize.X / 2, -Settings.GameBoardWith / 2d + Settings.BladeSize.X / 2);
            var yInterval = new Interval(-Settings.BladeSize.Y / 2, Settings.BladeSize.Y / 2);
            var zInterval = new Interval(-Settings.BladeSize.Z / 2, Settings.BladeSize.Z / 2);

            _bladeLeft = new Box(Plane.WorldXY, xInterval, yInterval, zInterval).ToBrep();
            _bladeRight = _bladeLeft.DuplicateBrep();
            _bladeRight.Translate(new Vector3d(Settings.GameBoardWith, 0, 0));

            _bladeLeftInitialPosition = _bladeLeft.GetBoundingBox(true).Center;
            _bladeRightInitialPosition = _bladeRight.GetBoundingBox(true).Center;

            _bladeLeftPosition = new Point3d(_bladeLeftInitialPosition);
            _bladeRightPosition = new Point3d(_bladeRightInitialPosition);

            _bladeTransformLetft = Transform.Identity;
            _bladeTransformRight = Transform.Identity;
            _ballTransform = Transform.Identity;
        }



        private double _frameRenderMilliseconds;
        private double _currentFps;
        public void StartGame()
        {
            SetUpScene();
            _bw.RunWorkerAsync();
            //_timerIncSpeed.Start();
            RhinoDoc.ActiveDoc.Views.Redraw();
        }


        private void GameLoop(object sender, DoWorkEventArgs e)
        {
            _playing = true;
            _ballDirection = -Vector3d.XAxis;
            var sw = new Stopwatch();
            var frameRenderMillisecondsMax = 1000.0 / Settings.FPS;
            while (_playing)
            {
                sw.Restart();

                var state = MoveBall();

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
                RhinoDoc.ActiveDoc.Views.Redraw();

                _frameRenderMilliseconds = sw.Elapsed.TotalMilliseconds;

                //if (_frameRenderMilliseconds < frameRenderMillisecondsMax)
                //{
                //    Thread.Sleep(Convert.ToInt32(frameRenderMillisecondsMax - _frameRenderMilliseconds));
                //}

                _currentFps = 1000.0 / sw.ElapsedMilliseconds;
            }
        }

        private void GameFinish(object sender, RunWorkerCompletedEventArgs e)
        {
            if (_playerPoints >= Settings.GamePointsToVictory)
            {
                SetDownScene();
                return;
            }

            if (_iaPoints >= Settings.GamePointsToVictory)
            {
                SetDownScene();
                return;
            }
            ResetPositions();

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

        private void ShowPlayerGoalAndRestart()
        {

            var bw = new BackgroundWorker();
            bw.DoWork += (o, e) => ShowAnimation("+1", Color.DarkGreen);
            bw.RunWorkerCompleted += (o, e) => _bw.RunWorkerAsync(); ;
            bw.RunWorkerAsync();
        }
        
        private void ShowIAGoalAndRestart()
        {
            var bw = new BackgroundWorker();
            bw.DoWork += (o, e) => ShowAnimation("-1", Color.Crimson);
            bw.RunWorkerCompleted += (o, e) => _bw.RunWorkerAsync(); ;
            bw.RunWorkerAsync();
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

        private void ShowAnimation(String text, Color color)
        {
           
            BigText = text;
            BigTextColor = color;
           
           
            var frameTime = Settings.AnimationDurationMillis / 20.0;
            var maxHeight = Convert.ToDouble(RhinoDoc.ActiveDoc.Views.ActiveView.Bounds.Height);
            var framesNumber = 2000.0/20.0;
            var currentFrame = 0.0;
            var sw = new Stopwatch();
            while (currentFrame < framesNumber)
            {
                sw.Restart();
                var heightperecentage = currentFrame/framesNumber;
                BigTextHeight = Convert.ToInt32(maxHeight * heightperecentage);
                RhinoDoc.ActiveDoc.Views.Redraw();
                var spent = sw.ElapsedMilliseconds;

                if (spent < frameTime)
                {
                    Thread.Sleep(Convert.ToInt32(frameTime-spent));
                }
                currentFrame++;
            }
            BigText = null;
        }

        private void MoveBlades(bool down, GeometryBase geo)
        {

            var box = geo.GetBoundingBox(false);

            if (down && box.Min.Y <= -Settings.GameBoardHieght / 2.0) return;
            if (box.Max.Y >= Settings.GameBoardHieght / 2.0) return;


            //Left Blade
            if (geo == _bladeLeft)
            {
                var motion = (_frameRenderMilliseconds * Settings.SpeedBladePlayer / 1000.0);
                if (down)
                    _bladeLeftPosition.Y -= motion;
                else
                    _bladeLeftPosition.Y += motion;

                _bladeTransformLetft = Transform.Translation(_bladeLeftPosition - _bladeLeftInitialPosition);

            }
            //Right Blade
            else if (geo == _bladeRight)
            {
                var motion = (_frameRenderMilliseconds * Settings.IALevel.SpeedBladeIA / 1000.0);
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
                case State.ColisionWall:
                    _ballDirection.Y *= -1;
                    break;
                case State.ColisionLeftBlade:
                    _ballDirection = _ballPosition - _bladeLeftPosition;
                    break;
                case State.ColisionRightBlade:
                    _ballDirection = _ballPosition - _bladeRightPosition;
                    break;
            }

            var motion = _frameRenderMilliseconds * Settings.IALevel.SpeedBall / 1000.0;

            _ballDirection.Unitize();
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

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            switch (e.KeyCode)
            {
                case Keys.Up:
                    MoveBlades(false, _bladeLeft);
                    break;
                case Keys.Down:
                    MoveBlades(true, _bladeLeft);
                    break;
                case Keys.Escape:
                    SetDownScene();
                    break;

            }
            e.SuppressKeyPress = e.KeyCode == Keys.Up || e.KeyCode == Keys.Down || e.KeyCode == Keys.Escape;
        }



        private State GetBallColision()
        {

            var ballMinX = _ballPosition.X - Settings.BallRadius;
            var ballMaxX = _ballPosition.X + Settings.BallRadius;
            var ballMinY = _ballPosition.Y - Settings.BallRadius;
            var ballMaxY = _ballPosition.Y + Settings.BallRadius;
            var halfHeigth = Settings.GameBoardHieght / 2.0;

            //Wall Colision
            if (ballMaxY >= halfHeigth || ballMinY <= -halfHeigth)
                return State.ColisionWall;

            var halfBladeX = Settings.BladeSize.X / 2;
            var halfBladeY = Settings.BladeSize.Y / 2;

            var maxBladeLeftX = _bladeLeftPosition.X + halfBladeX;
            var minBladeLeftY = _bladeLeftPosition.Y - halfBladeY;
            var maxBladeLeftY = _bladeLeftPosition.Y + halfBladeY;
            var minBladeRightX = _bladeRightPosition.X - halfBladeX;
            var minBladeRightY = _bladeRightPosition.Y - halfBladeY;
            var maxBladeRightY = _bladeRightPosition.Y + halfBladeY;

            //Left Blade Collision
            if (ballMinX <= maxBladeLeftX)
            {
                //Goal 
                if (ballMinY > maxBladeLeftY || ballMaxY < minBladeLeftY)
                    return State.PlayerLost;

                //Colision Blade
                return State.ColisionLeftBlade;

            }

            //Right Blade Collision
            if (ballMaxX >= minBladeRightX)
            {
                if (ballMinY > maxBladeRightY || ballMaxY < minBladeRightY)
                    return State.IALost;

                //Colision Blade
                return State.ColisionRightBlade;
            }

            //Keep Direction
            return State.None;

        }





        private void DisplayPipeline_CalculateBoundingBox(object sender, CalculateBoundingBoxEventArgs e)
        {
            e.IncludeBoundingBox(_bounding);
        }
        private void DisplayPipeline_PostDrawObjects(object sender, DrawEventArgs e)
        {
            //Walls
            e.Display.DrawBrepShaded(_limitBottom, _material);
            e.Display.DrawBrepShaded(_limitTop, _material);

            //Left Blade
            e.Display.PushModelTransform(_bladeTransformLetft);
            e.Display.DrawBrepShaded(_bladeLeft, _material);
            e.Display.PopModelTransform();

            //Right Blade
            e.Display.PushModelTransform(_bladeTransformRight);
            e.Display.DrawBrepShaded(_bladeRight, _material);
            e.Display.PopModelTransform();

            //Ball
            e.Display.PushModelTransform(_ballTransform);
            e.Display.DrawBrepShaded(_ball, _material);
            e.Display.PopModelTransform();

            //FPS
            var fpsStr = String.Format("FPS:{0}", Math.Round(_currentFps, 1));
            e.Display.Draw2dText(fpsStr, Color.Red, new Point2d(30, 30), false, 30);

            var scoreHeigth = Convert.ToInt32(e.Viewport.Bounds.Height*0.1);

            //Player Score
            e.Display.Draw2dText(_playerPoints.ToString(), Color.DarkGreen, new Point2d(e.Viewport.Bounds.Width / 2.0 - scoreHeigth, scoreHeigth/2.0), true, scoreHeigth);

            //Player Score
            e.Display.Draw2dText(_iaPoints.ToString(), Color.DarkGreen, new Point2d(e.Viewport.Bounds.Width / 2.0 + scoreHeigth, scoreHeigth / 2.0), true, scoreHeigth);

            if (BigText != null)
            {
                var mid = new Point2d(e.Viewport.Bounds.Width / 2.0, e.Viewport.Bounds.Height / 2.0);
                e.Display.Draw2dText(BigText, BigTextColor, mid, true, BigTextHeight);
            }


        }
    }
}
