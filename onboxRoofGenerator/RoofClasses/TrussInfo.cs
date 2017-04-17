using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace onboxRoofGenerator.RoofClasses
{
    class TrussInfo
    {
        public XYZ FirstPoint { get; set; }
        public XYZ SecondPoint { get; set; }
        public double Height { get; set; }
        public CurveArray TopChords { get; set; }
        public CurveArray BottomChords { get; set; }


        internal Line FootPrintLine { get { return GetFootPrinLine(); } }

        public TrussInfo(XYZ firstPoint, XYZ secondPoint, double height)
        {
            FirstPoint = firstPoint;
            SecondPoint = secondPoint;
            Height = height;
        }

        private Line GetFootPrinLine()
        {
            if (FirstPoint != null && SecondPoint != null)
                return Line.CreateBound(FirstPoint, SecondPoint);

            return null;
        }
    }
}
