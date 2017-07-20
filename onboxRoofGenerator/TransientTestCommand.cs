using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace onboxRoofGenerator
{
    class MakeTransient : ITransientElementMaker
    {
        Document Doc { get; set; }
        public DirectShape Shape { get; set; }

        public MakeTransient(Document doc)
        {
            Doc = doc;
        }
        public void Execute()
        {
            Shape = DirectShape.CreateElement(Doc, new ElementId(BuiltInCategory.OST_GenericAnnotation));
            DirectShapeType dt = DirectShapeType.Create(Doc, "typ", new ElementId(BuiltInCategory.OST_GenericAnnotation));
            ViewShapeBuilder v = new ViewShapeBuilder(DirectShapeTargetViewType.Plan);
            v.ViewNormal = Doc.ActiveView.ViewDirection;

            Transform tr = Transform.CreateTranslation(new XYZ(50, 50, 0));


            Line l = Line.CreateBound(XYZ.Zero, new XYZ(50, 50, 0)).CreateTransformed(tr) as Line;
            if (v.ValidateCurve(l))
                v.AddCurve(l);
            l = Line.CreateBound(XYZ.Zero, new XYZ(-60, 60, 0)).CreateTransformed(tr) as Line;
            if (v.ValidateCurve(l))
                v.AddCurve(l);

            Shape.AppendShape(v);
            Shape.SetName("myshape2");
            Shape.SetTypeId(dt.Id);
            DirectShapeLibrary.GetDirectShapeLibrary(Doc).AddDefinitionType("yy", dt.Id);
        }
    }

    class DeleteTransient : ITransientElementMaker
    {
        Document Doc { get; set; }
        public DirectShape Shape { get; set; }
        public ElementId ElemId { get; set; }

        public DeleteTransient(Document doc)
        {
            Doc = doc;
        }
        public void Execute()
        {

            DirectShapeLibrary dl = DirectShapeLibrary.GetDirectShapeLibrary(Doc);
            ElemId = dl.FindDefinitionType("yy");
        }
    }


    [Transaction(TransactionMode.Manual)]
    class TransientTestDelete : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            Document doc = uidoc.Document;

            var a = new DeleteTransient(doc);
            doc.MakeTransientElements(a);

            using (Transaction t = new Transaction(doc, "ee"))
            {
                t.Start();
                doc.Delete(a.ElemId);
                t.Commit();
            }

            return Result.Succeeded;
        }
    }

    [Transaction(TransactionMode.Manual)]
    class TransientTestCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            Document doc = uidoc.Document;

            doc.MakeTransientElements(new MakeTransient(doc));

            using (Transaction t = new Transaction(doc, "Regen"))
            {
                t.Start();
                doc.Regenerate();
                t.Commit();
            }

            return Result.Succeeded;
        }
    }
}
