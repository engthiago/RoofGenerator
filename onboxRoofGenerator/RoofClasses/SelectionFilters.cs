using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace onboxRoofGenerator.RoofClasses
{
    class SelectionFilters
    {
        internal class FootPrintRoofSelFilter : ISelectionFilter
        {
            public bool AllowElement(Element elem)
            {
                if ((elem as FootPrintRoof) != null)
                    return true;
                else
                    return false;
            }

            public bool AllowReference(Reference reference, XYZ position)
            {
                return false;
            }
        }

        internal class RidgeSelectionFilter : ISelectionFilter
        {
            private Document doc;

            public RidgeSelectionFilter(Document targetDocument)
            {
                doc = targetDocument;
            }

            public bool AllowElement(Element elem)
            {
                if (elem is FootPrintRoof)
                    return true;
                else
                    return false;
            }

            public bool AllowReference(Reference reference, XYZ position)
            {
                if (doc == null)
                    return false;
                
                Element currentFootPrintElem = doc.GetElement(reference);

                if (currentFootPrintElem == null)
                    return false;

                FootPrintRoof currentFootPrintRoof = currentFootPrintElem as FootPrintRoof;

                if (currentFootPrintRoof == null)
                    return false;

                Edge currentEdge = Support.GetEdgeFromReference(reference, currentFootPrintRoof);

                IList<PlanarFace> pfaces = new List<PlanarFace>();
                Support.IsListOfPlanarFaces(HostObjectUtils.GetTopFaces(currentFootPrintRoof), currentFootPrintRoof, out pfaces);
                                
                EdgeInfo currentInfo = Support.GetCurveInformation(currentFootPrintRoof, currentEdge.AsCurve(), pfaces);

                System.Diagnostics.Debug.WriteLine(currentInfo.RoofLineType.ToString());

                if (currentInfo.RoofLineType != RoofLineType.Ridge && currentInfo.RoofLineType != RoofLineType.RidgeSinglePanel)
                    return false;

                return true;
            }
        }

        internal class SupportsSelectionFilter : ISelectionFilter
        {
            XYZ Direction { get; set; }

            public SupportsSelectionFilter(XYZ targetDirection)
            {
                Direction = targetDirection;
            }

            public bool AllowElement(Element elem)
            {
                if (elem.Category.Id.IntegerValue == BuiltInCategory.OST_Walls.GetHashCode() ||
                    elem.Category.Id.IntegerValue == BuiltInCategory.OST_StructuralFraming.GetHashCode() //||
                    //elem is ReferencePlane
                        )
                {
                    if (elem.Location is LocationCurve)
                    {
                        Curve elemLocationCurve = (elem.Location as LocationCurve).Curve;

                        if (elemLocationCurve is Line 
                           // || elemLocationCurve is Arc
                            )
                        {
                            Line elemLocationLine = elemLocationCurve as Line;

                            if (Math.Abs(elemLocationLine.Direction.DotProduct(Direction)).IsAlmostEqualTo(1, 0.02))
                                return true;
                        }
                        

                    }
                }


                return false;
            }

            public bool AllowReference(Reference reference, XYZ position)
            {
                return false;
            }
        }
    }
}
