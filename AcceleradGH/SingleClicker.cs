using System;
using System.Collections.Generic;
using System.Diagnostics;

using Grasshopper.Kernel;
using Rhino.Geometry;

namespace AcceleradGH
{
    public class SingleClicker : GH_Component
    {
        /// <summary>
        /// Timer to track elapsed time since click
        /// </summary>
        Stopwatch stopwatch = new Stopwatch();

        /// <summary>
        /// Initializes a new instance of the SingleClicker class.
        /// </summary>
        public SingleClicker()
            : base("Single Clicker", "Click",
                "Convert double click to single click",
                "Accelerad", "Utility")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddBooleanParameter("Input", "B", "Input boolean for button input", GH_ParamAccess.item, false);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddBooleanParameter("Output", "B", "Output boolean", GH_ParamAccess.item);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            bool input = false;
            DA.GetData(0, ref input);
            if (input)
            {
                if (stopwatch.IsRunning)
                {
                    stopwatch.Stop();
                    DA.SetData(0, stopwatch.ElapsedMilliseconds > 500);
                    stopwatch.Reset();
                }
                else
                {
                    DA.SetData(0, true);
                }
            }
            else
            {
                stopwatch.Start();
                DA.SetData(0, false);
            }
        }

        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        protected override System.Drawing.Bitmap Icon
        {
            get
            {
                //You can add image files to your project resources and access them like this:
                return Properties.Resources.vfd; //TODO new icon
            }
        }

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid
        {
            get { return new Guid("{d83fd1e7-5e41-4859-9c1c-9334cfd3666e}"); }
        }
    }
}