using System;
using System.Collections.Generic;
using System.Windows.Forms;

using Grasshopper.Kernel;
using Rhino.Geometry;

using Rhino;
using Rhino.Commands;
using Rhino.Display;
using Rhino.DocObjects;

namespace AcceleradGH
{
    public class AcceleradGHComponent : GH_Component
    {
        /// <summary>
        /// Last wake state.
        /// </summary>
        protected bool state = false;

        /// <summary>
        /// A command is currently in progress.
        /// </summary>
        protected bool inCommand = false;

        /// <summary>
        /// A change has been made to the model during the current command.
        /// </summary>
        protected bool modelChanged = false;

        /// <summary>
        /// A change occurred during a completed command.
        /// </summary>
        protected bool doUpdate = false;

        /// <summary>
        /// Timer is running.
        /// </summary>
        protected bool timerRunning = false;

        protected Timer trampoline = new Timer();

        protected GH_Document ghDocument = null;

        /// <summary>
        /// Last sun direction.
        /// </summary>
        protected Vector3d solar = new Vector3d(0, 0, 0);

        protected int count = 0;

        /// <summary>
        /// Each implementation of GH_Component must provide a public 
        /// constructor without any arguments.
        /// Category represents the Tab in which the component will appear, 
        /// Subcategory the panel. If you use non-existing tab or panel names, 
        /// new tabs/panels will automatically be created.
        /// </summary>
        public AcceleradGHComponent()
            : base("Watch for Change", "Watch",
                "Signal when the Rhino model has changed.",
                "Accelerad", "RT")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddBooleanParameter("Awake", "A", "AcceleradRT is listening for changes", GH_ParamAccess.item, false);
            pManager.AddVectorParameter("Sun Vector", "S", "Direction to sun", GH_ParamAccess.item, solar);
            //pManager.AddIntegerParameter("Interval", "I", "Time interval between checks (milliseconds)", GH_ParamAccess.item, 1000);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddBooleanParameter("Update", "U", "Send update to AcceleradRT", GH_ParamAccess.item);
            pManager.AddIntegerParameter("Count", "C", "Times run", GH_ParamAccess.item);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object can be used to retrieve data from input parameters and 
        /// to store data in output parameters.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            // Use the DA object to retrieve the data inside the first input parameter.
            // If the retrieval fails (for example if there is no data) we need to abort.
            bool awake = false;
            DA.GetData(0, ref awake);

            Vector3d sun = new Vector3d(solar);
            DA.GetData(1, ref sun);
            bool timeChanged = !solar.Equals(sun);
            if (timeChanged)
                solar = sun;

            //int interval = 1000;
            //DA.GetData(2, ref interval);
            //trampoline.Interval = interval;
            trampoline.Interval = 1;

            bool update = false;

            if (awake)
            {
                if (ghDocument == null)
                    ghDocument = this.OnPingDocument();

                if (!state)
                {
                    // Register command event handlers
                    RhinoDoc.AddRhinoObject += HandleAddObject;
                    RhinoDoc.DeleteRhinoObject += HandleDeleteObject;
                    RhinoDoc.UndeleteRhinoObject += HandleUndeleteObject;
                    RhinoDoc.ModifyObjectAttributes += HandleModifyAttributes;
                    //RhinoDoc.PurgeRhinoObject += HandlePurgeObject;
                    //RhinoDoc.ReplaceRhinoObject += HandleReplaceObject;
                    //RhinoDoc.ModifyObjectAttributes += HandleModifyObject;
                    //RhinoDoc.MaterialTableEvent += HandleMaterial;
                    RhinoDoc.LayerTableEvent += HandleLayer;

                    Command.BeginCommand += HandleBeginCommand;
                    Command.EndCommand += HandleEndCommand;
                    //Command.UndoRedo += HandleUndoRedo;

                    update = true;
                }
                else
                {
                    update = doUpdate || timeChanged;
                }

                if (update)
                    ghDocument.SolutionEnd += documentSolutionEnd;
            }
            else if (state)
            {
                // Register command event handlers
                RhinoDoc.AddRhinoObject -= HandleAddObject;
                RhinoDoc.DeleteRhinoObject -= HandleDeleteObject;
                RhinoDoc.UndeleteRhinoObject -= HandleUndeleteObject;
                RhinoDoc.ModifyObjectAttributes -= HandleModifyAttributes;
                //RhinoDoc.PurgeRhinoObject -= HandlePurgeObject;
                //RhinoDoc.ReplaceRhinoObject -= HandleReplaceObject;
                //RhinoDoc.ModifyObjectAttributes -= HandleModifyObject;
                //RhinoDoc.MaterialTableEvent -= HandleMaterial;
                RhinoDoc.LayerTableEvent -= HandleLayer;

                Command.BeginCommand -= HandleBeginCommand;
                Command.EndCommand -= HandleEndCommand;
                //Command.UndoRedo -= HandleUndoRedo;
            }
            state = awake;
            doUpdate = false;

