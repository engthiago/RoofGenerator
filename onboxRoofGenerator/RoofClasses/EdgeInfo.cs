using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace onboxRoofGenerator.RoofClasses
{
    //TODO create child class of this one, to handle Ridges and merged Gables and other specific functions
    class EdgeInfo
    {
        internal IList<Edge> Edges { get; set; }
        internal Curve Curve { get; set; }
        internal RoofLineType RoofLineType { get; set; }
        internal FootPrintRoof CurrentRoof { get; set; }

        internal IList<Edge> RelatedRidgeEaves { get; set; }
        internal IList<Face> RelatedPanelFaces { get; set; }

        public override string ToString()
        {
            return RoofLineType.ToString() + ": " + Curve.ApproximateLength + "'";
        }

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

        internal XYZ GetTrussTopPoint(XYZ currentPointOnRidge)
        {

            if (RoofLineType == RoofLineType.Ridge)
                return currentPointOnRidge;

            //If the is not a Ridge this MUST be a SinglePanelRidge
            if (RoofLineType == RoofLineType.RidgeSinglePanel)
                return currentPointOnRidge;

            Line currentRidgeLine = Curve.Clone() as Line;
            if (currentRidgeLine == null)
                throw new Exception("The ridge is not a straight line!");

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

                    double currentDist = targetGeoLine.Project(currentPointOnRidge).Distance;
                    if (currentDist < minDist)
                    {
                        minDist = currentDist;
                        targetEave = currentCurve;
                        projectedPoint = targetGeoLine.Project(currentPointOnRidge).XYZPoint;
                    }
                }
            }

            double overHang = 0;
            try { overHang = CurrentRoof.get_Overhang(targetEave); }
            catch { }

            XYZ ridgePointFlatten = new XYZ(currentPointOnRidge.X, currentPointOnRidge.Y, currentRoofTotalHeight);

            //We just need to get the side that the eave is to move the point to that direction
            //so we dont need to get a specific eave, lets just project the first one with infinite bounds to get the direction
            if (RelatedRidgeEaves == null || RelatedRidgeEaves.Count == 0)
                RelatedRidgeEaves = GetRelatedEaves();

            if (RelatedRidgeEaves == null || RelatedRidgeEaves.Count == 0)
                throw new Exception("Related eave or eaves to current singleRidge was not found");


            Curve eaveCurve = RelatedRidgeEaves[0].AsCurve();
            if (eaveCurve as Line == null) throw new Exception("Related eave is not a straight line!");

            Line eaveLine = eaveCurve as Line;

            XYZ lineIntersectionPoint = GeometrySupport.GetRoofIntersectionFlattenLines(currentRidgeLine, ridgePointFlatten, eaveLine, currentRoofTotalHeight);

            if (lineIntersectionPoint == null) throw new Exception("No Intersection between eave could be estabilished!");

            XYZ overHangdirection = Line.CreateBound(projectedPoint, lineIntersectionPoint).Direction.Normalize();
            XYZ pointOnOverhang = projectedPoint.Add(overHangdirection.Multiply(overHang));

            //We will get the point on the overhang because if we are working with a single panel ridge it may have overhangs
            XYZ pointOnSupport = GetSupportPoint(pointOnOverhang, currentRidgeLine.Direction.Normalize());

            //Now we will shoot the point up on the Roof
            XYZ startingPoint = new XYZ(pointOnSupport.X, pointOnSupport.Y, pointOnSupport.Z - 1);
            ReferenceIntersector currentRefIntersect = new ReferenceIntersector(CurrentRoof.Id, FindReferenceTarget.Element, CurrentRoof.Document.ActiveView as View3D);
            ReferenceWithContext currenRefContext = currentRefIntersect.FindNearest(startingPoint, XYZ.BasisZ);

            if (currenRefContext == null)
                return null;

            XYZ projectedPointOnRoof = currenRefContext.GetReference().GlobalPoint;

            return projectedPointOnRoof;
        }

        internal IList<XYZ> ProjectRidgePointOnEaves(XYZ currentPointOnRidge)
        {

            if (RoofLineType != RoofLineType.Ridge && RoofLineType != RoofLineType.RidgeSinglePanel)
                throw new Exception("EdgeInfo is not a Ridge!");

            double currentRoofTotalHeight = GetCurrentRoofHeight();

            if (RelatedRidgeEaves.Count == 0)
                RelatedRidgeEaves = GetRelatedEaves();
            
            if (RelatedRidgeEaves.Count == 0)
                throw new Exception("No ridge related eave was found!");

            IList<XYZ> projectionPoints = new List<XYZ>();

            Line currentRidgeLine = Curve.Clone() as Line;
            if (currentRidgeLine == null)
                throw new Exception("The ridge is not a straight line!");

            //TODO try to extract the this to use on get top truss point
            XYZ crossedDirection = currentRidgeLine.Direction.CrossProduct(XYZ.BasisZ);
            Line CrossedRidgeLine = Line.CreateBound(currentPointOnRidge, crossedDirection.Add(currentPointOnRidge)).Flatten(currentRoofTotalHeight);
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

            if (projectionPoints == null || projectionPoints.Count == 0)
            {
                //Document doc = CurrentRoof.Document;
                //FamilySymbol fs = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_GenericModel).WhereElementIsElementType().Where(type => type.Name.Contains("DebugPoint2")).FirstOrDefault() as FamilySymbol;
                //doc.Create.NewFamilyInstance(Edges[0].Evaluate(0.5), fs, Autodesk.Revit.DB.Structure.StructuralType.NonStructural);

                //throw new Exception("No projection between ridge and eave could be stabilished!");
            }

            return projectionPoints;

        }

        internal IList<XYZ> GetEavePointsOnOverhang(XYZ currentPointOnRidge)
        {
            IList<XYZ> ProjectedPoints = new List<XYZ>();
            //XYZ currentPointOnRidge = Curve.Evaluate(Curve.GetEndParameter(0) + distanceAlongRidge, false);
            foreach (XYZ currentPointOnEave in ProjectRidgePointOnEaves(currentPointOnRidge))
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
                catch { }

                currentPointOnRidge = new XYZ(currentPointOnRidge.X, currentPointOnRidge.Y, currentRoofTotalHeight);
                Line l = Line.CreateBound(projectedPoint, currentPointOnRidge);

                ProjectedPoints.Add(currentPointOnEave.Add(l.Direction.Multiply(overHang)));
            }
            return ProjectedPoints;
        }

        internal IList<XYZ> GetEavePointsOnSupports(XYZ currentPointOnRidge)
        {
            IList<XYZ> overhangPoints = GetEavePointsOnOverhang(currentPointOnRidge);
            IList<XYZ> projectedPoints = new List<XYZ>();
            foreach (XYZ currentPoint in overhangPoints)
            {
                Line l = Curve as Line;
                if (l == null)
                    throw new Exception("CurrentEdge is not a straight line!");
                XYZ currentProjectedPoint = GetSupportPoint(currentPoint, l.Direction.Normalize());
                projectedPoints.Add(currentProjectedPoint);
            }
            return projectedPoints;
        }

        internal IList<XYZ> ProjectSupportPointsOnRoof(XYZ currentPointOnRidge)
        {
            IList<XYZ> supportPoints = GetEavePointsOnSupports(currentPointOnRidge);

            //if (supportPoints.Count < 1 || supportPoints.Count > 2)
            //    throw new Exception("Invalid number of support points for the truss");

            IList<XYZ> resultingPoints = new List<XYZ>();

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
                        XYZ ridgePointFlaten = new XYZ(currentPointOnRidge.X, currentPointOnRidge.Y, resultingPoints[0].Z);
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

        internal IList<Edge> GetRelatedEaves()
        {
            //TODO optimise this!!!! We are going through the entire process of getting all EdgeInfo again
            //The good thing is that this will not run too often
            return Support.GetRidgeInfoList(Edges[0], Support.GetRoofEdgeInfoList(CurrentRoof, false).Union(Support.GetRoofEdgeInfoList(CurrentRoof, true)).ToList());
        }

        internal XYZ GetSupportPoint(XYZ overhangPoint, XYZ optionalDirection, double searchBoundings = 1)
        {
            if (searchBoundings != 1)
            {
                if (searchBoundings <= 0)
                    searchBoundings = 1;
            }

            XYZ minPoint = overhangPoint.Add(XYZ.BasisX.Multiply(-searchBoundings/2)).Add(XYZ.BasisY.Multiply(-searchBoundings / 2)).Add(XYZ.BasisZ.Multiply(-searchBoundings / 2));
            XYZ maxPoint = overhangPoint.Add(XYZ.BasisX.Multiply(searchBoundings / 2)).Add(XYZ.BasisY.Multiply(searchBoundings / 2)).Add(XYZ.BasisZ.Multiply(searchBoundings / 2));
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

                //Prevents that the truss uses Structural Beams hosted on other Trusses as supports
                //In other words, prevents that the trusses uses other trusses as supports
                if (currentElem.Category.Id.IntegerValue == BuiltInCategory.OST_StructuralFraming.GetHashCode())
                {
                    FamilyInstance currentFraming = currentElem as FamilyInstance;
                    if (currentFraming != null)
                    {
                        Element currentHost = currentFraming.Host;
                        if (currentHost != null)
                        {
                            if (currentHost.Category.Id.IntegerValue == BuiltInCategory.OST_StructuralTruss.GetHashCode())
                                continue;
                        }
                    }
                }

                Curve currentCurve = (currentElem.Location as LocationCurve).Curve;

                if (optionalDirection != null)
                {
                    if (currentCurve as Line == null) continue;
                    double currentAngle = (currentCurve as Line).Direction.Normalize().AngleTo(optionalDirection);

                    if (!currentAngle.IsAlmostEqualTo(0, 0.05) && !currentAngle.IsAlmostEqualTo(Math.PI, 0.05) && !currentAngle.IsAlmostEqualTo(2 * Math.PI, 0.05))
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

        internal double GetCurrentRoofHeight()
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
}
