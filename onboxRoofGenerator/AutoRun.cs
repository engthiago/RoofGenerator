using Autodesk.Revit.Attributes;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.Revit.DB;

namespace onboxRoofGenerator
{
    [Transaction(TransactionMode.Manual)]
    class AutoRun : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            Document doc = uidoc.Document;
            
            if ((uidoc.ActiveView  as View3D) == null)
            {
                message = "Por favor, rode este comando em uma vista 3d";
                return Result.Failed;
            }

            Reference currentReference = uidoc.Selection.PickObject(ObjectType.Element);
            Element currentElement = doc.GetElement(currentReference.ElementId);
            Edge currentEdge = Support.GetEdgeFromReference(currentReference, currentElement);

            EdgeInfo currentEdgeInfo = Support.GetEdgeInformation(doc, currentEdge, currentElement);

            TaskDialog.Show("Edge", currentEdgeInfo.roofLineType.ToString());

            return Result.Succeeded;
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
