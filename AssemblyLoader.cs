using Mono.Cecil;
using Mono.Cecil.Rocks;
using System.Text.Json;

namespace ProtoLurker
{
    internal class AssemblyLoader
    {

        //CHANGE ME
        public const string PROTOBUF_BASE_CLASS = "IPEGMFKCEKO";
        public const string PROTOBUF_MESSAGE_PARSER = "DMIOFPBAHMI";

        private AssemblyDefinition assembly;
        private InstructionParser instructionParser;

        public Dictionary<TypeDefinition, FunctionAddresses> functions = new Dictionary<TypeDefinition, FunctionAddresses>();
        public Dictionary<FieldDefinition, PropertyDefinition> fieldToProperty = new Dictionary<FieldDefinition, PropertyDefinition>();
        public Dictionary<PropertyDefinition, FieldDefinition> propertyToField = new Dictionary<PropertyDefinition, FieldDefinition>();
        public Dictionary<string, ObjectDescription> items = new Dictionary<string, ObjectDescription>();
        public Dictionary<string, string> nametranslations = new Dictionary<string, string>();
        public SortedDictionary<int, KeyValuePair<int, string>> cmdidsMapper = new SortedDictionary<int, KeyValuePair<int, string>>();

        public AssemblyLoader(string assembly_csharp, string filePath)
        {
            var resolver = new DefaultAssemblyResolver();
            resolver.AddSearchDirectory(Path.GetDirectoryName(assembly_csharp));
            this.assembly = AssemblyDefinition.ReadAssembly(assembly_csharp, new ReaderParameters { AssemblyResolver = resolver });
            this.instructionParser = new InstructionParser(this, filePath);
        }

        private IEnumerable<TypeDefinition> get_public_nested_types_recursively(TypeDefinition type)
        {
            var nested_types = new List<TypeDefinition>();

            foreach (var t in type.NestedTypes)
            {
                nested_types.AddRange(get_public_nested_types_recursively(t));
            }

            nested_types.AddRange(type.NestedTypes.Where(t => IsProtobufPacket(t) || t.IsEnum));

            return nested_types;
        }

        private bool IsParsed(TypeDefinition type)
        {
            return items.ContainsKey(type.Name);
        }

        private bool IsProtobufPacket(TypeDefinition t)
        {
            return t.BaseType != null && t.BaseType.FullName.Equals(PROTOBUF_BASE_CLASS) && !t.IsAbstract;
        }

        private ObjectDescription? parseType(TypeDefinition type)
        {
            try
            {
                return type.IsEnum ? (ObjectDescription)parseEnum(type) : (ObjectDescription)parseClass(type);
            }
            catch (Exception e)
            {
                Console.WriteLine("Unable to parse " + type.Name);
                Console.WriteLine(e.ToString());
                return null;
            }
        }

        private EnumDescription parseEnum(TypeDefinition t)
        {
            EnumDescription ed = new EnumDescription(t.FullName);

            var fields = t.Fields.Where(f => !f.Name.Equals("value__"));

            foreach (var f in fields)
            {
                var value = f.GetConstant();

                ItemDescription od = new ItemDescription(f.Name, (int)value);

                ed.Items.Add(f.Name, od);
            }

            return ed;
        }

        private ClassDescription parseClass(TypeDefinition type)
        {
            if (!IsProtobufPacket(type)) return null;
            FunctionAddresses functionAddress;

            //NOTICE ME!
            //if this fails, point them manually

            //The 4th from last function
            var writeTo = type.Methods.ElementAt(type.Methods.Count - 4);
            //The last function
            var mergeFrom = type.Methods.ElementAt(type.Methods.Count - 1);
            var getStaticCmdId = type.Methods.ElementAt(3);
            var ctorFunction = type.GetStaticConstructor();

            var writeToFunctionData = new Il2cppFunctionAddressData(writeTo.CustomAttributes[0].Fields[0].Argument.Value.ToString(), writeTo.CustomAttributes[0].Fields[1].Argument.Value.ToString(), writeTo.CustomAttributes[0].Fields[2].Argument.Value.ToString());
            var ctorFunctionData = new Il2cppFunctionAddressData(ctorFunction.CustomAttributes[0].Fields[0].Argument.Value.ToString(), ctorFunction.CustomAttributes[0].Fields[1].Argument.Value.ToString(), ctorFunction.CustomAttributes[0].Fields[2].Argument.Value.ToString());
            var staticCmdIdFunctionData = new Il2cppFunctionAddressData(getStaticCmdId.CustomAttributes[0].Fields[0].Argument.Value.ToString(), getStaticCmdId.CustomAttributes[0].Fields[1].Argument.Value.ToString(), getStaticCmdId.CustomAttributes[0].Fields[2].Argument.Value.ToString());
            var mergeFromFunctionData = new Il2cppFunctionAddressData(mergeFrom.CustomAttributes[0].Fields[0].Argument.Value.ToString(), mergeFrom.CustomAttributes[0].Fields[1].Argument.Value.ToString(), mergeFrom.CustomAttributes[0].Fields[2].Argument.Value.ToString());

            functionAddress = new FunctionAddresses(writeToFunctionData, ctorFunctionData, staticCmdIdFunctionData, mergeFromFunctionData);

            this.instructionParser.parse(type, functionAddress);
            ClassDescription cd = new ClassDescription(type.FullName, type);

            var inner_types = get_public_nested_types_recursively(type);
            var fields = this.instructionParser.fieldInfo.GetValueOrDefault(type);


            foreach (var inner_type in inner_types)
            {
                var skip = false;

                foreach (var field in fields)
                {
                    OneofField oneof = field as OneofField;
                    if (oneof != null)
                    {
                        if (oneof.Type.Name == inner_type.Name)
                        {
                            skip = true;
                        }
                    }
                }
                if (skip) continue;
                var od = parseType(inner_type);
                if (inner_type.IsEnum)
                {
                    cd.Enums.Add(inner_type.Name, od as EnumDescription);
                }
                else
                {
                    cd.Classes.Add(inner_type.Name, od as ClassDescription);
                }
            }
            if (fields != null)
            {
                foreach (var kvp in fields)
                {
                    if (cd.Fields.ContainsKey(kvp.Name)) continue;
                    cd.Fields.Add(kvp.Name, kvp);
                }

                //Console.WriteLine("Processed class " + type.FullName);
            }
            return cd;
        }