            DA.SetData(0, update);
            DA.SetData(1, count++);
        }

        /// <summary>
        /// Provides an Icon for every component that will be visible in the User Interface.
        /// Icons need to be 24x24 pixels.
        /// </summary>
        protected override System.Drawing.Bitmap Icon
        {
            get
            {
                // You can add image files to your project resources and access them like this:
                return Properties.Resources.vfd;
            }
        }

        /// <summary>
        /// Each component must have a unique Guid to identify it. 
        /// It is vital this Guid doesn't change otherwise old ghx files 
        /// that use the old ID will partially fail during loading.
        /// </summary>
        public override Guid ComponentGuid
        {
            get { return new Guid("{23ed7027-4273-4cee-acb6-9c97be1f9d05}"); }
        }

        #region Command Event Handlers

        public void HandleAddObject(Object sender, RhinoObjectEventArgs e)
        {
            //if (!modelOpening)
            //{
            handleChange();
            //    Print("Add " + e.TheObject.ObjectType.ToString());
            //}
        }

        public void HandleDeleteObject(Object sender, RhinoObjectEventArgs e)
        {
            handleChange();
            //Print("Delete " + e.TheObject.ObjectType.ToString());
        }

        public void HandleUndeleteObject(Object sender, RhinoObjectEventArgs e) // Similar to add
        {
            handleChange();
            //Print("Undelete " + e.TheObject.ObjectType.ToString());
        }

        public void HandleModifyAttributes(Object sender, RhinoModifyObjectAttributesEventArgs e)
        {
            if (e.OldAttributes.LayerIndex != e.NewAttributes.LayerIndex)
            {
                handleChange();
            }
        }

        //public void HandlePurgeObject(Object sender, RhinoObjectEventArgs e)
        //{
        //    modelChanged = true;
        //    Print("Purge Object " + e.TheObject.Id);
        //}

        //public void HandleReplaceObject(Object sender, RhinoReplaceObjectEventArgs e)
        //{
        //    modelChanged = true;
        //    Print("Replace Object " + e.OldRhinoObject.Id + " with " + e.NewRhinoObject.Id);
        //}

        //public void HandleModifyObject(Object sender, RhinoModifyObjectAttributesEventArgs e)
        //{
        //    modelChanged = true;
        //    Print("Modify Object " + e.RhinoObject.Id);
        //}

        //public void HandleMaterial(Object sender, Rhino.DocObjects.Tables.MaterialTableEventArgs e)
        //{
        //    modelChanged = true;
        //    Print("Material " + e.Index);
        //}

        public void HandleLayer(Object sender, Rhino.DocObjects.Tables.LayerTableEventArgs e)
        {
            if (e.OldState.IsVisible != e.NewState.IsVisible)
            {
                handleChange();
            }
            //Print("Layer " + e.LayerIndex);
        }

        public void HandleBeginCommand(Object sender, CommandEventArgs e)
        {
            inCommand = true;
            //EndIdleTime();
            ////PrintDebug("Begin Command " + e.CommandEnglishName);
        }

        public void HandleEndCommand(Object sender, CommandEventArgs e)
        {
            inCommand = false;
            if (e.CommandResult == Result.Success && modelChanged)
            {
                //AcceptIdleTime();
                handleChange();
                //idleTime.Restart(); // Restart from zero
            }
            modelChanged = false;
            //Print("End Command " + e.CommandEnglishName + ": " + e.CommandResult.ToString());
        }

        public void handleChange()
        {
            if (inCommand)
                modelChanged = true;
            else
            {
                doUpdate = true;
                this.ExpireSolution(true);
            }
        }

        /// <summary>
        /// Based on trampoline.ghx: http://www.grasshopper3d.com/forum/topics/timers-and-theengine-script?commentId=2985220%3AComment%3A129569
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public void documentSolutionEnd(object sender, GH_SolutionEventArgs e)
        {
            ghDocument.SolutionEnd -= documentSolutionEnd;

            // The trampoline gives time to stop the infinitly recursive chain from canvas,
            // as well as mantains the call stack fixed in length
            if (!timerRunning)
            {
                timerRunning = true;
                trampoline.Tick += trampolineTick;
                trampoline.Start();
            }
        }

        public void trampolineTick(object sender, EventArgs e)
        {
            trampoline.Tick -= trampolineTick;
            trampoline.Stop();
            timerRunning = false;
            this.ExpireSolution(true);
        }

        #endregion
    }
}
