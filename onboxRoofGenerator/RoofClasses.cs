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

    //TODO create child class of this one, to handle Ridges and merged Gables and other specific functions
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

        #region Deprecated
        //internal XYZ ProjectRidgePointOnEave(double distanceAlongRidge)
        //{
        //    if (RoofLineType != RoofLineType.Ridge && RoofLineType != RoofLineType.RidgeSinglePanel)
        //        throw new Exception("EdgeInfo is not a Ridge!");

        //    ElementId LevelId = CurrentRoof.LevelId;
        //    if (LevelId == null || LevelId == ElementId.InvalidElementId)
        //        throw new Exception("Roof level not found!");

        //    Level currentRoofLevel = CurrentRoof.Document.GetElement(LevelId) as Level;
        //    Parameter currentRoofBaseOffsetParameter = CurrentRoof.get_Parameter(BuiltInParameter.ROOF_BASE_LEVEL_PARAM);

        //    if (currentRoofBaseOffsetParameter == null)
        //        throw new Exception("Roof level offset not found!");

        //    double currentRoofBaseOffset = currentRoofBaseOffsetParameter.AsDouble();
        //    double currentRoofTotalHeight = currentRoofLevel.ProjectElevation + currentRoofBaseOffset;

        //    double parameterDistance = Curve.GetEndParameter(0) + distanceAlongRidge;
        //    XYZ ridgePoint = Curve.Evaluate(parameterDistance, false);

        //    if (RelatedRidgeEaves.Count == 0)
        //        throw new Exception("No ridge related eave was found!");

        //    XYZ projectionPoint = null;

        //    foreach (Edge currentEdge in RelatedRidgeEaves)
        //    {
        //        Line firstEaveLine = (currentEdge.AsCurve() as Line);
        //        if (firstEaveLine == null) continue;

        //        Line firstEaveLineFlatten = firstEaveLine.Flatten(currentRoofTotalHeight);
        //        firstEaveLineFlatten.MakeUnbound();

        //        IntersectionResult projectionResult = firstEaveLineFlatten.Project(ridgePoint);
        //        if (projectionResult == null) continue;

        //        projectionPoint = projectionResult.XYZPoint;
        //        break;
        //    }

        //    if (projectionPoint == null) throw new Exception("No projection between ridge and eave could be stabilished!");

        //    return projectionPoint;
        //}

        //internal XYZ ProjectRidgePointOnOverhang(double distanceAlongRidge)
        //{
        //    XYZ currentPointOnEave = ProjectRidgePointOnEave(distanceAlongRidge);
        //    XYZ currentPointOnRidge = Curve.Evaluate(Curve.GetEndParameter(0) + distanceAlongRidge, false);

        //    double dist = CurrentRoof.get_Overhang(CurrentRoof.GetProfiles().get_Item(0).get_Item(0));
        //    Line l = Line.CreateBound(currentPointOnEave, currentPointOnRidge).Flatten(currentPointOnEave.Z);
        //    return currentPointOnEave.Add(l.Direction.Multiply(dist));
        //} 
        #endregion

        internal IList<XYZ> ProjectRidgePointOnEaves(double distanceAlongRidge)
        {

            if (RoofLineType != RoofLineType.Ridge && RoofLineType != RoofLineType.RidgeSinglePanel)
                throw new Exception("EdgeInfo is not a Ridge!");

            double currentRoofTotalHeight = GetCurrentRoofHeight();

            double parameterDistance = Curve.GetEndParameter(0) + distanceAlongRidge;
            XYZ ridgePoint = Curve.Evaluate(parameterDistance, false);

            if (RelatedRidgeEaves.Count == 0)
                throw new Exception("No ridge related eave was found!");

            IList<XYZ> projectionPoints = new List<XYZ>();

            Line currentRidgeLine = Curve.Clone() as Line;
            if (currentRidgeLine == null)
                throw new Exception("The ridge is not a straight line!");

            XYZ crossedDirection = currentRidgeLine.Direction.CrossProduct(XYZ.BasisZ);
            Line CrossedRidgeLine = Line.CreateBound(ridgePoint, crossedDirection.Add(ridgePoint)).Flatten(currentRoofTotalHeight);
            Line CrossedRidgeLineFlatten = (CrossedRidgeLine.Clone() as Line);
            CrossedRidgeLineFlatten.MakeUnbound();

            foreach (Edge currentEdge in RelatedRidgeEaves)
            {
                Line firstEaveLine = (currentEdge.AsCurve() as Line);
                if (firstEaveLine == null) continue;

                Line firstEaveLineFlatten = firstEaveLine.Flatten(currentRoofTotalHeight);

                IntersectionResultArray projectionResultArr = null;

                CrossedRidgeLineFlatten.Intersect(firstEaveLineFlatten, out projectionResultArr);

                if (projectionResultArr == null || projectionResultArr.Size == 0) continue;

                XYZ currentIntersPoint = projectionResultArr.get_Item(0).XYZPoint;
                if (currentIntersPoint == null) continue;

                projectionPoints.Add(currentIntersPoint);

            }

            if (projectionPoints == null || projectionPoints.Count == 0) throw new Exception("No projection between ridge and eave could be stabilished!");

            return projectionPoints;

        }

        internal IList<XYZ> ProjectRidgePointsOnOverhang(double distanceAlongRidge)
        {
            IList<XYZ> ProjectedPoints = new List<XYZ>();
            XYZ currentPointOnRidge = Curve.Evaluate(Curve.GetEndParameter(0) + distanceAlongRidge, false);
            foreach (XYZ currentPointOnEave in ProjectRidgePointOnEaves(distanceAlongRidge))
            {
                ModelCurveArrArray sketchModels = CurrentRoof.GetProfiles();
                double minDist = double.MaxValue;
                ModelCurve targetEave = null;
                XYZ projectedPoint = null;

                double currentRoofTotalHeight = GetCurrentRoofHeight();

                foreach (ModelCurveArray currentCurveArr in sketchModels)
                {
                    foreach (ModelCurve currentCurve in currentCurveArr)
                    {
                        Curve targetGeoCurve = currentCurve.GeometryCurve;
                        Line targetGeoLine = targetGeoCurve as Line;

                        if (targetGeoLine == null)
                            throw new Exception("Eave is not a straight line");

                        targetGeoLine = targetGeoLine.Flatten(currentRoofTotalHeight);

                        double currentDist = targetGeoLine.Project(currentPointOnEave).Distance;
                        if (currentDist < minDist)
                        {
                            minDist = currentDist;
                            targetEave = currentCurve;
                            projectedPoint = targetGeoLine.Project(currentPointOnEave).XYZPoint;
                        }
                    }
                }

                double overHang = 0;
                try { overHang = CurrentRoof.get_Overhang(targetEave); }
                catch { overHang = CurrentRoof.get_Offset(targetEave); }

                currentPointOnRidge = new XYZ(currentPointOnRidge.X, currentPointOnRidge.Y, currentRoofTotalHeight);
                Line l = Line.CreateBound(projectedPoint, currentPointOnRidge);

                ProjectedPoints.Add(currentPointOnEave.Add(l.Direction.Multiply(overHang)));
            }
            return ProjectedPoints;
        }

        internal IList<XYZ> ProjetRidgePointsOnSupports(double distanceAlongRidge)
        {
            IList<XYZ> overhangPoints = ProjectRidgePointsOnOverhang(distanceAlongRidge);
            IList<XYZ> projectedPoints = new List<XYZ>();
            foreach (XYZ currentPoint in overhangPoints)
            {
                XYZ currentProjectedPoint = GetSupportPoint(currentPoint, null);
                projectedPoints.Add(currentProjectedPoint);
            }
            return projectedPoints;
        }

        internal IList<XYZ> GetTopChords(double distanceAlongRidge)
        {
            IList<XYZ> supportPoints = ProjetRidgePointsOnSupports(distanceAlongRidge);

            if (supportPoints.Count < 1 || supportPoints.Count > 2)
                throw new Exception("Invalid number of support points for the truss");

            IList<XYZ> resultingPoints = new List<XYZ>();

            double parameterDistance = Curve.GetEndParameter(0) + distanceAlongRidge;
            XYZ ridgePoint = Curve.Evaluate(parameterDistance, false);
            ReferenceIntersector roofIntersector = new ReferenceIntersector(CurrentRoof.Id, FindReferenceTarget.Element, CurrentRoof.Document.ActiveView as View3D);

            foreach (XYZ currentSupportPoint in supportPoints)
            {
                XYZ originPoint = new XYZ(currentSupportPoint.X, currentSupportPoint.Y, GetCurrentRoofHeight() - 1);
                ReferenceWithContext roofIntersContext = roofIntersector.FindNearest(originPoint, XYZ.BasisZ);

                if (roofIntersContext == null) continue;

                XYZ currentHitPoint = roofIntersContext.GetReference().GlobalPoint;
                resultingPoints.Add(currentHitPoint);
            }

            if (resultingPoints.Count == 1)
            {
                if (RoofLineType == RoofLineType.Ridge)
                {
                    if (Support.HasSameSlopes(CurrentRoof))
                    {
                        XYZ ridgePointFlaten = new XYZ(ridgePoint.X, ridgePoint.Y, resultingPoints[0].Z);
                        Line CrossedRidgeLine = Line.CreateBound(resultingPoints[0], ridgePointFlaten);
                        XYZ crossedDirection = CrossedRidgeLine.Direction;
                        XYZ mirroredPoint = resultingPoints[0].Add(crossedDirection.Multiply(CrossedRidgeLine.ApproximateLength * 2));
                        resultingPoints.Add(mirroredPoint);
                    }
                }
            }

            return resultingPoints;
        }

        internal IList<Face> GetRelatedPanels()
        {
            return Support.GetEdgeRelatedPanels(Edges[0], CurrentRoof);
        }

        private XYZ GetSupportPoint(XYZ overhangPoint, XYZ optionalDirection)
        {
            XYZ minPoint = overhangPoint.Add(XYZ.BasisX.Multiply(-0.5)).Add(XYZ.BasisY.Multiply(-0.5)).Add(XYZ.BasisZ.Multiply(-0.5));
            XYZ maxPoint = overhangPoint.Add(XYZ.BasisX.Multiply(0.5)).Add(XYZ.BasisY.Multiply(0.5)).Add(XYZ.BasisZ.Multiply(0.5));
            //BoundingBoxXYZ bbVolume = new BoundingBoxXYZ();
            //bbVolume.Min = minPoint;
            //bbVolume.Max = maxPoint;
            //bbVolume.Enabled = true;

            BoundingBoxIntersectsFilter bbVolumeIntersFilter = new BoundingBoxIntersectsFilter(new Outline(minPoint, maxPoint));
            ElementCategoryFilter wallFilter = new ElementCategoryFilter(BuiltInCategory.OST_Walls);
            ElementCategoryFilter beamFilter = new ElementCategoryFilter(BuiltInCategory.OST_StructuralFraming);
            LogicalOrFilter categoriesFilter = new LogicalOrFilter(wallFilter, beamFilter);

            IList<Element> intersectElements = new FilteredElementCollector(CurrentRoof.Document).WherePasses(categoriesFilter).WherePasses(bbVolumeIntersFilter).WhereElementIsNotElementType().ToElements();

            Element supportElement = null;
            XYZ projetctedPoint = null;
            double minDist = double.MaxValue;
            foreach (Element currentElem in intersectElements)
            {
                if (currentElem.Location == null) continue;
                if (currentElem.Location as LocationCurve == null) continue;

                Curve currentCurve = (currentElem.Location as LocationCurve).Curve;

                if (optionalDirection != null)
                {
                    if (currentCurve as Line == null) continue;
                    double currentAngle = (currentCurve as Line).Direction.Normalize().AngleTo(optionalDirection);

                    if (!currentAngle.IsAlmostEqualTo(0, 0.05) || !currentAngle.IsAlmostEqualTo(Math.PI, 0.05) || !currentAngle.IsAlmostEqualTo(2 * Math.PI, 0.05))
                        continue;
                }

                IntersectionResult intRes = currentCurve.Project(overhangPoint);

                if (intRes == null) continue;

                projetctedPoint = intRes.XYZPoint;
                double currentDist = projetctedPoint.DistanceTo(overhangPoint);

                if (currentDist < minDist)
                {
                    minDist = currentDist;
                    supportElement = currentElem;
                }
            }

            if (projetctedPoint == null)
                return overhangPoint;

            double totalRoofHeight = GetCurrentRoofHeight();

            return new XYZ(projetctedPoint.X, projetctedPoint.Y, totalRoofHeight);
        }

        private double GetCurrentRoofHeight()
        {
            ElementId LevelId = CurrentRoof.LevelId;
            if (LevelId == null || LevelId == ElementId.InvalidElementId)
                throw new Exception("Roof level not found!");

            Level currentRoofLevel = CurrentRoof.Document.GetElement(LevelId) as Level;
            Parameter currentRoofBaseOffsetParameter = CurrentRoof.get_Parameter(BuiltInParameter.ROOF_BASE_LEVEL_PARAM);

            if (currentRoofBaseOffsetParameter == null)
                throw new Exception("Roof level offset not found!");

            double currentRoofBaseOffset = currentRoofBaseOffsetParameter.AsDouble();
            double currentRoofTotalHeight = currentRoofLevel.ProjectElevation + currentRoofBaseOffset;

            return currentRoofTotalHeight;
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
