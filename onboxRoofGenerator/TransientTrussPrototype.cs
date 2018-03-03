using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.Revit.DB;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.UI.Selection;
using Autodesk.Revit.ApplicationServices;

namespace onboxRoofGenerator
{
    //[Transaction(TransactionMode.Manual)]
    //class TransientTrussPrototype : IExternalCommand
    //{
    //    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    //    {
    //        UIDocument uidoc = commandData.Application.ActiveUIDocument;
    //        Document doc = uidoc.Document;
    //        Selection sel = uidoc.Selection;

    //        Reference footPrintRoofRef = sel.PickObject(ObjectType.Element, new RoofClasses.SelectionFilters.StraightLinesAndFacesFootPrintRoofSelFilter(), "Select a footprint roof");
    //        Element footPrintRoofElem = doc.GetElement(footPrintRoofRef);
    //        FootPrintRoof footPrintRoof = footPrintRoofElem as FootPrintRoof;

    //        Managers.TransientTrussRidgeManager transientTrussManager = new Managers.TransientTrussRidgeManager();
    //        transientTrussManager.CreateTrussesFromRoof(footPrintRoof);


    //        return Result.Succeeded;
    //    }
    //}

    [Transaction(TransactionMode.Manual)]
    class TransientRoofUpdater : IExternalCommand
    {
        //static AddInId appId = new AddInId(new Guid("8F96A2D8-D011-4922-9927-B1B5AA9F0D78"));
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            Document doc = uidoc.Document;
            Selection sel = uidoc.Selection;

            FootPrintRoof footPrintRoof = null;

            using (Managers.OnboxRoofStorage onboxStorage = new Managers.OnboxRoofStorage())
            {
                ElementId elemId = onboxStorage.GetRoofIdNumber(doc);
                if (elemId == ElementId.InvalidElementId) return Result.Failed;
                footPrintRoof = doc.GetElement(elemId) as FootPrintRoof;
            }

            Managers.TransientTrussRidgeManager transientTrussManager = new Managers.TransientTrussRidgeManager();
            transientTrussManager.CreateTrussesFromRoof(footPrintRoof);


            return Result.Succeeded;
        }
    }

    [Transaction(TransactionMode.Manual)]
    class TransientTrussPrototype : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            Application app = commandData.Application.Application;
            Document doc = uidoc.Document;

            Reference footPrintRoofRef = uidoc.Selection.PickObject(ObjectType.Element, new RoofClasses.SelectionFilters.StraightLinesAndFacesFootPrintRoofSelFilter(), "Select a footprint roof");
            Element footPrintRoofElem = doc.GetElement(footPrintRoofRef);
            FootPrintRoof footPrintRoof = footPrintRoofElem as FootPrintRoof;

            using (Transaction t = new Transaction(doc, "store id"))
            {
                t.Start();
                using (Managers.OnboxRoofStorage onboxStorage = new Managers.OnboxRoofStorage())
                {
                    onboxStorage.SetRoofIdNumber(doc, footPrintRoof.Id);
                }
                t.Commit();
            }

            Managers.DynamicRoofUpdater updater
              = new Managers.DynamicRoofUpdater(
                app.ActiveAddInId);

            UpdaterRegistry.RegisterUpdater(updater);

            ElementCategoryFilter f
              = new ElementCategoryFilter(
                BuiltInCategory.OST_Roofs);

            UpdaterRegistry.AddTrigger(
              updater.GetUpdaterId(), f,
              Element.GetChangeTypeGeometry());

            return Result.Succeeded;
        }
    }

    //[Transaction(TransactionMode.Manual)]
    //class TransientTrussPrototype : IExternalCommand
    //{
    //    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    //    {
    //        UIDocument uidoc = commandData.Application.ActiveUIDocument;
    //        Document doc = uidoc.Document;
    //        Selection sel = uidoc.Selection;


    //        Reference footPrintRoofRef = sel.PickObject(ObjectType.Element, new RoofClasses.SelectionFilters.StraightLinesAndFacesFootPrintRoofSelFilter(), "Select a footprint roof");
    //        Element footPrintRoofElem = doc.GetElement(footPrintRoofRef);
    //        FootPrintRoof footPrintRoof = footPrintRoofElem as FootPrintRoof;

    //        Managers.TransientTrussRidgeManager transientTrussManager = new Managers.TransientTrussRidgeManager();
    //        transientTrussManager.CreateTrussesFromRoof(footPrintRoof);


    //        uidoc.Application.Application.DocumentChanged += Application_DocumentChanged;


    //        return Result.Succeeded;
    //    }

    //    private void Application_DocumentChanged(object sender, Autodesk.Revit.DB.Events.DocumentChangedEventArgs e)
    //    {
    //        ICollection<ElementId> ElemntIds = e.GetModifiedElementIds();
    //        if (ElemntIds.Count == 0) return;

    //        Document doc = e.GetDocument();

    //        foreach (ElementId currentElementId in ElemntIds)
    //        {
    //            Element currentElement = doc.GetElement(currentElementId);
    //            FootPrintRoof currentRoof = currentElement as FootPrintRoof;

    //            if (currentRoof == null) continue;

    //            Managers.TransientTrussRidgeManager transientTrussManager = new Managers.TransientTrussRidgeManager();
    //            transientTrussManager.CreateTrussesFromRoof(currentRoof);

    //        }

    //    }
    //}
}
