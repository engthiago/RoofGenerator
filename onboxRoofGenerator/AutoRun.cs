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

            Reference currentReference = uidoc.Selection.PickObject(ObjectType.Element, new RoofClasses.SelectionFilters.FootPrintRoofSelFilter(), "Selecione um telhado.");
            FootPrintRoof currentFootPrintRoof = doc.GetElement(currentReference) as FootPrintRoof;

            //TODO if the footprint contains something other than lines (straight lines) warn the user and exit
            Element tTypeElement = new FilteredElementCollector(doc).OfClass(typeof(FamilySymbol)).Where(fsy => fsy is TrussType).ToList().FirstOrDefault();

            if (tTypeElement == null)
            {
                message = "Nenhum tipo de treliça foi encontrada no projeto, por favor, carregue um tipo e rode este comando novamente";
                return Result.Failed;
            }

            TrussType tType = tTypeElement as TrussType;
            Managers.TrussManager currentTrussManager = new Managers.TrussManager();
            IList<RoofClasses.TrussInfo> currentTrussInfoList = currentTrussManager.CreateTrussesFromRoof(currentFootPrintRoof, tType);

            TaskDialog tDialog = new TaskDialog("Trusses");
            tDialog.MainInstruction = currentTrussInfoList.Count.ToString();
            tDialog.Show();

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

            //if ((uidoc.ActiveView as View3D) == null)
            //{
            //    message = "Por favor, rode este comando em uma vista 3d";
            //    return Result.Failed;
            //}

            //Reference currentReference = uidoc.Selection.PickObject(ObjectType.Element, new RoofClasses.SelectionFilters.FootPrintRoofSelFilter(), "Selecione um telhado.");
            //FootPrintRoof currentFootPrintRoof = doc.GetElement(currentReference.ElementId) as FootPrintRoof;

            //IList<RoofClasses.EdgeInfo> currentRoofEdgeInfoList = Support.GetRoofEdgeInfoList(currentFootPrintRoof, false);

            //using (Transaction t2 = new Transaction(doc, "Points"))
            //{
            //    t2.Start();
            //    foreach (RoofClasses.EdgeInfo currentEdgeInfo in currentRoofEdgeInfoList)
            //    {
            //        if (currentEdgeInfo.RoofLineType == RoofClasses.RoofLineType.Ridge || currentEdgeInfo.RoofLineType == RoofClasses.RoofLineType.RidgeSinglePanel)
            //        {
            //            IList<XYZ> currentPoints = currentEdgeInfo.ProjectSupportPointsOnRoof(3);
            //            FamilySymbol fs = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_GenericModel).WhereElementIsElementType().Where(type => type.Name.Contains("DebugPoint")).FirstOrDefault() as FamilySymbol;
            //            foreach (XYZ currentPoint in currentPoints)
            //            {
            //                doc.Create.NewFamilyInstance(currentPoint, fs, Autodesk.Revit.DB.Structure.StructuralType.NonStructural);
            //            }
            //        }
            //    }
            //    t2.Commit();
            //}


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

            RoofClasses.EdgeInfo currentInfo = Support.GetCurveInformation(currentFootPrintRoof, edge.AsCurve(), pfaces);

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
    class DetectSingleEdgeAndPlaceTrusses : IExternalCommand
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
            TaskDialog.Show("fac", currentInfo.RoofLineType.ToString());

            Element tTypeElement = new FilteredElementCollector(doc).OfClass(typeof(FamilySymbol)).Where(fsy => fsy is TrussType).ToList().FirstOrDefault();

            if (tTypeElement == null)
            {
                message = "Nenhum tipo de treliça foi encontrada no projeto, por favor, carregue um tipo e rode este comando novamente";
                return Result.Failed;
            }

            TrussType tType = tTypeElement as TrussType;
            Managers.TrussManager currentTrussManager = new Managers.TrussManager();
            IList<RoofClasses.TrussInfo> currentTrussInfoList = currentTrussManager.CreateTrussesFromRidgeInfo(currentInfo, tType);

            TaskDialog tDialog = new TaskDialog("Trusses");
            tDialog.MainInstruction = currentTrussInfoList.Count.ToString();
            tDialog.Show();

            return Result.Succeeded;
        }
    }
}