        public void parseAll()
        {
            foreach (TypeDefinition type in this.assembly.MainModule.Types)
            {
                if (IsProtobufPacket(type))
                {
                    if (!IsParsed(type))
                    {
                        var protobufType = parseType(type);
                        if (protobufType != null)
                        {
                            items.Add(type.Name, protobufType);
                        }
                    }

                    foreach (var od in items.Values.ToList())
                    {
                        var cd = od as ClassDescription;

                        if (cd != null)
                        {
                            var e_types = cd.GetExternalTypes();

                            foreach (var e_type in e_types)
                            {
                                var ed_type = e_type.Resolve();
                                if (!IsParsed(ed_type))
                                {
                                    var protobufType = parseType(ed_type);
                                    if (protobufType != null)
                                    {
                                        items.Add(ed_type.Name, protobufType);
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        protected void DumpItem(StreamWriter w, ObjectDescription item)
        {
            var lines = item.ToPBLines();

            var c = item as ClassDescription;

            if (c != null)
            {
                // Imports
                foreach (var t in c.GetExternalTypes())
                {
                    var cut = t.FullName.CutAfterPlusSlashAndDot();

                    if (cut.Length == 0)
                    {
                        throw new Exception("PooPee: " + t);
                    }

                    w.WriteLine("import \"{0}.proto\";", cut);
                }
                w.WriteLine();
            }

            foreach (var line in lines)
            {
                w.WriteLine(line);
            }
        }

        public async void DumpToDirectory(string directory)
        {
            //make a folder for protos
            var protoDirectory = Directory.CreateDirectory(Path.Combine(directory, "protos"));
            foreach (var item in this.items)
            {
                var filename = Path.Combine(protoDirectory.FullName, item.Key.CutAfterPlusSlashAndDot() + ".proto");

                var w = new StreamWriter(filename);

                w.WriteLine("syntax = \"proto3\";");

                DumpItem(w, item.Value);

                w.Close();
            }

            var nameTranslationWriter = new StreamWriter(Path.Combine(directory, "nameTranslation.txt"));
            for (var i = 0; i < this.nametranslations.Count; i++)
            {
                var nameTranslation = this.nametranslations.ElementAt(i);
                nameTranslationWriter.WriteLine(nameTranslation.Key + "⇨" + nameTranslation.Value);
            }
            nameTranslationWriter.Close();

            Dictionary<int, string> cmdids = new Dictionary<int, string>();
            foreach (var keyPairValue in this.cmdidsMapper.Values)
            {
                cmdids.Add(keyPairValue.Key, keyPairValue.Value);
            }

            using FileStream createStream = File.Create(Path.Combine(directory, "cmdids.json"));
            JsonSerializer.Serialize(createStream, cmdids);
        }
    }

    public class Il2cppFunctionAddressData
    {
        public string RVA;
        public string VA;
        public string Offset;

        public Il2cppFunctionAddressData(string RVA, string Offset, string VA)
        {
            this.RVA = RVA;
            this.VA = VA;
            this.Offset = Offset;
        }
    }

    public class FunctionAddresses
    {
        public Il2cppFunctionAddressData writeToAddress;
        public Il2cppFunctionAddressData ctorAddress;
        public Il2cppFunctionAddressData cmdIdAddress;
        public Il2cppFunctionAddressData mergeFromAddress;

        public FunctionAddresses(Il2cppFunctionAddressData writeToAddress, Il2cppFunctionAddressData ctorAddress, Il2cppFunctionAddressData cmdIdAddress, Il2cppFunctionAddressData mergeFromAddress)
        {
            this.writeToAddress = writeToAddress;
            this.ctorAddress = ctorAddress;
            this.cmdIdAddress = cmdIdAddress;
            this.mergeFromAddress = mergeFromAddress;
        }
    }
}
