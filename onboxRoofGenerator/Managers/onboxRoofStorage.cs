using Autodesk.Revit.DB;
using Autodesk.Revit.DB.ExtensibleStorage;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace onboxRoofGenerator.Managers
{
    class OnboxRoofStorage : IDisposable
    {

        #region Private fields

        Schema onboxRoofSchema = null;
        string onboxRoofSchemaName = "OnboxRoof";
        Guid onboxRoofSchemaGuid = new Guid("9064A515-92EB-4000-AF7F-F25D69299240");
        Entity onboxRoofEntity = null;
        Field roofId = null;

        #endregion

        public OnboxRoofStorage()
        {
            IList<Schema> schemaList = Schema.ListSchemas();
            onboxRoofSchema = schemaList.FirstOrDefault(s => s.GUID == onboxRoofSchemaGuid);

            if (onboxRoofSchema == null)
                onboxRoofSchema = CreateSchema();

            onboxRoofEntity = new Entity(onboxRoofSchema);
            roofId = onboxRoofSchema.GetField("roofIdNumber");
        }


        internal ElementId GetRoofIdNumber(Document doc)
        {
            int currentNumber = -1;

            if (onboxRoofSchema != null)
            {
                if (onboxRoofEntity != null)
                {
                    if (roofId != null)
                    {
                        onboxRoofEntity = doc.ProjectInformation.GetEntity(onboxRoofSchema);
                        if (onboxRoofEntity != null && onboxRoofEntity.IsValid())
                        {
                            currentNumber = onboxRoofEntity.Get<int>("roofIdNumber");
                        }
                    }
                }
            }
            return new ElementId(currentNumber);
        }

        internal void SetRoofIdNumber(Document doc, ElementId targetRoofId)
        {
            if (onboxRoofEntity != null)
            {
                if (roofId == null)
                    onboxRoofSchema = CreateSchema();

                int targetValue = targetRoofId.IntegerValue;

                onboxRoofEntity.Set(roofId, targetValue);
                doc.ProjectInformation.SetEntity(onboxRoofEntity);
            }
        }

        private Schema CreateSchema()
        {
            SchemaBuilder schBuilder = new SchemaBuilder(onboxRoofSchemaGuid);
            schBuilder.SetSchemaName(onboxRoofSchemaName);

            FieldBuilder fieldLastUsedNumber = schBuilder.AddSimpleField("roofIdNumber", typeof(int));
            Schema currentSchema = schBuilder.Finish();

            if (currentSchema == null)
                throw new Exception("Schema can't be created!");

            return currentSchema;
        }

        public void Dispose()
        {
            if (onboxRoofSchema != null) onboxRoofSchema.Dispose();
            if (onboxRoofEntity != null) onboxRoofEntity.Dispose();
            if (roofId != null) roofId.Dispose();
        }
    }
}
