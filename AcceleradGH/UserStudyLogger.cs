using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;

using Grasshopper.Kernel;
using Rhino.Geometry;

using Rhino;
using Rhino.Commands;
using Rhino.Display;
using Rhino.DocObjects;

namespace AcceleradGH
{
    public class UserStudyLogger : GH_Component
    {
        /// <summary>
        /// Log file.
        /// </summary>
        protected StreamWriter file = null;

        /// <summary>
        /// Timer to end session.
        /// </summary>
        protected Timer timer = null;

        /// <summary>
        /// Previous month value.
        /// </summary>
        protected int oldMonth = -1;

        /// <summary>
        /// Previous day of month value.
        /// </summary>
        protected int oldDay = -1;

        /// <summary>
        /// Previous hour of day value.
        /// </summary>
        protected double oldHour = -1.0;

        /// <summary>
        /// Previous view point.
        /// </summary>
        protected Point3d oldPoint = new Point3d(0, 0, 0);

        /// <summary>
        /// Previous view direction.
        /// </summary>
        protected Vector3d oldDirection = new Vector3d(0, 0, 0);

        /// <summary>
        /// Previous DGP value.
        /// </summary>
        protected double oldDGP = -1.0;

        /// <summary>
        /// Number of adds in current command.
        /// </summary>
        protected int adds = 0;

        /// <summary>
        /// Number of deletes in current command.
        /// </summary>
        protected int removes = 0;

        /// <summary>
        /// Number of undeletes in current command.
        /// </summary>
        protected int unremoves = 0;

        /// <summary>
        /// Initializes a new instance of the UserStudyLogger class.
        /// </summary>
        public UserStudyLogger()
            : base("User Study Logger", "Logger",
                "Log User Actions",
                "Accelerad", "Study")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddBooleanParameter("Start", "S", "Start the session", GH_ParamAccess.item, false);
            //pManager.AddIntegerParameter("Participant Number", "PN", "Participant ID number", GH_ParamAccess.item, 0);
            pManager.AddNumberParameter("Session Length", "SL", "Session length in minutes", GH_ParamAccess.item, 0.5);
            pManager.AddTextParameter("Log Path", "L", "Log file path", GH_ParamAccess.item, (string)null);

            pManager.AddIntegerParameter("Month", "M", "Month [1, 12]", GH_ParamAccess.item, 1);
            pManager.AddIntegerParameter("Day", "D", "Day of month [1, DaysInMonth]", GH_ParamAccess.item, 1);
            pManager.AddNumberParameter("Hour", "H", "Hour of day [0, 24]", GH_ParamAccess.item, 12.0);

            pManager.AddPointParameter("View Point", "VP", "View point", GH_ParamAccess.item);
            pManager.AddVectorParameter("View Direction", "VD", "View direction", GH_ParamAccess.item);

            pManager.AddBooleanParameter("Run DGP", "R", "Run DGP simulation in DIVA", GH_ParamAccess.item, false);
            pManager.AddNumberParameter("Daylight Glare Probability", "DGP", "Daylight glare probability from DIVA", GH_ParamAccess.item, -1.0);
            pManager.AddTextParameter("Simulation Name", "SN", "Simulation name", GH_ParamAccess.item, (string)null);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            //pManager.AddBooleanParameter("Run DGP", "R", "Run DGP simulation in DIVA (pass through)", GH_ParamAccess.item);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            if (file == null)
            {
                // See if we are started the component
                bool running = false;
                DA.GetData(0, ref running);
                if (!running) return;

                double minutes = 0.0;
                DA.GetData(1, ref minutes);
                if (!(minutes > 0.0)) return;

                string path = null;
                DA.GetData(2, ref path);
                if (path == null) return;

                DialogResult result = MessageBox.Show(string.Format("Your {0}-minute session will begin when you press OK.", minutes), "Start Session", MessageBoxButtons.OKCancel, MessageBoxIcon.Information);
                if (result != DialogResult.OK) return;

                timer = new Timer();
                timer.Interval = (int)(minutes * 60 * 1000);
                timer.Tick += endSession;

                file = new StreamWriter(path);
                log("start");
                timer.Start();

                // Register disk access event handlers
                //RhinoDoc.NewDocument += HandleNewDocument;
                RhinoDoc.BeginOpenDocument += HandleBeginOpenDocument;
                RhinoDoc.EndOpenDocument += HandleEndOpenDocument;
                RhinoDoc.BeginSaveDocument += HandleBeginSaveDocument;
                RhinoDoc.EndSaveDocument += HandleEndSaveDocument;
                //RhinoDoc.CloseDocument += HandleCloseDocument;

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
            }

            int month = oldMonth;
            DA.GetData(3, ref month);

            int day = oldDay;
            DA.GetData(4, ref day);

            double hour = oldHour;
            DA.GetData(5, ref hour);

            if (month != oldMonth || day != oldDay || hour != oldHour)
            {
                oldMonth = month;
                oldDay = day;
                oldHour = hour;
                log(string.Format("time {0} {1} {2}", month, day, hour));
            }

            Point3d point = new Point3d(oldPoint);
            DA.GetData(6, ref point);

