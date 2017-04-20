using Autodesk.Revit.DB;
using Autodesk.Revit.DB.IFC;
using onboxRoofGenerator.RoofClasses;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace onboxRoofGenerator
{
    class Support
    {

        static internal bool HasSameSlopes(FootPrintRoof targetRoof)
        {
            //Get the slope of the entire roof
            Parameter selectedRoofSlopeParameter = targetRoof.get_Parameter(BuiltInParameter.ROOF_SLOPE);
            double selectedRoofSlope = selectedRoofSlopeParameter.AsDouble();

            //Verify if the roof has all the same slopes
            if (selectedRoofSlope <= 0) return false;

            return true;
        }

        static internal bool IsListOfPlanarFaces(IList<Reference> targetListOfReferences, Element currentElement, out IList<PlanarFace> targetListOfPlanarFaces)
        {
            targetListOfPlanarFaces = new List<PlanarFace>();
            foreach (Reference currentReference in targetListOfReferences)
            {
                Face currentFace = GetFaceFromReference(currentReference, currentElement);
                PlanarFace currentPlanarFace = null;
                if (currentFace != null)
                {
                    currentPlanarFace = currentFace as PlanarFace;
                    if (currentPlanarFace != null)
                        targetListOfPlanarFaces.Add(currentPlanarFace);
                    else
                        return false;
                }
            }
            return true;
        }

        static internal Face GetFaceFromReference(Reference targetReference, Element targetElement)
        {
            Face currentFace = null;
            if (targetReference != null && targetElement != null)
            {
                GeometryObject currentGeometryObject = targetElement.GetGeometryObjectFromReference(targetReference);
                if (currentGeometryObject != null)
                {
                    if (currentGeometryObject is Face)
                        currentFace = currentGeometryObject as Face;
                }
            }

            return currentFace;
        }

        static internal Edge GetEdgeFromReference(Reference targetReference, Element targetElement)
        {
            Edge currentEdge = null;
            if (targetReference != null && targetElement != null)
            {
                GeometryObject currentGeometryObject = targetElement.GetGeometryObjectFromReference(targetReference);
                if (currentGeometryObject != null)
                {
                    if (currentGeometryObject is Edge)
                        currentEdge = currentGeometryObject as Edge;
                }
            }

            return currentEdge;
        }

        static private CurveLoop GetOuterCurveLoop(PlanarFace targetFace)
        {
            CurveLoop currentCurveLoop = new CurveLoop();

            if (targetFace != null)
            {
                IList<XYZ> Points = new List<XYZ>();
                IList<IList<CurveLoop>> currentListOfListOfCurveLoops = ExporterIFCUtils.SortCurveLoops(targetFace.GetEdgesAsCurveLoops());

                if (currentListOfListOfCurveLoops != null)
                {
                    if (currentListOfListOfCurveLoops.Count > 0)
                    {
                        IList<CurveLoop> currentOuterLoop = currentListOfListOfCurveLoops[0];
                        if (currentOuterLoop != null)
                        {
                            if (currentOuterLoop.Count > 0)
                                currentCurveLoop = currentOuterLoop[0];
                        }
                    }

                }
            }
            return currentCurveLoop;
        }

        static private XYZ GetExtendedPoint(XYZ startPosition, PlanarFace targetPlanarFace, double amount = 0.1)
        {
            if (targetPlanarFace != null)
            {
                CurveLoop currentCurveLoop = GetOuterCurveLoop(targetPlanarFace);

                foreach (Curve currentCurve in currentCurveLoop)
                {
                    if (currentCurve.GetEndPoint(0).Z.IsAlmostEqualTo(targetPlanarFace.Origin.Z) && currentCurve.GetEndPoint(1).Z.IsAlmostEqualTo(targetPlanarFace.Origin.Z))
                    {
                        XYZ p0 = startPosition;
                        XYZ p1 = startPosition;

                        if (p0.IsAlmostEqualTo(currentCurve.GetEndPoint(0)))
                            p1 = currentCurve.GetEndPoint(1);
                        else
                            p1 = currentCurve.GetEndPoint(0);

                        XYZ lineDirection = Line.CreateBound(p0, p1).Direction;
                        XYZ extendendPoint = p0 + (lineDirection.Negate() * amount);

                        return extendendPoint;
                    }
                }
            }
            return startPosition;
        }

        static internal EdgeInfo GetCurveInformation(Element targetRoof, Curve targetCurve, IList<PlanarFace> targetPlanarFaceList)
        {
            if (targetPlanarFaceList != null && targetRoof != null && (targetRoof as FootPrintRoof) != null)
            {
                FootPrintRoof currentRoof = targetRoof as FootPrintRoof;
                foreach (PlanarFace currentPlanarFace in targetPlanarFaceList)
                {
                    EdgeArrayArray EdgeLoops = currentPlanarFace.EdgeLoops;
                    foreach (EdgeArray currentEdgeArray in EdgeLoops)
                    {
                        foreach (Edge currentEdge in currentEdgeArray)
                        {
                            if (currentEdge != null)
                            {
                                IList<Edge> currentEdges = new List<Edge>();
                                currentEdges.Add(currentEdge);
                                EdgeInfo currentEdgeInfo = new EdgeInfo();
                                Curve edgeCurve = currentEdge.AsCurve();

                                if (edgeCurve.IsAlmostEqualTo(targetCurve))
                                {
                                    if (edgeCurve.GetEndPoint(0).Z.IsAlmostEqualTo(currentPlanarFace.Origin.Z) && edgeCurve.GetEndPoint(1).Z.IsAlmostEqualTo(currentPlanarFace.Origin.Z))
                                    {
                                        currentEdgeInfo = new EdgeInfo { Edges = currentEdges, Curve = edgeCurve, RoofLineType = RoofLineType.Eave, CurrentRoof = currentRoof };
                                        currentEdgeInfo.RelatedPanelFaces = GetEdgeRelatedPanels(currentEdge, targetPlanarFaceList);
                                        return currentEdgeInfo;
                                    }
                                    else if (edgeCurve.GetEndPoint(0).Z.IsAlmostEqualTo(currentPlanarFace.Origin.Z) || edgeCurve.GetEndPoint(1).Z.IsAlmostEqualTo(currentPlanarFace.Origin.Z))
                                    {
                                        PlanarFace firstFace = currentEdge.GetFace(0) as PlanarFace;
                                        PlanarFace secondFace = currentEdge.GetFace(1) as PlanarFace;

                                        if (!targetPlanarFaceList.Contains(firstFace) || !targetPlanarFaceList.Contains(secondFace))
                                        {
                                            currentEdgeInfo = new EdgeInfo { Edges = currentEdges, Curve = edgeCurve, RoofLineType = RoofLineType.Gable, CurrentRoof = currentRoof };
                                            currentEdgeInfo.RelatedPanelFaces = GetEdgeRelatedPanels(currentEdge, targetPlanarFaceList);
                                            return currentEdgeInfo;
                                        }
                                        else
                                        {
                                            if (GetOuterCurveLoop(firstFace).Count() == 3 || GetOuterCurveLoop(secondFace).Count() == 3)
                                            {
                                                currentEdgeInfo = new EdgeInfo { Edges = currentEdges, Curve = edgeCurve, RoofLineType = RoofLineType.Hip, CurrentRoof = currentRoof };
                                                currentEdgeInfo.RelatedPanelFaces = GetEdgeRelatedPanels(currentEdge, targetPlanarFaceList);
                                                return currentEdgeInfo;
                                            }
                                            else
                                            {
                                                XYZ startingPoint = targetCurve.GetEndPoint(0).Z < targetCurve.GetEndPoint(1).Z ? targetCurve.GetEndPoint(0) : targetCurve.GetEndPoint(1);

                                                XYZ extendedPoint = GetExtendedPoint(startingPoint, firstFace);
                                                XYZ rayTracePoint = new XYZ(extendedPoint.X, extendedPoint.Y, extendedPoint.Z + 999);

                                                ReferenceIntersector ReferenceIntersect = new ReferenceIntersector(targetRoof.Id, FindReferenceTarget.Element, (targetRoof.Document.ActiveView as View3D));
                                                ReferenceWithContext RefContext = ReferenceIntersect.FindNearest(rayTracePoint, XYZ.BasisZ.Negate());

                                                if (RefContext == null)
                                                {
                                                    currentEdgeInfo = new EdgeInfo { Edges = currentEdges, Curve = edgeCurve, RoofLineType = RoofLineType.Hip, CurrentRoof = currentRoof };
                                                    currentEdgeInfo.RelatedPanelFaces = GetEdgeRelatedPanels(currentEdge, targetPlanarFaceList);
                                                    return currentEdgeInfo;
                                                }

                                                currentEdgeInfo = new EdgeInfo { Edges = currentEdges, Curve = edgeCurve, RoofLineType = RoofLineType.Valley, CurrentRoof = currentRoof };
                                                currentEdgeInfo.RelatedPanelFaces = GetEdgeRelatedPanels(currentEdge, targetPlanarFaceList);
                                                return currentEdgeInfo;
                                            }
                                        }
                                    }
                                    else
                                    {
                                        if (edgeCurve.GetEndPoint(0).Z.IsAlmostEqualTo(edgeCurve.GetEndPoint(1).Z))
                                        {
                                            if (targetPlanarFaceList.Contains(currentEdge.GetFace(0)) && targetPlanarFaceList.Contains(currentEdge.GetFace(1)))
                                            {
                                                currentEdgeInfo = new EdgeInfo { Edges = currentEdges, Curve = edgeCurve, RoofLineType = RoofLineType.Ridge, CurrentRoof = currentRoof };
                                                currentEdgeInfo.RelatedPanelFaces = GetEdgeRelatedPanels(currentEdge, targetPlanarFaceList);
                                                return currentEdgeInfo;
                                            }
                                            else
                                            {
                                                PlanarFace firstFace = currentEdge.GetFace(0) as PlanarFace;
                                                PlanarFace secondFace = currentEdge.GetFace(1) as PlanarFace;

                                                XYZ startingPoint = edgeCurve.Evaluate(0.5, true);
                                                XYZ crossedDirection = Line.CreateBound(edgeCurve.GetEndPoint(0), edgeCurve.GetEndPoint(1)).Direction.CrossProduct(XYZ.BasisZ);
                                                XYZ crossedPointStart = startingPoint.Add(crossedDirection.Multiply(0.02));
                                                XYZ crossedPointEnd = startingPoint.Add(crossedDirection.Multiply(0.1));
                                                XYZ rayTracePointStart = new XYZ(crossedPointStart.X, crossedPointStart.Y, crossedPointStart.Z + 999);
                                                XYZ rayTracePointEnd = new XYZ(crossedPointEnd.X, crossedPointEnd.Y, crossedPointEnd.Z + 999);

                                                ReferenceIntersector ReferenceIntersect = new ReferenceIntersector(targetRoof.Id, FindReferenceTarget.Element, (targetRoof.Document.ActiveView as View3D));
                                                ReferenceWithContext refStart = ReferenceIntersect.FindNearest(rayTracePointStart, XYZ.BasisZ.Negate());
                                                ReferenceWithContext refEnd = ReferenceIntersect.FindNearest(rayTracePointEnd, XYZ.BasisZ.Negate());

                                                bool isEave = true;
                                                if (refEnd != null)
                                                {
                                                    if (refStart != null)
                                                    {
                                                        //Document doc = currentRoof.Document;
                                                        //double refPointHeight = refEnd.GetReference().GlobalPoint.Z;
                                                        //double starPHeight = refStart.GetReference().GlobalPoint.Z;
                                                        //FamilySymbol fs = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_GenericModel).WhereElementIsElementType().Where(type => type.Name.Contains("DebugPoint2")).FirstOrDefault() as FamilySymbol;
                                                        //doc.Create.NewFamilyInstance(refEnd.GetReference().GlobalPoint, fs, Autodesk.Revit.DB.Structure.StructuralType.NonStructural);
                                                        //doc.Create.NewFamilyInstance(refStart.GetReference().GlobalPoint, fs, Autodesk.Revit.DB.Structure.StructuralType.NonStructural);
                                                        if (refEnd.GetReference().GlobalPoint.Z < refStart.GetReference().GlobalPoint.Z)
                                                            isEave = false;
                                                    }
                                                }
                                                else
                                                {
                                                    crossedPointStart = startingPoint.Add(crossedDirection.Multiply(-0.02));
                                                    crossedPointEnd = startingPoint.Add(crossedDirection.Multiply(-0.1));
                                                    rayTracePointStart = new XYZ(crossedPointStart.X, crossedPointStart.Y, crossedPointStart.Z + 999);
                                                    rayTracePointEnd = new XYZ(crossedPointEnd.X, crossedPointEnd.Y, crossedPointEnd.Z + 999);

                                                    refStart = ReferenceIntersect.FindNearest(rayTracePointStart, XYZ.BasisZ.Negate());
                                                    refEnd = ReferenceIntersect.FindNearest(rayTracePointEnd, XYZ.BasisZ.Negate());

                                                    if (refEnd != null)
                                                    {
                                                        if (refStart != null)
                                                        {
                                                            //Document doc = currentRoof.Document;
                                                            //double refPointHeight = refEnd.GetReference().GlobalPoint.Z;
                                                            //double starPHeight = refStart.GetReference().GlobalPoint.Z;
                                                            //FamilySymbol fs = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_GenericModel).WhereElementIsElementType().Where(type => type.Name.Contains("DebugPoint2")).FirstOrDefault() as FamilySymbol;
                                                            //doc.Create.NewFamilyInstance(refEnd.GetReference().GlobalPoint, fs, Autodesk.Revit.DB.Structure.StructuralType.NonStructural);
                                                            //doc.Create.NewFamilyInstance(refStart.GetReference().GlobalPoint, fs, Autodesk.Revit.DB.Structure.StructuralType.NonStructural);
                                                            if (refEnd.GetReference().GlobalPoint.Z < refStart.GetReference().GlobalPoint.Z)
                                                                isEave = false;
                                                        }
                                                    }
                                                }

                                                if (isEave)
                                                {
                                                    currentEdgeInfo = new EdgeInfo { Edges = currentEdges, Curve = edgeCurve, RoofLineType = RoofLineType.Eave, CurrentRoof = currentRoof };
                                                    currentEdgeInfo.RelatedPanelFaces = GetEdgeRelatedPanels(currentEdge, targetPlanarFaceList);
                                                    return currentEdgeInfo;
                                                }
                                                else
                                                {
                                                    currentEdgeInfo = new EdgeInfo { Edges = currentEdges, Curve = edgeCurve, RoofLineType = RoofLineType.RidgeSinglePanel, CurrentRoof = currentRoof };
                                                    currentEdgeInfo.RelatedPanelFaces = GetEdgeRelatedPanels(currentEdge, targetPlanarFaceList);
                                                    return currentEdgeInfo;
                                                }
                                            }
                                        }
                                        else
                                        {
                                            if (targetPlanarFaceList.Contains(currentEdge.GetFace(0)) && targetPlanarFaceList.Contains(currentEdge.GetFace(1)))
                                            {
                                                currentEdgeInfo = new EdgeInfo { Edges = currentEdges, Curve = edgeCurve, RoofLineType = RoofLineType.Hip, CurrentRoof = currentRoof };
                                                currentEdgeInfo.RelatedPanelFaces = GetEdgeRelatedPanels(currentEdge, targetPlanarFaceList);
                                                return currentEdgeInfo;
                                            }
                                            else
                                            {
                                                currentEdgeInfo = new EdgeInfo { Edges = currentEdges, Curve = edgeCurve, RoofLineType = RoofLineType.Gable, CurrentRoof = currentRoof };
                                                currentEdgeInfo.RelatedPanelFaces = GetEdgeRelatedPanels(currentEdge, targetPlanarFaceList);
                                                return currentEdgeInfo;
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            return new EdgeInfo { Edges = new List<Edge>(), Curve = targetCurve, RoofLineType = RoofLineType.Undefined, CurrentRoof = null };
        }

        static internal IList<Edge> GetRidgeInfoList(Edge targetRidgeEdge, IList<EdgeInfo> targetEdgeInfoList)
        {
            IList<Edge> resultingRidgeInfo = new List<Edge>();
            foreach (EdgeInfo currentRidgeInfo in targetEdgeInfoList)
            {
                if (currentRidgeInfo.RoofLineType == RoofLineType.Eave)
                {
                    if (currentRidgeInfo.Edges[0].GetFace(0) == targetRidgeEdge.GetFace(0) || currentRidgeInfo.Edges[0].GetFace(0) == targetRidgeEdge.GetFace(1) ||
                        currentRidgeInfo.Edges[0].GetFace(1) == targetRidgeEdge.GetFace(0) || currentRidgeInfo.Edges[0].GetFace(1) == targetRidgeEdge.GetFace(1))
                    {
                        resultingRidgeInfo.Add(currentRidgeInfo.Edges[0]);
                    }
                }
            }
            return resultingRidgeInfo;
        }

        static internal IList<Curve> GetNonDuplicatedCurvesFromListOfFaces(IList<PlanarFace> targetPlanarFaceList)
        {
            IList<Curve> resultingNonDuplicatedCurves = new List<Curve>();
            foreach (PlanarFace currentPlanarFace in targetPlanarFaceList)
            {
                foreach (Curve currentCurve in GetOuterCurveLoop(currentPlanarFace))
                {
                    if (resultingNonDuplicatedCurves.ContainsSimilarCurve(currentCurve))
                        continue;

                    resultingNonDuplicatedCurves.Add(currentCurve);
                }
            }
            return resultingNonDuplicatedCurves;
        }

        static internal IList<EdgeInfo> GetRoofEdgeInfoList(FootPrintRoof currentRoof, bool topFaces = true)
        {
            //TODO see if theres any segmented curves (segmented sketch lines) on the footprint of the roof
            //If yes, ask the user to correct then OR exit the command OR merge the sketchlines

            IList<EdgeInfo> resultingEdgeInfoList = new List<EdgeInfo>();
            IList<EdgeInfo> tempEdgeInfoList = new List<EdgeInfo>();

            IList<Reference> currentListOfReferences = new List<Reference>();
            if (topFaces)
                currentListOfReferences = HostObjectUtils.GetTopFaces(currentRoof);
            else
                currentListOfReferences = HostObjectUtils.GetBottomFaces(currentRoof);

            IList<PlanarFace> currentListOfFaces = new List<PlanarFace>();
            IsListOfPlanarFaces(currentListOfReferences, currentRoof, out currentListOfFaces);

            IList<Curve> nonDuplicatedEdgeList = GetNonDuplicatedCurvesFromListOfFaces(currentListOfFaces);

            foreach (Curve currentCurve in nonDuplicatedEdgeList)
            {
                tempEdgeInfoList.Add(GetCurveInformation(currentRoof, currentCurve, currentListOfFaces));
            }

            foreach (EdgeInfo currentEdgeInfo in tempEdgeInfoList)
            {
                EdgeInfo newEdgeInfo = currentEdgeInfo;
                newEdgeInfo.RelatedRidgeEaves = GetRidgeInfoList(newEdgeInfo.Edges[0], tempEdgeInfoList);
                resultingEdgeInfoList.Add(newEdgeInfo);
            }

            resultingEdgeInfoList = MergeEdgeCurves(resultingEdgeInfoList);

            //foreach (EdgeInfo currentEdgeInfo in resultingEdgeInfoList)
            //{
            //    if (currentEdgeInfo.Edges.Count > 1)
            //        System.Diagnostics.Debug.WriteLine("Merged Edge");

            //    Document doc = currentEdgeInfo.CurrentRoof.Document;
            //    PlanarFace pfce = currentEdgeInfo.GetRelatedPanels()[0] as PlanarFace;
            //    Plane pl = new Plane(pfce.FaceNormal, pfce.Origin);
            //    SketchPlane skp = SketchPlane.Create(doc, pl);
            //    doc.Create.NewModelCurve(currentEdgeInfo.Curve, skp);
            //}


            return resultingEdgeInfoList;
        }

        static internal IList<Face> GetEdgeRelatedPanels(Edge targetEdge, IList<PlanarFace> targetRoofPlanarFaces)
        {
            IList<Face> resultingListOfFaces = new List<Face>();
            Face targetFace0 = targetEdge.GetFace(0);
            Face targetFace1 = targetEdge.GetFace(1);

            if (targetRoofPlanarFaces.Contains(targetFace0))
                resultingListOfFaces.Add(targetFace0);
            if (targetRoofPlanarFaces.Contains(targetFace1))
                resultingListOfFaces.Add(targetFace1);

            return resultingListOfFaces;
        }

        static internal IList<Face> GetEdgeRelatedPanels(Edge targetEdge, FootPrintRoof targetRoof)
        {
            IList<Face> resultingListOfFaces = new List<Face>();
            Face targetFace0 = targetEdge.GetFace(0);
            Face targetFace1 = targetEdge.GetFace(1);

            IList<Reference> currentListOfReferences = HostObjectUtils.GetTopFaces(targetRoof).Union(HostObjectUtils.GetBottomFaces(targetRoof)).ToList();

            IList<PlanarFace> targetRoofPlanarFaces = new List<PlanarFace>();
            IsListOfPlanarFaces(currentListOfReferences, targetRoof, out targetRoofPlanarFaces);

            if (targetRoofPlanarFaces.Contains(targetFace0))
                resultingListOfFaces.Add(targetFace0);
            if (targetRoofPlanarFaces.Contains(targetFace1))
                resultingListOfFaces.Add(targetFace1);

            return resultingListOfFaces;
        }

        static private IList<EdgeInfo> MergeEdgeCurves(IList<EdgeInfo> targetListOfEdgeInfo)
        {
            IList<EdgeInfo> resultingListOfEdgeInfo = targetListOfEdgeInfo.ToList();
            foreach (EdgeInfo currentEdgeInfo in targetListOfEdgeInfo)
            {
                foreach (EdgeInfo currentOtherEdgeInfo in targetListOfEdgeInfo)
                {
                    if ((currentEdgeInfo.RoofLineType == currentOtherEdgeInfo.RoofLineType) &&
                        !(currentEdgeInfo.Curve.IsAlmostEqualTo(currentOtherEdgeInfo.Curve)))
                    {
                        if (currentEdgeInfo.Curve is Line && currentOtherEdgeInfo.Curve is Line)
                        {

                            Line l1 = currentEdgeInfo.Curve as Line;
                            Line l2 = currentOtherEdgeInfo.Curve as Line;

                            XYZ d1 = l1.Direction;
                            XYZ d2 = l2.Direction;

                            if (d1.AngleTo(d2).IsAlmostEqualTo(0, 0.02) || d1.AngleTo(d2).IsAlmostEqualTo(Math.PI, 0.02) || d1.AngleTo(d2).IsAlmostEqualTo(2 * Math.PI, 0.02))
                            {
                                IntersectionResultArray edgeIntResult = null;
                                SetComparisonResult edgeCompResult = currentEdgeInfo.Curve.Intersect(currentOtherEdgeInfo.Curve, out edgeIntResult);

                                if (edgeIntResult != null)
                                {
                                    XYZ currentIntersPoint = edgeIntResult.get_Item(0).XYZPoint;
                                    double maxDist = double.MinValue;
                                    XYZ startingPoint = null;
                                    XYZ endingPoint = null;

                                    for (int i = 0; i <= 1; i++)
                                    {
                                        XYZ currentPoint = currentEdgeInfo.Curve.GetEndPoint(i);
                                        for (int j = 0; j <= 1; j++)
                                        {
                                            XYZ currentOtherPoint = currentOtherEdgeInfo.Curve.GetEndPoint(j);
                                            double currentDist = currentPoint.DistanceTo(currentOtherPoint);

                                            if (currentDist > maxDist)
                                            {
                                                maxDist = currentDist;
                                                startingPoint = currentPoint;
                                                endingPoint = currentOtherPoint;
                                            }
                                        }
                                    }

                                    EdgeInfo currentEdgeInfoNew = new EdgeInfo { CurrentRoof = currentEdgeInfo.CurrentRoof, RoofLineType = currentEdgeInfo.RoofLineType };

                                    Line mergedLine = Line.CreateBound(startingPoint, endingPoint);
                                    currentEdgeInfoNew.Curve = mergedLine;

                                    resultingListOfEdgeInfo.Remove(currentEdgeInfo);
                                    resultingListOfEdgeInfo.Remove(currentOtherEdgeInfo);

                                    bool alreadyExists = false;
                                    foreach (EdgeInfo currentResultInfo in resultingListOfEdgeInfo)
                                    {
                                        if (currentResultInfo.Curve.IsAlmostEqualTo(mergedLine))
                                            alreadyExists = true;
                                    }


                                    if (!alreadyExists)
                                    {
                                        IList<Edge> currentEdges = new List<Edge> { currentEdgeInfo.Edges[0], currentOtherEdgeInfo.Edges[0] };
                                        currentEdgeInfoNew.Edges = currentEdges;
                                        resultingListOfEdgeInfo.Add(currentEdgeInfoNew);
                                    }

                                    //Document doc = currentEdgeInfo.CurrentRoof.Document;
                                    //FamilySymbol fs = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_GenericModel).WhereElementIsElementType().Where(type => type.Name.Contains("DebugPoint2")).FirstOrDefault() as FamilySymbol;
                                    //doc.Create.NewFamilyInstance(currentIntersPoint, fs, Autodesk.Revit.DB.Structure.StructuralType.NonStructural);
                                }
                            }
                        }
                    }
                }
            }
            return resultingListOfEdgeInfo;
        }

        static internal Curve GetMostSimilarCurve(Curve targetCurve, IList<EdgeInfo> edgeInfoList)
        {
            Curve edgeInfoToReturn = null;
            if (edgeInfoList == null)
                return edgeInfoToReturn;

            Line edgeLine = targetCurve as Line;

            if (edgeLine == null)
                return edgeInfoToReturn;

            double minDistance = double.MaxValue;
            foreach (EdgeInfo currentEdgeInfo in edgeInfoList)
            {
                Line currentEdgeInfoLine = currentEdgeInfo.Curve as Line;

                if (currentEdgeInfoLine == null)
                    continue;

                if (!(edgeLine.ApproximateLength.IsAlmostEqualTo(currentEdgeInfoLine.ApproximateLength)))
                    continue;

                if (!Math.Abs(edgeLine.Direction.DotProduct(currentEdgeInfoLine.Direction)).IsAlmostEqualTo(1))
                    continue;

                double currentDistance = edgeLine.Evaluate(0.5,true).DistanceTo(currentEdgeInfoLine.Evaluate(0.5, true));

                if (currentDistance < minDistance)
                {
                    minDistance = currentDistance;
                    edgeInfoToReturn = currentEdgeInfo.Curve;
                }
            }

            return edgeInfoToReturn;
        }

        static internal EdgeInfo GetMostSimilarEdgeInfo(Reference currentReference, Document targetDoc)
        {
            FootPrintRoof currentFootPrintRoof = targetDoc.GetElement(currentReference) as FootPrintRoof;
            Edge edge = Support.GetEdgeFromReference(currentReference, currentFootPrintRoof);

            IList<PlanarFace> pfaces = new List<PlanarFace>();
            Support.IsListOfPlanarFaces(HostObjectUtils.GetBottomFaces(currentFootPrintRoof)
                , currentFootPrintRoof, out pfaces);

            IList<RoofClasses.EdgeInfo> currentEdgeInfoList = GetRoofEdgeInfoList(currentFootPrintRoof, false);

            Curve currentCurve = GetMostSimilarCurve(edge.AsCurve(), currentEdgeInfoList);
            RoofClasses.EdgeInfo currentRidgeInfo = GetCurveInformation(currentFootPrintRoof, currentCurve, pfaces);

            return currentRidgeInfo;
        }
    }

    class GeometrySupport
    {
        static internal XYZ GetRoofIntersectionFlattenLines(Line RidgeLine, XYZ ridgePointFlatten, Line eaveOrValleyLine, double flattenHeight)
        {
            Line eaveOrValleyInfiniteLine = (eaveOrValleyLine as Line).Flatten(flattenHeight);
            eaveOrValleyInfiniteLine.MakeUnbound();
            XYZ crossedRidgeDirection = ridgePointFlatten.Add(RidgeLine.Flatten(flattenHeight).Direction.CrossProduct(XYZ.BasisZ));
            Line crossedRidgeInfitineLine = Line.CreateBound(ridgePointFlatten, crossedRidgeDirection.Multiply(1));
            crossedRidgeInfitineLine = crossedRidgeInfitineLine.Flatten(flattenHeight);
            crossedRidgeInfitineLine.MakeUnbound();

            IntersectionResultArray lineInterserctions = null;
            eaveOrValleyInfiniteLine.Intersect(crossedRidgeInfitineLine, out lineInterserctions);

            if (lineInterserctions == null || lineInterserctions.Size > 1 || lineInterserctions.Size == 0)
                return null;

            return lineInterserctions.get_Item(0).XYZPoint;
        }

        static internal TrussInfo GetTrussInfo(XYZ currentTopPoint, XYZ supportPoint0, XYZ supportPoint1)
        {
            if (currentTopPoint.DistanceTo(supportPoint0) < 0.5 || currentTopPoint.DistanceTo(supportPoint1) < 0.5 || supportPoint0.DistanceTo(supportPoint1) < 0.5)
                return null;

            CurveArray topChords = new CurveArray();
            CurveArray bottomChords = new CurveArray();

            topChords.Append(Line.CreateBound(currentTopPoint, supportPoint0));
            topChords.Append(Line.CreateBound(currentTopPoint, supportPoint1));
            bottomChords.Append(Line.CreateBound(supportPoint0, supportPoint1));
            double height = currentTopPoint.DistanceTo(new XYZ(currentTopPoint.X, currentTopPoint.Y, supportPoint0.Z));

            TrussInfo trussInfo = new TrussInfo(supportPoint0, supportPoint1, height);
            trussInfo.TopChords = topChords;
            trussInfo.BottomChords = bottomChords;

            return trussInfo;
        }

        static internal XYZ AdjustTopPointToRoofAngle(XYZ targetTopPoint, IList<XYZ> supportPoints, EdgeInfo currentRidgeInfo)
        {

            if (currentRidgeInfo.RelatedPanelFaces == null || currentRidgeInfo.RelatedPanelFaces.Count < 1)
                currentRidgeInfo.RelatedPanelFaces = currentRidgeInfo.GetRelatedPanels();

            if (currentRidgeInfo.RelatedPanelFaces == null || currentRidgeInfo.RelatedPanelFaces.Count < 1)
                return targetTopPoint;

            PlanarFace relatedFace = currentRidgeInfo.RelatedPanelFaces[0] as PlanarFace;

            if (relatedFace == null)
                return targetTopPoint;

            Line ridgeLine = currentRidgeInfo.Curve as Line;

            if (ridgeLine == null)
                return targetTopPoint;

            XYZ ridgeDirection = ridgeLine.Direction;
            XYZ ridgeDirectionCrossed = ridgeDirection.CrossProduct(XYZ.BasisZ);

            XYZ projectedPoint = targetTopPoint.Add(ridgeDirectionCrossed.Multiply(0.1));
            IntersectionResult iResult = relatedFace.Project(projectedPoint);

            if (iResult == null)
            {
                projectedPoint = targetTopPoint.Add(ridgeDirectionCrossed.Negate().Multiply(0.1));
                iResult = relatedFace.Project(projectedPoint);

                if (iResult == null)
                    return targetTopPoint;
            }

            //Just to make sure that the targetTopPoint is located on Ridge
            IntersectionResult ridgeProjected = ridgeLine.Project(targetTopPoint);
            if (ridgeProjected != null)
                targetTopPoint = ridgeProjected.XYZPoint;

            XYZ roofSlopeLineDirection = Line.CreateBound(iResult.XYZPoint, targetTopPoint).Direction;

            if (supportPoints == null || supportPoints.Count < 1)
                return targetTopPoint;

            //This code assumes that there are 1 or 2 supportPoints
            XYZ supportPoint = supportPoints[0];

            if (supportPoints.Count == 2)
            {
                XYZ supportPoints1 = supportPoints[1];
                if (supportPoints1.DistanceTo(projectedPoint) < supportPoint.DistanceTo(projectedPoint))
                    supportPoint = supportPoints1;
            }

            Line roofSlopeLine = Line.CreateUnbound(supportPoint, roofSlopeLineDirection);
            Line roofRidgeLine = Line.CreateUnbound(targetTopPoint, XYZ.BasisZ);

            IntersectionResultArray iResultArrCurves = null;
            roofSlopeLine.Intersect(roofRidgeLine, out iResultArrCurves);

            if (iResultArrCurves != null && iResultArrCurves.Size == 1)
            {
                return iResultArrCurves.get_Item(0).XYZPoint;
            }



            return targetTopPoint;
        }

    }

}
