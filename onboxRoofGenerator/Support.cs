﻿using Autodesk.Revit.DB;
using Autodesk.Revit.DB.IFC;
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

        static private Edge GetEdgeFromReference(Reference targetReference, Element targetElement)
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
                                                XYZ startingPoint = edgeCurve.GetEndPoint(0).Z < edgeCurve.GetEndPoint(1).Z ? edgeCurve.GetEndPoint(0) : edgeCurve.GetEndPoint(1);

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
                                                XYZ crossedPoint = startingPoint.Add(crossedDirection.Multiply(0.1));
                                                XYZ rayTracePoint = new XYZ(crossedPoint.X, crossedPoint.Y, crossedPoint.Z + 999);

                                                ReferenceIntersector ReferenceIntersect = new ReferenceIntersector(targetRoof.Id, FindReferenceTarget.Element, (targetRoof.Document.ActiveView as View3D));
                                                ReferenceWithContext RefContext = ReferenceIntersect.FindNearest(rayTracePoint, XYZ.BasisZ.Negate());

                                                bool isEave = true;
                                                if (RefContext != null)
                                                {
                                                    if (RefContext.GetReference().GlobalPoint.Z < startingPoint.Z)
                                                        isEave = false;
                                                }
                                                else
                                                {
                                                    crossedDirection = Line.CreateBound(edgeCurve.GetEndPoint(0), edgeCurve.GetEndPoint(1)).Direction.Negate().CrossProduct(XYZ.BasisZ);
                                                    crossedPoint = startingPoint.Add(crossedDirection.Multiply(0.1));
                                                    rayTracePoint = new XYZ(crossedPoint.X, crossedPoint.Y, crossedPoint.Z + 999);

                                                    ReferenceIntersect = new ReferenceIntersector(targetRoof.Id, FindReferenceTarget.Element, (targetRoof.Document.ActiveView as View3D));
                                                    RefContext = ReferenceIntersect.FindNearest(rayTracePoint, XYZ.BasisZ.Negate());

                                                    if (RefContext != null)
                                                    {
                                                        if (RefContext.GetReference().GlobalPoint.Z < startingPoint.Z)
                                                            isEave = false;
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

        static private IList<Curve> GetNonDuplicatedCurvesFromListOfFaces(IList<PlanarFace> targetPlanarFaceList)
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

            IList<Reference> currentListOfReferences = currentListOfReferences = HostObjectUtils.GetTopFaces(targetRoof);

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
    }
}