            Vector3d direction = new Vector3d(oldDirection);
            DA.GetData(7, ref direction);

            if (!point.Equals(oldPoint) || !direction.Equals(oldDirection))
            {
                oldPoint = point;
                oldDirection = direction;
                log(string.Format("view {0} {1}", point.ToString(), direction.ToString()));
            }

            bool runDGP = false;
            DA.GetData(8, ref runDGP);
            if (runDGP)
            {
                bool gotElapsed = false;
                string name = null;
                DA.GetData(10, ref name);
                if (!string.IsNullOrWhiteSpace(name))
                {
                    string path = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + @"\DIVA\GH_Data\" + name + @"\Glare\" + name + @".bat";
                    if (File.Exists(path))
                    {
                        FileInfo fi = new FileInfo(path);
                        DateTime modified = fi.LastWriteTime;
                        if (modified != null)
                        {
                            log(string.Format("run {0}", DateTime.Now.Subtract(modified).TotalMilliseconds));
                            gotElapsed = true;
                        }
                    }
                }
                if (!gotElapsed)
                    log("run");
            }
            //DA.SetData(0, runDGP); // Pass through

            double dgp = oldDGP;
            DA.GetData(9, ref dgp);
            if (dgp != oldDGP)
            {
                oldDGP = dgp;
                log(string.Format("dgp {0}", dgp));
            }
        }

        public void endSession(object sender, EventArgs e)
        {
            // Unregister disk access event handlers
            //RhinoDoc.NewDocument -= HandleNewDocument;
            RhinoDoc.BeginOpenDocument -= HandleBeginOpenDocument;
            RhinoDoc.EndOpenDocument -= HandleEndOpenDocument;
            RhinoDoc.BeginSaveDocument -= HandleBeginSaveDocument;
            RhinoDoc.EndSaveDocument -= HandleEndSaveDocument;
            //RhinoDoc.CloseDocument -= HandleCloseDocument;

            // Unregister command event handlers
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

            timer.Stop();
            timer.Tick -= endSession;
            log("finish");
            file.Close();
            file = null;

            MessageBox.Show("Please complete the survey", "Time's Up!", MessageBoxButtons.OK, MessageBoxIcon.Stop);
            System.Diagnostics.Process.Start("https://docs.google.com/forms/d/e/1FAIpQLSdoG65HJc6DJ8ftrhCFpKtwzyqxgfewwfIt7OOYLKp3WUhGUQ/viewform");
        }

        protected void log(string message)
        {
            if (file != null)
                file.WriteLine("{0}: {1}", DateTime.Now.ToString(), message);
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
            get { return new Guid("{09ee909f-8659-4a13-9e40-1d15e446b599}"); }
        }

        #region Disk Access Event Handlers

        //public void HandleNewDocument(Object sender, DocumentEventArgs e)
        //{
        //    EndDiskTime();
        //    Print("New Document " + e.DocumentId);
        //}

        public void HandleBeginOpenDocument(Object sender, DocumentEventArgs e)
        {
            log("begin open " + e.Document.Name);
        }

        public void HandleEndOpenDocument(Object sender, DocumentEventArgs e)
        {
            log("end open " + e.Document.Name);
        }

        public void HandleBeginSaveDocument(Object sender, DocumentEventArgs e)
        {
            log("begin save " + e.Document.Path);
        }

        public void HandleEndSaveDocument(Object sender, DocumentEventArgs e)
        {
            log("end save " + e.Document.Path);
        }

        #endregion

        #region Command Event Handlers

        public void HandleAddObject(Object sender, RhinoObjectEventArgs e)
        {
            //log("add " + e.TheObject.ObjectType.ToString());
            adds++;
        }

        public void HandleDeleteObject(Object sender, RhinoObjectEventArgs e)
        {
            //log("delete " + e.TheObject.ObjectType.ToString());
            removes++;
        }

        public void HandleUndeleteObject(Object sender, RhinoObjectEventArgs e) // Similar to add
        {
            //log("undelete " + e.TheObject.ObjectType.ToString());
            unremoves++;
        }

        public void HandleModifyAttributes(Object sender, RhinoModifyObjectAttributesEventArgs e)
        {
            if (e.OldAttributes.LayerIndex != e.NewAttributes.LayerIndex)
            {
                log("layer " + Rhino.RhinoDoc.ActiveDoc.Layers[e.OldAttributes.LayerIndex].Name + " to " + Rhino.RhinoDoc.ActiveDoc.Layers[e.NewAttributes.LayerIndex].Name);
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
                log((e.NewState.IsVisible ? "show" : "hide") + " layer " + Rhino.RhinoDoc.ActiveDoc.Layers[e.LayerIndex].Name);
            }
        }

        public void HandleBeginCommand(Object sender, CommandEventArgs e)
        {
            log("begin command " + e.CommandEnglishName);
            adds = removes = unremoves = 0;
        }

        public void HandleEndCommand(Object sender, CommandEventArgs e)
        {
            log("end command " + e.CommandEnglishName + string.Format(" {0} {1} {2}", adds, removes, unremoves));
        }
        #endregion
    }
}