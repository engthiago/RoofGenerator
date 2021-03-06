﻿using Autodesk.Revit.Attributes;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;

namespace onboxRoofGenerator
{
    /// <summary>
    /// Command to create trusses for all Ridges on the selected Roof
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    class TrussRidgeDistribution : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            Document doc = uidoc.Document;

            if ((uidoc.ActiveView as View3D) == null)
            {
                message = "Por favor, rode este comando em uma vista 3d";
                return Result.Failed;
            }

            try
            {
                Element tTypeElement = new FilteredElementCollector(doc).OfClass(typeof(FamilySymbol)).Where(fsy => fsy is TrussType).ToList().FirstOrDefault();
                TrussType tType = tTypeElement as TrussType;

                if (tTypeElement == null)
                {
                    message = "Nenhum tipo de treliça foi encontrada no projeto, por favor, carregue um tipo e rode este comando novamente";
                    return Result.Failed;
                }

                Reference currentReference = uidoc.Selection.PickObject(ObjectType.Element, new RoofClasses.SelectionFilters.StraightLinesAndFacesFootPrintRoofSelFilter(), "Selecione um telhado.");
                FootPrintRoof currentFootPrintRoof = doc.GetElement(currentReference) as FootPrintRoof;

                Managers.TrussRidgeManager currentTrussManager = new Managers.TrussRidgeManager();
                IList<RoofClasses.TrussInfo> currentTrussInfoList = currentTrussManager.CreateTrussesFromRoof(currentFootPrintRoof, tType);

                TaskDialog tDialog = new TaskDialog("Trusses");
                tDialog.MainInstruction = currentTrussInfoList.Count.ToString();
                tDialog.Show();
            }
            catch (Exception e)
            {
                if (!(e is Autodesk.Revit.Exceptions.OperationCanceledException))
                {
                    throw e;
                }
            }

