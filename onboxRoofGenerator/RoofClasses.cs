using Autodesk.Revit.DB;
using Autodesk.Revit.UI.Selection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace onboxRoofGenerator
{
    enum RoofLineType { Hip, Ridge, RidgeSinglePanel, Valley, Eave, Gable, Undefined };

    class EdgeInfo
    {
        internal Edge edge;
        internal Curve curve;
        internal RoofLineType roofLineType;

        internal IList<Edge> relatedRidgeEaves;
        internal IList<Face> relatedPanelFaces;

        public EdgeInfo()
        {
            roofLineType = RoofLineType.Undefined;
            relatedRidgeEaves = new List<Edge>();
            relatedPanelFaces = new List<Face>();
        }
    }

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


}
