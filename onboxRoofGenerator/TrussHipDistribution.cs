using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI.Selection;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB.Structure;

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

            //if (currentHipRef.GlobalPoint != null)
            //    DEBUG.CreateDebugPoint(doc, currentHipRef.GlobalPoint);

            //DEBUG.CreateDebugPoint(doc, currentEdgeInfo.Curve.Evaluate(0.5, true));

            if (currentEdgeInfo == null)
            {
                message = "Nenhuma linha inferior pode ser obtida a partir da seleção";
                return Result.Failed;
            }

            if (currentEdgeInfo.Curve as Line == null)
            {
                message = "Rincão deve ser uma linha reta";
                return Result.Failed;
            }

            Line currentHipLine = currentEdgeInfo.Curve as Line;
            ISelectionFilter firstSupportFilter = new RoofClasses.SelectionFilters.SupportsSelectionFilter(currentHipLine.Flatten().Direction.Rotate(45));
            ISelectionFilter secondSupportFilter = new RoofClasses.SelectionFilters.SupportsSelectionFilter(currentHipLine.Flatten().Direction.Rotate(-45));
            Element firstSupport = null;
            Element secondSupport = null;

            try
            {
                Reference firstSupportRef = sel.PickObject(ObjectType.Element, firstSupportFilter);
                Reference SecondSupportRef = sel.PickObject(ObjectType.Element, secondSupportFilter);

                firstSupport = doc.GetElement(firstSupportRef);
                secondSupport = doc.GetElement(SecondSupportRef);
            }
            catch
            {
            }

            if (firstSupport == null || secondSupport == null)
            {
                message = "Nenhum suporte foi selecionado";
                return Result.Failed;
            }

            Element tTypeElement = new FilteredElementCollector(doc).OfClass(typeof(FamilySymbol)).Where(fsy => fsy is TrussType).ToList().FirstOrDefault();
            if (tTypeElement == null)
            {
                message = "Nenhum tipo de treliça foi encontrada no projeto, por favor, carregue um tipo e rode este comando novamente";
                return Result.Failed;
            }
            TrussType tType = tTypeElement as TrussType;

            Managers.TrussHipManager trussHipManager = new Managers.TrussHipManager();
            trussHipManager.CreateTrussInfo(doc, currentHipLine.Evaluate(0.5, true), currentEdgeInfo, firstSupport, secondSupport, tType);

            return Result.Succeeded;
        }
    }
}
