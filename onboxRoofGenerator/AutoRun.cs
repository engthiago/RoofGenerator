using Autodesk.Revit.Attributes;
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
    [Transaction(TransactionMode.Manual)]
    class AutoRun : IExternalCommand
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

            Reference currentReference = uidoc.Selection.PickObject(ObjectType.Element, new FootPrintRoofSelFilter(), "Selecione um telhado.");
            FootPrintRoof currentFootPrintRoof = doc.GetElement(currentReference) as FootPrintRoof;

            //TODO if the footprint contains something other than lines (straight lines) warn the user and exit
            Element tTypeElement = new FilteredElementCollector(doc).OfClass(typeof(FamilySymbol)).Where(fsy => fsy is TrussType).ToList().FirstOrDefault();

            if (tTypeElement == null)
            {
                message = "Nenhum tipo de treliça foi encontrada, por favor, carregue um tipo e rode este comando novamente";
                return Result.Failed;
            }

            TrussType tType = tTypeElement as TrussType;
            IList<EdgeInfo> currentRoofEdgeInfoList = new List<EdgeInfo>();

            using (Transaction t = new Transaction(doc, "Points"))
            {
                t.Start();
                currentRoofEdgeInfoList = Support.GetRoofEdgeInfoList(currentFootPrintRoof, false);

                foreach (EdgeInfo currentEdgeInfo in currentRoofEdgeInfoList)
                {
                    if (currentEdgeInfo.RoofLineType == RoofLineType.RidgeSinglePanel || currentEdgeInfo.RoofLineType == RoofLineType.Ridge)
                    {
                        Tuple<int, double> iterations = Utils.Utils.EstabilishIterations(currentEdgeInfo.Curve.ApproximateLength, 8.22);
                        int numPoints = iterations.Item1;
                        double distance = iterations.Item2;

                        for (int i = 0; i <= numPoints; i++)
                        {
                            TrussInfo currentTrussInfo = null;
                            double currentParam = i * distance;

                            if (currentEdgeInfo.CanBuildTrussAtRidge(currentParam, out currentTrussInfo))
                            {
                                SketchPlane stkP = SketchPlane.Create(doc, currentEdgeInfo.CurrentRoof.LevelId);

                                double levelHeight = currentEdgeInfo.GetCurrentRoofHeight();

                                XYZ firstPoint = new XYZ(currentTrussInfo.FirstPoint.X, currentTrussInfo.FirstPoint.Y, levelHeight);
                                XYZ secondPoint = new XYZ(currentTrussInfo.SecondPoint.X, currentTrussInfo.SecondPoint.Y, levelHeight);
                                Truss currentTruss = Truss.Create(doc, tType.Id, stkP.Id, Line.CreateBound(firstPoint, secondPoint));

                                currentTruss.get_Parameter(BuiltInParameter.TRUSS_HEIGHT).Set(currentTrussInfo.Height);

                            }
                            else
                            {
                                FamilySymbol fs = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_GenericModel).WhereElementIsElementType().Where(type => type.Name.Contains("DebugPoint")).FirstOrDefault() as FamilySymbol;
                                doc.Create.NewFamilyInstance(currentEdgeInfo.GetTrussTopPoint(currentParam), fs, Autodesk.Revit.DB.Structure.StructuralType.NonStructural);
                            }
                        }
                    }
                }

                t.Commit();
            }


            IList<EdgeInfo> Ridges = currentRoofEdgeInfoList.Where(ed => ed.RoofLineType == RoofLineType.Ridge || ed.RoofLineType == RoofLineType.RidgeSinglePanel).ToList();

            //EdgeInfo currentRidge = Ridges[0];

            //foreach (EdgeInfo currentEndCondition in currentRidge.GetEndConditions(1))
            //{
            //    using (Transaction t = new Transaction(doc, "Place bounding points"))
            //    {
            //        t.Start();
            //        TaskDialog.Show("Results", currentEndCondition.RoofLineType.ToString());

            //        XYZ currentPoint = currentRidge.Curve.GetEndPoint(1);
            //        XYZ currenta = currentEndCondition.Curve.Evaluate(0.5, true);
            //        doc.Create.NewFamilyInstance(currentPoint, fs, Autodesk.Revit.DB.Structure.StructuralType.NonStructural);
            //        doc.Create.NewFamilyInstance(currenta, fs, Autodesk.Revit.DB.Structure.StructuralType.NonStructural);
            //        t.Commit();
            //    }
            //}


            //using (Transaction t = new Transaction(doc, "Place bounding points"))
            //{
            //    t.Start();
            //    foreach (EdgeInfo currentEdgeInfo in currentRoofEdgeInfoList)
            //    {
            //        XYZ currentPoint = currentEdgeInfo.Curve.Evaluate(0.5, true);
            //        doc.Create.NewFamilyInstance(currentPoint, fs, Autodesk.Revit.DB.Structure.StructuralType.NonStructural);

            //        //XYZ currentPoint = currentEdgeInfo.ProjectRidgePointOnEave(2);
            //        //doc.Create.NewFamilyInstance(currentPoint, fs, Autodesk.Revit.DB.Structure.StructuralType.NonStructural);

            //        //XYZ currentThirdPoint = currentEdgeInfo.ProjectRidgePointOnOverhang(2);
            //        //doc.Create.NewFamilyInstance(currentThirdPoint, fs, Autodesk.Revit.DB.Structure.StructuralType.NonStructural);

            //    }
            //    t.Commit();
            //}


            return Result.Succeeded;
        }
    }

    [Transaction(TransactionMode.Manual)]
    class DetectEavePoints : IExternalCommand
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

            Reference currentReference = uidoc.Selection.PickObject(ObjectType.Element, new FootPrintRoofSelFilter(), "Selecione um telhado.");
            FootPrintRoof currentFootPrintRoof = doc.GetElement(currentReference.ElementId) as FootPrintRoof;

            IList<EdgeInfo> currentRoofEdgeInfoList = Support.GetRoofEdgeInfoList(currentFootPrintRoof, false);

            using (Transaction t2 = new Transaction(doc, "Points"))
            {
                t2.Start();
                foreach (EdgeInfo currentEdgeInfo in currentRoofEdgeInfoList)
                {
                    if (currentEdgeInfo.RoofLineType == RoofLineType.Ridge || currentEdgeInfo.RoofLineType == RoofLineType.RidgeSinglePanel)
                    {
                        IList<XYZ> currentPoints = currentEdgeInfo.ProjectSupportPointsOnRoof(3);
                        FamilySymbol fs = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_GenericModel).WhereElementIsElementType().Where(type => type.Name.Contains("DebugPoint")).FirstOrDefault() as FamilySymbol;
                        foreach (XYZ currentPoint in currentPoints)
                        {
                            doc.Create.NewFamilyInstance(currentPoint, fs, Autodesk.Revit.DB.Structure.StructuralType.NonStructural);
                        }
                    }
                }
                t2.Commit();
            }


            return Result.Succeeded;
        }
    }

    [Transaction(TransactionMode.Manual)]
    class Test : IExternalCommand
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

            EdgeInfo currentInfo = Support.GetCurveInformation(currentFootPrintRoof, edge.AsCurve(), pfaces);

            using (Transaction t = new Transaction(doc, "Test"))
            {
                t.Start();

                FamilySymbol fs = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_GenericModel).WhereElementIsElementType().Where(type => type.Name.Contains("DebugPoint")).FirstOrDefault() as FamilySymbol;
                doc.Create.NewFamilyInstance((currentInfo.Curve as Line).Direction, fs, Autodesk.Revit.DB.Structure.StructuralType.NonStructural);
                doc.Create.NewFamilyInstance(((currentInfo.Curve as Line).Direction).Rotate(-90), fs, Autodesk.Revit.DB.Structure.StructuralType.NonStructural);
                doc.Create.NewFamilyInstance(((currentInfo.Curve as Line).Direction).Rotate(45), fs, Autodesk.Revit.DB.Structure.StructuralType.NonStructural);
                t.Commit();
            }
            
            return Result.Succeeded;
        }
    }

    [Transaction(TransactionMode.Manual)]
    class DetectSingleEdge : IExternalCommand
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

            EdgeInfo currentInfo = Support.GetCurveInformation(currentFootPrintRoof, edge.AsCurve(), pfaces);
            TaskDialog.Show("fac", currentInfo.RoofLineType.ToString());


            return Result.Succeeded;
        }
    }
}
