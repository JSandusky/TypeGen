using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace typegen
{
    public class DataFieldsGenerator
    {
        public void WriteDataFields(StringBuilder sb, CodeScanDB database)
        {
            var types = database.FlatTypes.Where(t => t.isPrimitive_ == false && t.isTemplate_ == false && t.isInternal_ == false);

            sb.AppendLine();
            sb.AppendLine("<fieldict.h>");
            sb.AppendLine();
            sb.AppendLine("void RegisterDataFields() {");

            foreach (var type in types)
            {
                sb.AppendLine("  {");
                sb.AppendLine("    auto fieldList = std::make_shared<DataFieldInfoList>()");
                sb.AppendLine("    std::shared_ptr<DataFieldInfo> fieldInfo;");
                sb.AppendLine();
                foreach (var fld in type.properties_)
                {
                    string tagID = fld.bindingData_.Get("tag");
                    sb.AppendLine($"    fieldInfo = fieldList->CreateFieldInfo(\"{fld.propertyName_}\", {tagID}, FieldType<{fld.GetFullTypeName(false)}>::Value);");
                    if (fld.bindingData_.GetBool("readonly", false))
                        sb.AppendLine("    fieldInfo->SetReadOnly(true);");
                    if (fld.bindingData_.GetBool("multiline", false))
                        sb.AppendLine("    fieldInfo->SetMultiline(true);");
                    if (fld.bindingData_.HasTrait("min"))
                        sb.AppendLine($"    fieldInfo->SetMinValue({fld.bindingData_.Get("min")});");
                    if (fld.bindingData_.HasTrait("max"))
                        sb.AppendLine($"    fieldInfo->SetMaxValue({fld.bindingData_.Get("max")});");
                    if (fld.bindingData_.HasTrait("step"))
                        sb.AppendLine($"    fieldInfo->SetStepValue({fld.bindingData_.Get("step")});");
                    if (fld.type_.enumValues_.Count > 0)
                    {
                        foreach (var value in fld.type_.enumValues_)
                            sb.AppendLine($"    fieldInfo->AddChoice(\"{value.Key}\", {value.Value});");
                    }
                }
                sb.AppendLine();
                sb.AppendLine($"    RegisterFieldList(\"{type.typeName_}\", fieldList);");
                sb.AppendLine("  }");
            }

            sb.AppendLine("}");
            sb.AppendLine();
        }
    }
}
