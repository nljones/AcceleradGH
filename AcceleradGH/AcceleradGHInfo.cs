using System;
using System.Drawing;
using Grasshopper.Kernel;

namespace AcceleradGH
{
    public class AcceleradGHInfo : GH_AssemblyInfo
    {
        public override string Name
        {
            get
            {
                return "AcceleradGH";
            }
        }
        public override Bitmap Icon
        {
            get
            {
                //Return a 24x24 pixel bitmap to represent this GHA library.
                return Properties.Resources.vfd;
            }
        }
        public override string Description
        {
            get
            {
                //Return a short string describing the purpose of this GHA library.
                return "Components for Accelerad";
            }
        }
        public override Guid Id
        {
            get
            {
                return new Guid("ea9f0eaf-4a68-44c7-a22c-7b8cf006f931");
            }
        }

        public override string AuthorName
        {
            get
            {
                //Return a string identifying you or your company.
                return "Nathaniel Jones";
            }
        }
        public override string AuthorContact
        {
            get
            {
                //Return a string representing your preferred contact details.
                return "nljones@mit.edu";
            }
        }
    }
}
