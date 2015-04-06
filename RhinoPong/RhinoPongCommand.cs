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
            get { return "RhinoPong"; }
        }

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            new Game().StartGame(); 
            return Result.Success;
        }
    }
}
