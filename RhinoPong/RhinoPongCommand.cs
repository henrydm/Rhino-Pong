using System;
using System.Collections.Generic;
using Rhino;
using Rhino.Commands;
using Rhino.Geometry;
using Rhino.Input;
using Rhino.Input.Custom;
using System.ComponentModel;

namespace RhinoPong
{
    [System.Runtime.InteropServices.Guid("9ad624ed-2908-48ba-982f-c6e3f22fbf94")]
    public class RhinoPongCommand : Command
    {
        public RhinoPongCommand()
        {
            // Rhino only creates one instance of each command class defined in a
            // plug-in, so it is safe to store a refence in a static property.
            Instance = this;
        }

        ///<summary>The only instance of this command.</summary>
        public static RhinoPongCommand Instance
        {
            get;
            private set;
        }

        ///<returns>The command name as it appears on the Rhino command line.</returns>
        public override string EnglishName
        {
            get { return "Pong"; }
        }

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            var options = new GetOption();
            options.SetCommandPrompt("Rhino-Pong");
            var indexLevel = options.AddOption("Level");
            var indexFps = options.AddOption("ShowFPS");
            var indexSound = options.AddOption("Sound");
            var indexExit = options.AddOption("Exit");
           
            var levelOptions = new GetOption();
            levelOptions.SetCommandPrompt("Select Level");

            var indexLevelEasy = levelOptions.AddOption("Easy");
            var indexLevelMedium = levelOptions.AddOption("Medium");
            var indexLevelHard = levelOptions.AddOption("Hard");
            var indexLevelImpossible = levelOptions.AddOption("Impossible");

            var game = new Pong();
            game.OnStopGame += (o, e) => RhinoApp.SendKeystrokes("!", true);
            game.StartGame();

            while (true)
            {
                options.Get();
                var slectedOption = options.Option();
                if (slectedOption == null) break;

                if (slectedOption.Index == indexLevel)
                {
                    levelOptions.Get();
                    var selectedLevelIndex = levelOptions.Option().Index;

                    if (selectedLevelIndex == indexLevelEasy)
                    {
                        RhinoPong.Settings.IALevel = IALevel.Easy;
                    }
                    if (selectedLevelIndex == indexLevelMedium)
                    {
                        RhinoPong.Settings.IALevel = IALevel.Medium;
                    }
                    if (selectedLevelIndex == indexLevelHard)
                    {
                        RhinoPong.Settings.IALevel = IALevel.Hard;
                    }
                    if (selectedLevelIndex == indexLevelImpossible)
                    {
                        RhinoPong.Settings.IALevel = IALevel.Impossible;
                    }

                    game.ResetGame();
                }

                else if (slectedOption.Index == indexFps)
                {
                    game.ShowFps = !game.ShowFps;
                }
                else if (slectedOption.Index == indexSound)
                {
                    game.SoundEnabled = !game.SoundEnabled;
                }

                else
                {
                    break;
                }
            }
            game.StopGame();
            return Result.Success;
        }
    }
}
