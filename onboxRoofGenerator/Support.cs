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

        static internal CurveLoop GetOuterCurveLoop(PlanarFace targetFace)
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

        static internal XYZ GetExtendedPoint(XYZ startPosition, PlanarFace targetPlanarFace, double amount = 0.1)
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
                                Curve edgeCurve = currentEdge.AsCurve();
                                if (edgeCurve.IsAlmostEqualTo(targetCurve))
                                {
                                    if (edgeCurve.GetEndPoint(0).Z.IsAlmostEqualTo(currentPlanarFace.Origin.Z) && edgeCurve.GetEndPoint(1).Z.IsAlmostEqualTo(currentPlanarFace.Origin.Z))
                                    {
                                        return new EdgeInfo { edge = currentEdge, curve = edgeCurve, roofLineType = RoofLineType.Eave };

                                    }
                                    else if (edgeCurve.GetEndPoint(0).Z.IsAlmostEqualTo(currentPlanarFace.Origin.Z) || edgeCurve.GetEndPoint(1).Z.IsAlmostEqualTo(currentPlanarFace.Origin.Z))
                                    {
                                        PlanarFace firstFace = currentEdge.GetFace(0) as PlanarFace;
                                        PlanarFace secondFace = currentEdge.GetFace(1) as PlanarFace;

                                        if (!targetPlanarFaceList.Contains(firstFace) || !targetPlanarFaceList.Contains(secondFace))
                                        {
                                            return new EdgeInfo { edge = currentEdge, curve = edgeCurve, roofLineType = RoofLineType.Gable };
                                        }
                                        else
                                        {
                                            if (GetOuterCurveLoop(firstFace).Count() == 3 || GetOuterCurveLoop(secondFace).Count() == 3)
                                            {
                                                return new EdgeInfo { edge = currentEdge, curve = edgeCurve, roofLineType = RoofLineType.Hip };
                                            }
                                            else
                                            {
                                                XYZ startingPoint = edgeCurve.GetEndPoint(0).Z < edgeCurve.GetEndPoint(1).Z ? edgeCurve.GetEndPoint(0) : edgeCurve.GetEndPoint(1);

                                                XYZ extendedPoint = GetExtendedPoint(startingPoint, firstFace);
                                                XYZ rayTracePoint = new XYZ(extendedPoint.X, extendedPoint.Y, extendedPoint.Z + 999);

                                                ReferenceIntersector ReferenceIntersect = new ReferenceIntersector(targetRoof.Id, FindReferenceTarget.Element, (targetRoof.Document.ActiveView as View3D));
                                                ReferenceWithContext RefContext = ReferenceIntersect.FindNearest(rayTracePoint, XYZ.BasisZ.Negate());

                                                if (RefContext == null)
                                                    return new EdgeInfo { edge = currentEdge, curve = edgeCurve, roofLineType = RoofLineType.Hip };

                                                return new EdgeInfo { edge = currentEdge, curve = edgeCurve, roofLineType = RoofLineType.Valley };
                                            }
                                        }
                                    }
                                    else
                                    {
                                        if (edgeCurve.GetEndPoint(0).Z.IsAlmostEqualTo(edgeCurve.GetEndPoint(1).Z))
                                            return new EdgeInfo { edge = currentEdge, curve = edgeCurve, roofLineType = RoofLineType.Ridge };
                                        else
                                        {
                                            return new EdgeInfo { edge = currentEdge, curve = edgeCurve, roofLineType = RoofLineType.Hip };
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

        static internal EdgeInfo GetEdgeInformation(Document doc, Edge selectedEdge, Element currentElement)
        {
            if (selectedEdge == null)
                return new EdgeInfo { curve = null, edge = null, roofLineType = RoofLineType.Undefined };

            Curve edgeCurve = selectedEdge.AsCurve();
            PlanarFace currentPlanarFace = selectedEdge.GetFace(0) as PlanarFace;

            if (currentPlanarFace == null)
                return new EdgeInfo { curve = null, edge = null, roofLineType = RoofLineType.Undefined };

            FootPrintRoof currentRoof = currentElement as FootPrintRoof;

            if (currentRoof == null)
                return new EdgeInfo { curve = null, edge = null, roofLineType = RoofLineType.Undefined };

            IList<Reference> listOfReferences = HostObjectUtils.GetTopFaces(currentRoof);
            listOfReferences = listOfReferences.Union(HostObjectUtils.GetBottomFaces(currentRoof)).ToList();
            IList<PlanarFace> listOfPlanarFaces = new List<PlanarFace>();

            if (!IsListOfPlanarFaces(listOfReferences, currentRoof, out listOfPlanarFaces))
                return new EdgeInfo { curve = null, edge = null, roofLineType = RoofLineType.Undefined };

            double firstPointHeight = edgeCurve.GetEndPoint(0).Z;
            double secondPointHeight = edgeCurve.GetEndPoint(1).Z;
            double planarFaceOrigin = currentPlanarFace.Origin.Z;

            if (firstPointHeight.IsAlmostEqualTo(planarFaceOrigin) && secondPointHeight.IsAlmostEqualTo(planarFaceOrigin)) 
                return new EdgeInfo { edge = selectedEdge, curve = edgeCurve, roofLineType = RoofLineType.Eave };

            if (firstPointHeight.IsAlmostEqualTo(planarFaceOrigin) || secondPointHeight.IsAlmostEqualTo(planarFaceOrigin))
            {
                PlanarFace firstFace = selectedEdge.GetFace(0) as PlanarFace;
                PlanarFace secondFace = selectedEdge.GetFace(1) as PlanarFace;

                if (!listOfPlanarFaces.Contains(firstFace) || !listOfPlanarFaces.Contains(secondFace))
                {
                    return new EdgeInfo { edge = selectedEdge, curve = edgeCurve, roofLineType = RoofLineType.Gable };
                }
                else
                {
                    if (GetOuterCurveLoop(firstFace).Count() == 3 || GetOuterCurveLoop(secondFace).Count() == 3)
                    {
                        return new EdgeInfo { edge = selectedEdge, curve = edgeCurve, roofLineType = RoofLineType.Hip };
                    }
                    else
                    {
                        XYZ startingPoint = edgeCurve.GetEndPoint(0).Z < edgeCurve.GetEndPoint(1).Z ? edgeCurve.GetEndPoint(0) : edgeCurve.GetEndPoint(1);

                        XYZ extendedPoint = GetExtendedPoint(startingPoint, firstFace);
                        XYZ rayTracePoint = new XYZ(extendedPoint.X, extendedPoint.Y, extendedPoint.Z + 999);

                        ReferenceIntersector ReferenceIntersect = new ReferenceIntersector(currentRoof.Id, FindReferenceTarget.Element, (doc.ActiveView as View3D));
                        ReferenceWithContext RefContext = ReferenceIntersect.FindNearest(rayTracePoint, XYZ.BasisZ.Negate());

                        if (RefContext == null)
                            return new EdgeInfo { edge = selectedEdge, curve = edgeCurve, roofLineType = RoofLineType.Hip };

                        return new EdgeInfo { edge = selectedEdge, curve = edgeCurve, roofLineType = RoofLineType.Valley };
                    }

                }
            }
            else
            {
                if (edgeCurve.GetEndPoint(0).Z.IsAlmostEqualTo(edgeCurve.GetEndPoint(1).Z))
                    return new EdgeInfo { edge = selectedEdge, curve = edgeCurve, roofLineType = RoofLineType.Ridge };
                else
                {
                    return new EdgeInfo { edge = selectedEdge, curve = edgeCurve, roofLineType = RoofLineType.Hip };
                }
            }
        }

    }
}
