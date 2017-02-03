using Autodesk.Revit.DB;
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

        static private bool IsListOfPlanarFaces(IList<Reference> targetListOfReferences, Element currentElement, out IList<PlanarFace> targetListOfPlanarFaces)
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

        static private Face GetFaceFromReference(Reference targetReference, Element targetElement)
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
            if (targetPlanarFaceList != null)
            {
                foreach (PlanarFace currentPlanarFace in targetPlanarFaceList)
                {
                    EdgeArrayArray EdgeLoops = currentPlanarFace.EdgeLoops;
                    foreach (EdgeArray currentEdgeArray in EdgeLoops)
                    {
                        foreach (Edge currentEdge in currentEdgeArray)
                        {
                            if (currentEdge != null)
                            {
                                EdgeInfo currentEdgeInfo = new EdgeInfo();
                                Curve edgeCurve = currentEdge.AsCurve();
                                if (edgeCurve.IsAlmostEqualTo(targetCurve))
                                {
                                    if (edgeCurve.GetEndPoint(0).Z.IsAlmostEqualTo(currentPlanarFace.Origin.Z) && edgeCurve.GetEndPoint(1).Z.IsAlmostEqualTo(currentPlanarFace.Origin.Z))
                                    {
                                        currentEdgeInfo = new EdgeInfo { edge = currentEdge, curve = edgeCurve, roofLineType = RoofLineType.Eave };
                                        currentEdgeInfo.relatedPanelFaces = GetEdgeRelatedPanels(currentEdge, targetPlanarFaceList);
                                        return currentEdgeInfo;
                                    }
                                    else if (edgeCurve.GetEndPoint(0).Z.IsAlmostEqualTo(currentPlanarFace.Origin.Z) || edgeCurve.GetEndPoint(1).Z.IsAlmostEqualTo(currentPlanarFace.Origin.Z))
                                    {
                                        PlanarFace firstFace = currentEdge.GetFace(0) as PlanarFace;
                                        PlanarFace secondFace = currentEdge.GetFace(1) as PlanarFace;

                                        if (!targetPlanarFaceList.Contains(firstFace) || !targetPlanarFaceList.Contains(secondFace))
                                        {
                                            currentEdgeInfo = new EdgeInfo { edge = currentEdge, curve = edgeCurve, roofLineType = RoofLineType.Gable };
                                            currentEdgeInfo.relatedPanelFaces = GetEdgeRelatedPanels(currentEdge, targetPlanarFaceList);
                                            return currentEdgeInfo;
                                        }
                                        else
                                        {
                                            if (GetOuterCurveLoop(firstFace).Count() == 3 || GetOuterCurveLoop(secondFace).Count() == 3)
                                            {
                                                currentEdgeInfo = new EdgeInfo { edge = currentEdge, curve = edgeCurve, roofLineType = RoofLineType.Hip };
                                                currentEdgeInfo.relatedPanelFaces = GetEdgeRelatedPanels(currentEdge, targetPlanarFaceList);
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
                                                    currentEdgeInfo = new EdgeInfo { edge = currentEdge, curve = edgeCurve, roofLineType = RoofLineType.Hip };
                                                    currentEdgeInfo.relatedPanelFaces = GetEdgeRelatedPanels(currentEdge, targetPlanarFaceList);
                                                    return currentEdgeInfo;
                                                }
                                                   
                                                currentEdgeInfo = new EdgeInfo { edge = currentEdge, curve = edgeCurve, roofLineType = RoofLineType.Valley };
                                                currentEdgeInfo.relatedPanelFaces = GetEdgeRelatedPanels(currentEdge, targetPlanarFaceList);
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
                                                currentEdgeInfo = new EdgeInfo { edge = currentEdge, curve = edgeCurve, roofLineType = RoofLineType.Ridge };
                                                currentEdgeInfo.relatedPanelFaces = GetEdgeRelatedPanels(currentEdge, targetPlanarFaceList);
                                                return currentEdgeInfo;
                                            }
                                               
                                            currentEdgeInfo = new EdgeInfo { edge = currentEdge, curve = edgeCurve, roofLineType = RoofLineType.RidgeSinglePanel };
                                            currentEdgeInfo.relatedPanelFaces = GetEdgeRelatedPanels(currentEdge, targetPlanarFaceList);
                                            return currentEdgeInfo;
                                        }
                                        else
                                        {
                                            currentEdgeInfo = new EdgeInfo { edge = currentEdge, curve = edgeCurve, roofLineType = RoofLineType.Hip };
                                            currentEdgeInfo.relatedPanelFaces = GetEdgeRelatedPanels(currentEdge, targetPlanarFaceList);
                                            return currentEdgeInfo;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            return new EdgeInfo { edge = null, curve = targetCurve, roofLineType = RoofLineType.Undefined };
        }

        static internal IList<Edge> GetRidgeInfoList(Edge targetRidgeEdge, IList<EdgeInfo> targetEdgeInfoList)
        {
            IList<Edge> resultingRidgeInfo = new List<Edge>();
            foreach (EdgeInfo currentRidgeInfo in targetEdgeInfoList)
            {
                if (currentRidgeInfo.roofLineType == RoofLineType.Eave)
                {
                    if (currentRidgeInfo.edge.GetFace(0) == targetRidgeEdge.GetFace(0) || currentRidgeInfo.edge.GetFace(0) == targetRidgeEdge.GetFace(1) ||
                        currentRidgeInfo.edge.GetFace(1) == targetRidgeEdge.GetFace(0) || currentRidgeInfo.edge.GetFace(1) == targetRidgeEdge.GetFace(1))
                    {
                        resultingRidgeInfo.Add(currentRidgeInfo.edge);
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
                newEdgeInfo.relatedRidgeEaves = GetRidgeInfoList(newEdgeInfo.edge, tempEdgeInfoList);
                resultingEdgeInfoList.Add(newEdgeInfo);
            }

            return resultingEdgeInfoList;
        }

        static private IList<Face> GetEdgeRelatedPanels(Edge targetEdge, IList<PlanarFace> targetRoofPlanarFaces)
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

        /// <summary>
        /// This method assumes that the Truss will be symmetric (the roof has the same slopes in all panels)
        /// </summary>
        /// <returns></returns>
        //static private XYZ TrussSupportLocation(EdgeInfo targetRidgeInfo, double distanceAlongRidge)
        //{
        //    if (targetRidgeInfo.roofLineType != RoofLineType.Ridge && targetRidgeInfo.roofLineType != RoofLineType.RidgeSinglePanel)
        //        throw new Exception("EdgeInfo is not a Ridge");

        //    if (targetRidgeInfo.edge.GetFace(0))

        //}

    }
}
