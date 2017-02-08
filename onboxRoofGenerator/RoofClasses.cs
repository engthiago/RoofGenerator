using Autodesk.Revit.DB;
using Autodesk.Revit.DB.IFC;
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
        internal IList<Edge> Edges { get; set; }
        internal Curve Curve { get; set; }
        internal RoofLineType RoofLineType { get; set; }
        internal FootPrintRoof CurrentRoof { get; set; }

        internal IList<Edge> RelatedRidgeEaves { get; set; }
        internal IList<Face> RelatedPanelFaces { get; set; }

        public EdgeInfo()
        {
            RoofLineType = RoofLineType.Undefined;
            RelatedRidgeEaves = new List<Edge>();
            RelatedPanelFaces = new List<Face>();
        }

        internal IList<EdgeInfo> GetEndConditions(int index)
        {
            IList<EdgeInfo> resultingEdgeInfo = new List<EdgeInfo>();
            if (index != 0 && index != 1) return resultingEdgeInfo;

            IList<EdgeInfo> allRoofEdgeInfo = Support.GetRoofEdgeInfoList(CurrentRoof).Union(Support.GetRoofEdgeInfoList(CurrentRoof, false)).ToList();

            foreach (EdgeInfo currentCurve in allRoofEdgeInfo)
            {
                if (!currentCurve.Curve.IsAlmostEqualTo(Curve))
                {
                    if (Curve.GetEndPoint(index).IsAlmostEqualTo(currentCurve.Curve.GetEndPoint(0)) || Curve.GetEndPoint(index).IsAlmostEqualTo(currentCurve.Curve.GetEndPoint(1)))
                    {
                        resultingEdgeInfo.Add(currentCurve);
                    } 
                }
            }
            return resultingEdgeInfo;
        }

        internal XYZ ProjectRidgePointOnEave(double distanceAlongRidge)
        {
            if (RoofLineType != RoofLineType.Ridge && RoofLineType != RoofLineType.RidgeSinglePanel)
                throw new Exception("EdgeInfo is not a Ridge!");

            ElementId LevelId = CurrentRoof.LevelId;
            if (LevelId == null || LevelId == ElementId.InvalidElementId)
                throw new Exception("Roof level not found!");

            Level currentRoofLevel = CurrentRoof.Document.GetElement(LevelId) as Level;
            Parameter currentRoofBaseOffsetParameter = CurrentRoof.get_Parameter(BuiltInParameter.ROOF_BASE_LEVEL_PARAM);

            if (currentRoofBaseOffsetParameter == null)
                throw new Exception("Roof level offset not found!");

            double currentRoofBaseOffset = currentRoofBaseOffsetParameter.AsDouble();
            double currentRoofTotalHeight = currentRoofLevel.ProjectElevation + currentRoofBaseOffset;

            double parameterDistance = Curve.GetEndParameter(0) + distanceAlongRidge;
            XYZ ridgePoint = Curve.Evaluate(parameterDistance, false);

            if (RelatedRidgeEaves.Count == 0)
                throw new Exception("No ridge related eave were found!");

            XYZ projectionPoint = null;

            foreach (Edge currentEdge in RelatedRidgeEaves)
            {
                Line firstEaveLine = (currentEdge.AsCurve() as Line);
                if (firstEaveLine == null) continue;

                Line firstEaveLineFlatten = firstEaveLine.Flatten(currentRoofTotalHeight);
                firstEaveLineFlatten.MakeUnbound();

                IntersectionResult projectionResult = firstEaveLineFlatten.Project(ridgePoint);
                if (projectionResult == null) continue;

                projectionPoint = projectionResult.XYZPoint;
                break;
            }

            if (projectionPoint == null) throw new Exception("No projection between ridge and eave could be stabilished!");

            return projectionPoint;
        }
        
        internal XYZ ProjectRidgePointOnOverhang(double distanceAlongRidge)
        {
            XYZ currentPointOnEave = ProjectRidgePointOnEave(distanceAlongRidge);
            XYZ currentPointOnRidge = Curve.Evaluate(Curve.GetEndParameter(0) + distanceAlongRidge, false);

            double dist = CurrentRoof.get_Overhang(CurrentRoof.GetProfiles().get_Item(0).get_Item(0));
            Line l = Line.CreateBound(currentPointOnEave, currentPointOnRidge).Flatten(currentPointOnEave.Z);
            return currentPointOnEave.Add(l.Direction.Multiply(dist));
        }

        internal IList<Face> GetRelatedPanels()
        {
            return Support.GetEdgeRelatedPanels(Edges[0], CurrentRoof);
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
