using Mono.Cecil;
using static ProtoLurker.WireFormat;

namespace ProtoLurker
{
    /// <summary>
    /// Description of FieldDescription.
    /// </summary>
    public abstract class FieldDescription : ObjectDescription
    {

        //CHANGE ME
        public const string PROTOBUF_BYTESTRING = "CELDHOLFBFE";

        public static Dictionary<string, string> pbTypeNames = new Dictionary<string, string>() {
            {typeof(System.UInt32).FullName, "uint32"},
            {typeof(System.UInt64).FullName, "uint64"},
            {typeof(System.Int32).FullName, "int32"},
            {typeof(System.Int64).FullName, "int64"},
            {typeof(System.Boolean).FullName, "bool"},
            {typeof(System.String).FullName, "string"},
            {typeof(float).FullName, "float"},
            {typeof(double).FullName, "double"},
            {"Google.Protobuf.ByteString", "bytes"},
            {PROTOBUF_BYTESTRING, "bytes"},
            {"System.Fixed32", "fixed32"}
        };

        public FieldDefinition fieldDefinition;
        public PropertyDefinition propertyDefinition;

        public FieldDescription(FieldDefinition field, PropertyDefinition property = null) : base(property == null ? field.Name : property.Name)
        {
            this.fieldDefinition = field;
            this.propertyDefinition = property;
        }

        public static string MapCsTypeToPb(TypeReference type, bool annotate_enums = false, bool add_repeated = true)
        {
            if (pbTypeNames.ContainsKey(type.FullName))
                return pbTypeNames[type.FullName];

            if (pbTypeNames.ContainsKey(type.Name))
                return pbTypeNames[type.Name];

            var gt = type as GenericInstanceType;

            if (gt != null && gt.GenericArguments.Count == 1)
            {

                var element_type = gt.GenericArguments[0];

                var type_name = MapCsTypeToPb(element_type, annotate_enums);

                if (add_repeated)
                {
                    return "repeated " + type_name;
                }

                if (pbTypeNames.ContainsKey(element_type.FullName))
                {
                    return "packed " + type_name;
                }

                return type_name;
            }

            if (gt != null && gt.GenericArguments.Count == 2)
            {
                var key_type = MapCsTypeToPb(gt.GenericArguments[0], annotate_enums);
                var value_type = MapCsTypeToPb(gt.GenericArguments[1], annotate_enums);

                return string.Format("map<{0}, {1}>", key_type, value_type);
            }

            var name = type.FullName.CutAfterPlusSlashAndDot();

            if (annotate_enums && type.Resolve().IsEnum) return "enum " + name;

            return name;
        }
    }

    public class RegularField : FieldDescription
    {
        public WireType wire_tag = 0;
        public int field_number = 0;

        public override TypeReference Type
        {
            get
            {
                return propertyDefinition == null ? this.fieldDefinition.FieldType : this.propertyDefinition.PropertyType;
            }
        }

        public RegularField(FieldDefinition field, PropertyDefinition property = null, int field_number = 0, WireType wire_tag = WireType.Varint) : base(field, property) //we use property
        {
            this.field_number = field_number;
            this.wire_tag = wire_tag;
        }

        public override string[] ToPBLines()
        {

            //Ehm what the frick
            var field_type_string = MapCsTypeToPb(this.Type);
            if (this.wire_tag == WireFormat.WireType.Fixed32)
            {
                if (field_type_string == "int32") field_type_string = "sfixed32";
                if (field_type_string == "uint32") field_type_string = "fixed32";
            }
            else if (this.wire_tag == WireFormat.WireType.Fixed64)
            {
                if (field_type_string == "int64") field_type_string = "sfixed64";
                if (field_type_string == "uint64") field_type_string = "fixed64";
            }

            return new string[] { field_type_string + " " + GetName() + " = " + field_number };
        }

        public override string[] ToPILines()
        {
            return new string[] { field_number + ": (\"" + MapCsTypeToPb(this.Type, true, false) + "\", \"" + GetName() + "\")" };
        }

        public string GetName()
        {
            return Name.Replace("_field_number", "");
        }

        public string GetCommentNote()
        {
            return "// " + fieldDefinition.Name + " | " + fieldDefinition.CustomAttributes[0].Fields[0].Argument.Value.ToString() + " | " + WireFormat.MakeTag(field_number, wire_tag);
        }
    }

    public class OneofField : FieldDescription
    {
        public Dictionary<int, RegularField> Fields = null;

        public override TypeReference Type
        {
            get
            {
                return this.fieldDefinition.FieldType;
            }
        }
        public OneofField(FieldDefinition field) : base(field)
        {
            Fields = new Dictionary<int, RegularField>();
        }

        public void AddRecord(FieldDefinition field, PropertyDefinition property, int field_number, WireFormat.WireType wireTag)
        {
            int index = Fields.Count;
            var tuple = new RegularField(field, property, field_number, wireTag);
            Fields.Add(index, tuple);
        }

        public string GetName()
        {
            return Name.CutAfterPlusSlashAndDot().Replace("OneofCase", "");
        }

        public override string[] ToPBLines()
        {
            var lines = new List<string>();

            lines.Add("oneof " + GetName() + " {");

            foreach (var item in Fields)
            {
                lines.AddRange(item.Value.ToPBLines().PadStrings("\t", "; " + item.Value.GetCommentNote()));
            }

            lines.Add("}");

            return lines.ToArray();
        }

        public override string[] ToPILines()
        {
            // Oneofs aren't directly supported by protobuf-inspector, so we'll just put comments marking it's start/end
            var lines = new List<string>();

            lines.Add("# oneof " + GetName() + " {");

            foreach (var item in Fields)
            {
                // Note that we don't want to pad it
                lines.AddRange(item.Value.ToPILines().PadStrings(""));
            }

            lines.Add("# }");

            return lines.ToArray();
        }
    }
}
