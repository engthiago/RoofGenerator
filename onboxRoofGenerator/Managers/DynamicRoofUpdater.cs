using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace onboxRoofGenerator.Managers
{
    class DynamicRoofUpdater : IUpdater
    {
        static AddInId appId;
        static UpdaterId updaterId;

        public DynamicRoofUpdater(AddInId id)
        {
            appId = id;

            updaterId = new UpdaterId(appId, new Guid(
              "F6569EE1-813F-4054-9956-93547ADC0BF5"));
        }

        public void Execute(UpdaterData data)
        {
            Document doc = data.GetDocument();
            UIDocument uidoc = new UIDocument(doc);

            RevitCommandId updateRoof = RevitCommandId.LookupCommandId("onboxRoofGenerator.TransientRoofUpdater");
            ElementId elemId = ElementId.InvalidElementId;

            using (Managers.OnboxRoofStorage onboxStorage = new Managers.OnboxRoofStorage())
            {
                elemId = onboxStorage.GetRoofIdNumber(doc);
                if (elemId == ElementId.InvalidElementId) return;
            }

            Autodesk.Revit.ApplicationServices.Application app = doc.Application;
            foreach (ElementId id in
              data.GetModifiedElementIds())
            {
                if (elemId == id)
                {
                    if (uidoc.Application.CanPostCommand(updateRoof))
                    {
                        uidoc.Application.PostCommand(updateRoof);
                    } 
                }
            }
        }

        public string GetAdditionalInformation()
        {
            return "Onbox Roofing - www.onboxdesign.com.br";
        }

        public ChangePriority GetChangePriority()
        {
            return ChangePriority.FloorsRoofsStructuralWalls;
        }

        public UpdaterId GetUpdaterId()
        {
            return updaterId;
        }

        public string GetUpdaterName()
        {
            return "Roof Updater";
        }
    }
}
