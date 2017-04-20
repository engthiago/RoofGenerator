using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI.Selection;
using Autodesk.Revit.Attributes;

namespace onboxRoofGenerator
{
    [Transaction(TransactionMode.Manual)]
    class TrussHipDistribution : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            Document doc = uidoc.Document;
            Selection sel = uidoc.Selection;

            ISelectionFilter hipSelFilter = new RoofClasses.SelectionFilters.StraightLinesAndFacesHipSelectionFilter(doc);
            Reference currentHipRef = sel.PickObject(ObjectType.Edge, hipSelFilter, "Selecione um rincão para posicionar as treliças");

            RoofClasses.EdgeInfo currentEdgeInfo = Support.GetMostSimilarEdgeInfo(currentHipRef, doc);

            if (currentHipRef.GlobalPoint != null)
                DEBUG.CreateDebugPoint(doc, currentHipRef.GlobalPoint);

            DEBUG.CreateDebugPoint(doc, currentEdgeInfo.Curve.Evaluate(0.5, true));

            return Result.Succeeded;
        }
    }
}