            return Result.Succeeded;
        }
    }

    [Transaction(TransactionMode.Manual)]
    class TrussRidgeSelectingSupport : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            Document doc = uidoc.Document;
            Selection sel = uidoc.Selection;

            try
            {
                Element tTypeElement = new FilteredElementCollector(doc).OfClass(typeof(FamilySymbol)).Where(fsy => fsy is TrussType).ToList().FirstOrDefault();
                if (tTypeElement == null)
                {
                    message = "Nenhum tipo de treliça foi encontrada no projeto, por favor, carregue um tipo e rode este comando novamente";
                    return Result.Failed;
                }
                TrussType tType = tTypeElement as TrussType;

                ISelectionFilter ridgeSelectionFilter = new RoofClasses.SelectionFilters.StraightLinesAndFacesRidgeSelectionFilter(doc);
                Reference currentReference = sel.PickObject(ObjectType.Edge, ridgeSelectionFilter);

                FootPrintRoof currentFootPrintRoof = doc.GetElement(currentReference) as FootPrintRoof;
                RoofClasses.EdgeInfo currentRidgeInfo = Support.GetMostSimilarEdgeInfo(currentReference, doc);

                if (currentRidgeInfo == null)
                {
                    message = "Nenhuma linha inferior pode ser obtida a partir da seleção";
                    return Result.Failed;
                }

                //                #region DEBUG ONLY
                //#if DEBUG
                //                using (Transaction ta = new Transaction(doc, "Line test"))
                //                {
                //                    ta.Start();

                //                    Frame fr = new Frame(currentRidgeInfo.Curve.Evaluate(0.5, true), XYZ.BasisX, XYZ.BasisY, XYZ.BasisZ);
                //                    SketchPlane skp = SketchPlane.Create(doc, Plane.Create(fr));
                //                    doc.Create.NewModelCurve(currentRidgeInfo.Curve, skp);


                //                    ta.Commit();
                //                }
                //#endif 
                //                #endregion

                Line currentRidgeLine = currentRidgeInfo.Curve as Line;

                if (currentRidgeLine == null)
                {
                    message = "Ridge must be a straight line";
                    return Result.Failed;
                }

                ISelectionFilter currentTrussBaseSupportFilter = new RoofClasses.SelectionFilters.SupportsSelectionFilter(currentRidgeLine.Direction.CrossProduct(XYZ.BasisZ));
                XYZ baseSupportPoint = null;
                try
                {
                    Reference currentTrussBaseRef = sel.PickObject(ObjectType.Element, currentTrussBaseSupportFilter, "Selecione uma base para a treliça ou (ESC) para ignorar");
                    Element currentTrussBaseElem = doc.GetElement(currentTrussBaseRef.ElementId);

                    //We can safely convert because the selection filter does not select anything that is not a curve locatated
                    Curve currentElementCurve = (currentTrussBaseElem.Location as LocationCurve).Curve;

                    if (currentRidgeLine != null)
                    {
                        if (currentElementCurve is Line)
                        {
                            Line currentSupportLine = currentElementCurve as Line;
                            double height = currentRidgeInfo.GetCurrentRoofHeight();
                            currentRidgeLine = currentRidgeLine.Flatten(height);
                            currentSupportLine = currentSupportLine.Flatten(height);

                            IntersectionResultArray iResutArr = new IntersectionResultArray();
                            SetComparisonResult compResult = currentRidgeLine.Intersect(currentSupportLine, out iResutArr);

                            if (iResutArr.Size == 1)
                            {
                                IntersectionResult iResult = iResutArr.get_Item(0);
                                if (iResult != null)
                                    baseSupportPoint = currentRidgeInfo.Curve.Project(iResult.XYZPoint).XYZPoint;
                            }
                        }
                    }

                }
                catch
                {
                }

                if (baseSupportPoint == null)
                {
                    baseSupportPoint = currentRidgeLine.Project(currentReference.GlobalPoint).XYZPoint;
                }


                Managers.TrussRidgeManager currentTrussManager = new Managers.TrussRidgeManager();

                Line currentSupport0ElemLine = null;
                Line currentSupport1ElemLine = null;

                currentTrussBaseSupportFilter = new RoofClasses.SelectionFilters.SupportsSelectionFilter(currentRidgeLine.Direction);

                if (currentRidgeInfo.RoofLineType == RoofClasses.RoofLineType.Ridge)
                {
                    try
                    {
                        Reference currentSupport0Ref = sel.PickObject(ObjectType.Element, currentTrussBaseSupportFilter, "O primeiro suporte para a treliça");
                        Element currentSupport0Elem = doc.GetElement(currentSupport0Ref.ElementId);
                        Curve currentSupport0ElemCurve = (currentSupport0Elem.Location as LocationCurve).Curve;
                        currentSupport0ElemLine = currentSupport0ElemCurve as Line;

                        Reference currentSupport1Ref = sel.PickObject(ObjectType.Element, currentTrussBaseSupportFilter, "O segundo suporte para a treliça");
                        Element currentSupport1Elem = doc.GetElement(currentSupport1Ref.ElementId);
                        Curve currentSupport1ElemCurve = (currentSupport1Elem.Location as LocationCurve).Curve;
                        currentSupport1ElemLine = currentSupport1ElemCurve as Line;
                    }
                    catch
                    {
                        currentSupport0ElemLine = null;
                        currentSupport1ElemLine = null;
                    }
                }
                else if (currentRidgeInfo.RoofLineType == RoofClasses.RoofLineType.RidgeSinglePanel)
                {
                    try
                    {
                        Reference currentSupport0Ref = sel.PickObject(ObjectType.Element, currentTrussBaseSupportFilter, "O suporte para a treliça");
                        Element currentSupport0Elem = doc.GetElement(currentSupport0Ref.ElementId);
                        Curve currentSupport0ElemCurve = (currentSupport0Elem.Location as LocationCurve).Curve;
                        currentSupport0ElemLine = currentSupport0ElemCurve as Line;
                    }
                    catch
                    {
                        currentSupport0ElemLine = null;
                    }
                }

                RoofClasses.TrussInfo currentTrussInfo = currentTrussManager.CreateTrussFromRidgeWithSupports(baseSupportPoint, currentRidgeInfo, tType, currentSupport0ElemLine, currentSupport1ElemLine);

                //#region DEBUG ONLY

                //if (currentReference != null)
                //    DEBUG.CreateDebugPoint(doc, baseSupportPoint);

                //#endregion

            }
            catch (Exception e)
            {
                if (!(e is Autodesk.Revit.Exceptions.OperationCanceledException))
                {
                    throw e;
                }
            }

            return Result.Succeeded;
        }
    }

    [Transaction(TransactionMode.Manual)]
    class TrussRidgeTest : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            Document doc = uidoc.Document;

            if ((uidoc.ActiveView as View3D) == null)
            {
                message = "Por favor, rode este comando em uma vista 3d";
                return Result.Failed;
            }

            Reference currentReference = uidoc.Selection.PickObject(ObjectType.Edge);
            FootPrintRoof currentFootPrintRoof = doc.GetElement(currentReference.ElementId) as FootPrintRoof;
            Edge edge = currentFootPrintRoof.GetGeometryObjectFromReference(currentReference) as Edge;

            IList<PlanarFace> pfaces = new List<PlanarFace>();
            Support.IsListOfPlanarFaces(HostObjectUtils.GetBottomFaces(currentFootPrintRoof).Union(HostObjectUtils.GetTopFaces(currentFootPrintRoof)).ToList()
                , currentFootPrintRoof, out pfaces);

            RoofClasses.EdgeInfo currentInfo = Support.GetCurveInformation(currentFootPrintRoof, edge.AsCurve(), pfaces);

            using (Transaction t = new Transaction(doc, "Test"))
            {
                t.Start();

                FamilySymbol fs = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_GenericModel).WhereElementIsElementType().Where(type => type.Name.Contains("DebugPoint")).FirstOrDefault() as FamilySymbol;
                fs.Activate();
                doc.Create.NewFamilyInstance((currentInfo.Curve as Line).Direction, fs, Autodesk.Revit.DB.Structure.StructuralType.NonStructural);
                doc.Create.NewFamilyInstance(((currentInfo.Curve as Line).Direction).Rotate(-90), fs, Autodesk.Revit.DB.Structure.StructuralType.NonStructural);
                doc.Create.NewFamilyInstance(((currentInfo.Curve as Line).Direction).Rotate(45), fs, Autodesk.Revit.DB.Structure.StructuralType.NonStructural);
                t.Commit();
            }

            return Result.Succeeded;
        }
    }

    [Transaction(TransactionMode.Manual)]
    class TrussRidgePlace : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            Document doc = uidoc.Document;

            if ((uidoc.ActiveView as View3D) == null)
            {
                message = "Por favor, rode este comando em uma vista 3d";
                return Result.Failed;
            }


            ISelectionFilter ridgeSelectionFilter = new RoofClasses.SelectionFilters.StraightLinesAndFacesRidgeSelectionFilter(doc);
            Reference currentReference = uidoc.Selection.PickObject(ObjectType.Edge, ridgeSelectionFilter);

            FootPrintRoof currentFootPrintRoof = doc.GetElement(currentReference) as FootPrintRoof;
            Edge edge = Support.GetEdgeFromReference(currentReference, currentFootPrintRoof);

            IList<PlanarFace> pfaces = new List<PlanarFace>();
            Support.IsListOfPlanarFaces(HostObjectUtils.GetBottomFaces(currentFootPrintRoof)
                , currentFootPrintRoof, out pfaces);

            IList<RoofClasses.EdgeInfo> currentEdgeInfoList = Support.GetRoofEdgeInfoList(currentFootPrintRoof, false);
            Curve currentCurve = Support.GetMostSimilarCurve(edge.AsCurve(), currentEdgeInfoList);

            RoofClasses.EdgeInfo currentInfo = Support.GetCurveInformation(currentFootPrintRoof, currentCurve, pfaces);
            TaskDialog.Show("fac", currentInfo.RoofLineType.ToString());

            Element tTypeElement = new FilteredElementCollector(doc).OfClass(typeof(FamilySymbol)).Where(fsy => fsy is TrussType).ToList().FirstOrDefault();

            if (tTypeElement == null)
            {
                message = "Nenhum tipo de treliça foi encontrada no projeto, por favor, carregue um tipo e rode este comando novamente";
                return Result.Failed;
            }

            TrussType tType = tTypeElement as TrussType;
            Managers.TrussRidgeManager currentTrussManager = new Managers.TrussRidgeManager();
            IList<RoofClasses.TrussInfo> currentTrussInfoList = currentTrussManager.CreateTrussesFromRidgeInfo(currentInfo, tType);

            TaskDialog tDialog = new TaskDialog("Trusses");
            tDialog.MainInstruction = currentTrussInfoList.Count.ToString();
            tDialog.Show();

            return Result.Succeeded;
        }
    }
}
