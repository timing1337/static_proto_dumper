using Iced.Intel;
using Mono.Cecil;

namespace ProtoLurker
{
    internal class InstructionParser
    {
        private AssemblyLoader AssemblyLoader;

        public Dictionary<TypeDefinition, List<FieldDescription>> fieldInfo = new Dictionary<TypeDefinition, List<FieldDescription>>();

        public Dictionary<TypeDefinition, Dictionary<int, FieldDefinition>> codecs = new Dictionary<TypeDefinition, Dictionary<int, FieldDefinition>>();

        Dictionary<string, ObjectDescription> items = new Dictionary<string, ObjectDescription>();

        static Dictionary<string, string> hardcodedName = new Dictionary<string, string>()
        {
            //Math
            { "Rotation", "Vector" },
            { "RotationSpeed", "MathQuaternion" },
            { "AxisSpeed", "MathQuaternion" },
            { "CubicSize", "Vector" },
            { "CylinderSize", "CylinderRegionSize" },
            { "PolygonSize", "PolygonRegionSize" },

            //Dungeon/Event Stuff
            { "SettleInfo", "RoguelikeDungeonSettleInfo" },

            //Camera Stuff
            { "LocationInfo", "WidgetCreateLocationInfo" },
            { "CameraInfo", "WidgetCameraInfo" },
            { "CreatorInfo", "WidgetCreatorInfo" },
            { "ThunderBirdFeatherInfo", "WidgetThunderBirdFeatherInfo" },
            { "SorushInfo", "WidgetSorushInfo" },

            //SceneGadgetInfo
            { "TrifleItem", "Item" },
            { "GatherGadget", "GatherGadgetInfo" },
            { "Worktop", "WorktopInfo" },
            { "ClientGadget", "ClientGadgetInfo" },
            { "Weather", "WeatherInfo" },
            { "AbilityGadget", "AbilityGadgetInfo" },
            { "StatueGadget", "StatueGadgetInfo" },
            { "BossChest", "BossChestInfo" },
            { "BlossomChest", "BlossomChestInfo" },
            { "MpPlayReward", "MpPlayRewardInfo" },
            { "GeneralReward", "GadgetGeneralRewardInfo" },

            //QueryCurRegion
            { "ForceUdpate", "ForceUpdateInfo" },
            { "StopServer", "StopServerInfo" },

            //MaterialDeleteInfo
            { "DateDelete", "DateTimeDelete" },

            // nah i aint documenting after this
            { "FishInfo", "SceneFishInfo" },

            { "CrucibleInfo", "GadgetCrucibleInfo" },

            { "Avatar", "SceneAvatarInfo" },
            { "Monster", "SceneMonsterInfo" },
            { "Npc", "SceneNpcInfo" },

            //Problem with CreateEntityInfo & SceneEntityInfo
            { "Gadget", "Ignore" },

            { "WaterInfo", "MassiveWaterInfo" },
            { "GrassInfo", "MassiveGrassInfo" },
            { "BoxInfo", "MassiveBoxInfo" },

            { "NpcData", "HomeMarkPointNPCData" },
            { "SuiteData", "HomeMarkPointSuiteData" },

            { "SyncCreateConnect", "BreakoutSyncCreateConnect" },
            { "SyncPing", "BreakoutSyncPing" },
            { "SyncFinishGame", "BreakoutSyncFinishGame" },
            { "SyncSnapShot", "BreakoutSyncSnapShot" },
            { "SyncAction", "BreakoutSyncAction" },

            //{ "BalloonInfo", "FleurFairBalloonInfo" },
            //{ "FallInfo", "FleurFairFallInfo" },

            { "BalloonInfo", "Ignore" },
            { "FallInfo", "Ignore" },

            { "MusicGameInfo", "FleurFairMusicGameInfo" },


            { "BundleInfo", "SalvageBundleChallengeInfo" },
            { "ScoreChallengeInfo", "SalvageScoreChallengeInfo" },

    };
        private byte[] userAssembly;
        private ByteArrayCodeReader codeReader;

        public InstructionParser(AssemblyLoader assemblyLoader, string filePath)
        {
            this.userAssembly = File.ReadAllBytes(filePath);
            AssemblyLoader = assemblyLoader;
            this.codeReader = new ByteArrayCodeReader(this.userAssembly);
        }

        public List<FieldDefinition> GetFields(TypeDefinition definition)
        {
            return definition.Fields.Where(f => !f.IsStatic && !f.HasConstant).ToList();
        }

        public FieldDefinition? findFieldFromOffset(TypeDefinition type, uint offset)
        {
            var field = this.GetFields(type).Find(field => field.CustomAttributes[0].Fields[0].Name == "Offset" && Convert.ToInt32(field.CustomAttributes[0].Fields[0].Argument.Value.ToString(), 16) == offset);
            if (field == null) return null;
            return field;
        }

        public PropertyDefinition? findPropertyFromOffset(TypeDefinition type, uint offset)
        {
            var field = findFieldFromOffset(type, offset);
            var property = this.AssemblyLoader.fieldToProperty.GetValueOrDefault(field);
            if (property == null) return null;
            return property;
        }

        public List<Instruction> GetInstructions(Il2cppFunctionAddressData address)
        {
            this.codeReader.Position = Convert.ToInt32(address.Offset, 16);
            var decoder = Iced.Intel.Decoder.Create(IntPtr.Size * 8, codeReader);
            decoder.IP = Convert.ToUInt64(address.VA, 16);
            var instructions = new List<Instruction>();
            while (true)
            {
                var instruction = decoder.Decode();
                instructions.Add(instruction);
                if (instruction.Mnemonic == Mnemonic.Ret)
                {
                    break;
                }
            }
            return instructions;
        }

        public void parseFieldToProperty(TypeDefinition type)
        {
            foreach (PropertyDefinition property in type.Properties)
            {
                if (this.AssemblyLoader.fieldToProperty.ContainsValue(property)) continue;
                if (!property.HasThis || property.GetMethod.IsVirtual)
                {
                    continue;
                }

                var fileOffset = type.Methods.ToList().Find(method => method.Name == property.GetMethod.Name);

                this.codeReader.Position = Convert.ToInt32(fileOffset.CustomAttributes[0].Fields[1].Argument.Value.ToString(), 16);
                var decoder = Iced.Intel.Decoder.Create(IntPtr.Size * 8, codeReader);
                var instructions = new List<Instruction>();
                var formatter = new NasmFormatter();
                formatter.Options.DigitSeparator = "`";
                formatter.Options.FirstOperandCharIndex = 10;
                var output = new StringOutput();
                while (true)
                {
                    var instruction = decoder.Decode();
                    if (instruction.MemoryDisplacement32 != 0)
                    {
                        var protoField = this.GetFields(type).Find(field => field.CustomAttributes[0].Fields[0].Name == "Offset" && Convert.ToInt32(field.CustomAttributes[0].Fields[0].Argument.Value.ToString(), 16) == instruction.MemoryDisplacement32);
                        if (protoField != null)
                        {
                            if (this.AssemblyLoader.fieldToProperty.ContainsKey(protoField))
                            {
                                break;
                            }
                            this.AssemblyLoader.fieldToProperty.Add(protoField, property);
                            break;
                        }
                    }
                    if (instruction.Mnemonic == Mnemonic.Ret)
                    {
                        break;
                    }
                }
            }
        }

        public void parseWriteTo(TypeDefinition type, Il2cppFunctionAddressData writeToAddress, Il2cppFunctionAddressData mergeFromAddress)
        {
            var properties = type.Properties.Where(p => p.HasThis && !p.GetMethod.IsVirtual).ToList();
            var index = 0;

            var formatter = new NasmFormatter();
            var output = new StringOutput();

            var hasOneof = false;
            var instructions = GetInstructions(writeToAddress);

            if (instructions.Count < 2)
            {
                Console.WriteLine("Empty write to: " + type.Name + " moving to mergefrom :yawn:");
                this.parseMergeFrom(type, mergeFromAddress);
                return;
            }

            if (instructions[0].Mnemonic == Mnemonic.Jmp)
            {
                return;
            }

            for (var i = 0; i < instructions.Count; i++)
            {
                var fieldStructInstruction = instructions[i];
                if (fieldStructInstruction.MemoryBase == Register.R8 && fieldStructInstruction.Op0Register == Register.R8)
                {
                    var codecFields = this.codecs.GetValueOrDefault(type).ToList().Where(f => f.Value.CustomAttributes[0].Fields[0].Name == "Offset" && Convert.ToInt32(f.Value.CustomAttributes[0].Fields[0].Argument.Value.ToString(), 16) == fieldStructInstruction.MemoryDisplacement32);
                    if (codecFields.Count() < 1) continue;
                    var codecField = codecFields.First();
                    var structBrute = 3;
                    while (structBrute < 18)
                    {
                        var actualFieldStruct = instructions[i - structBrute];
                        if (actualFieldStruct.Mnemonic == Mnemonic.Mov && (actualFieldStruct.MemoryBase == Register.RDI || actualFieldStruct.MemoryBase == Register.RBX) && actualFieldStruct.MemoryDisplacement32 != 0)
                        {
                            formatter.Format(actualFieldStruct, output);
                            var field = findFieldFromOffset(type, actualFieldStruct.MemoryDisplacement32);
                            var property = findPropertyFromOffset(type, actualFieldStruct.MemoryDisplacement32);
                            if (field != null)
                            {
                                var protoField = new RegularField(field, property, codecField.Key, WireFormat.GetTagWireType(codecField.Key << 3));
                                this.fieldInfo.GetValueOrDefault(type).Add(protoField);
                                break;
                            }
                        }
                        structBrute++;
                    }
                    continue;
                }
                if (fieldStructInstruction.Mnemonic == Mnemonic.Jmp)
                {
                    var scanUntilR8Index = 0;
                    var codecIndex = -1;
                    while (scanUntilR8Index < 6)
                    {
                        var scanUntilR8IndexStruct = instructions[i - scanUntilR8Index];
                        if (scanUntilR8IndexStruct.Op0Register == Register.R8)
                        {
                            codecIndex = (int)scanUntilR8IndexStruct.MemoryDisplacement32;
                            break;
                        }
                        scanUntilR8Index++;
                    }
                    if (codecIndex == -1) continue;
                    var codecField = this.codecs.GetValueOrDefault(type).ToList().Where(f => f.Value.CustomAttributes[0].Fields[0].Name == "Offset" && Convert.ToInt32(f.Value.CustomAttributes[0].Fields[0].Argument.Value.ToString(), 16) == codecIndex).FirstOrDefault();
                    var structBrute = 3;
                    while (structBrute < 18)
                    {
                        var actualFieldStruct = instructions[i - structBrute];
                        if (actualFieldStruct.Mnemonic == Mnemonic.Mov && actualFieldStruct.MemoryBase == Register.RBX)
                        {
                            var field = findFieldFromOffset(type, actualFieldStruct.MemoryDisplacement32);
                            var property = findPropertyFromOffset(type, actualFieldStruct.MemoryDisplacement32);
                            if (field != null)
                            {
                                var protoField = new RegularField(field, property, codecField.Key, WireFormat.GetTagWireType(codecField.Key << 3));
                                this.fieldInfo.GetValueOrDefault(type).Add(protoField);
                                break;
                            }
                        }
                        structBrute++;
                    }
                    continue;
                }

                //Special case, as its the only one that happens here, i dont want to mess up every thing
                if (instructions[i].Op0Register == Register.R8 && instructions[i + 1].Mnemonic == Mnemonic.Call)
                {
                    var codecField = this.codecs.GetValueOrDefault(type).ToList().Where(f => f.Value.CustomAttributes[0].Fields[0].Name == "Offset" && Convert.ToInt32(f.Value.CustomAttributes[0].Fields[0].Argument.Value.ToString(), 16) == instructions[i].MemoryDisplacement32).FirstOrDefault();
                    var structBrute = 3;
                    while (structBrute < 18)
                    {
                        var actualFieldStruct = instructions[i - structBrute];
                        if (actualFieldStruct.Mnemonic == Mnemonic.Mov && actualFieldStruct.MemoryBase == Register.RDI)
                        {
                            var field = findFieldFromOffset(type, actualFieldStruct.MemoryDisplacement32);
                            var property = findPropertyFromOffset(type, actualFieldStruct.MemoryDisplacement32);
                            if (field != null)
                            {
                                var protoField = new RegularField(field, property, codecField.Key, WireFormat.GetTagWireType(codecField.Key << 3));
                                this.fieldInfo.GetValueOrDefault(type).Add(protoField);
                                break;
                            }
                        }
                        structBrute++;
                    }
                }

                if (fieldStructInstruction.Mnemonic == Mnemonic.Cmp ||
                    fieldStructInstruction.Mnemonic == Mnemonic.Movss ||
                    fieldStructInstruction.Mnemonic == Mnemonic.Movsd ||
                    (fieldStructInstruction.Mnemonic == Mnemonic.Mov && fieldStructInstruction.MemoryDisplacement32 != 0 && (fieldStructInstruction.Op0Register == Register.RAX || fieldStructInstruction.Op0Register == Register.RCX)))
                {
                    var structOffset = fieldStructInstruction.MemoryDisplacement32;
                    //Check if codec
                    //Hack for string
                    if (fieldStructInstruction.MemoryBase == Register.RAX)
                    {
                        //Brute till you find op0 == rax && memorybase as rbx
                        var stringBrute = 0;
                        while (i - stringBrute > 0)
                        {
                            var stringBruteInstruction = instructions[i - stringBrute];
                            if (stringBruteInstruction.Op0Register == Register.RAX && stringBruteInstruction.MemoryBase == Register.RDI)
                            {
                                structOffset = stringBruteInstruction.MemoryDisplacement32;
                                break;
                            }
                            stringBrute++;
                        }
                    }
                    var field = findFieldFromOffset(type, structOffset);

                    if (field != null)
                    {
                        var property = findPropertyFromOffset(type, structOffset);
                        var fieldId = 0;
                        var fieldIdBrute = 4; //skip the first 3 instructions
                        while (fieldIdBrute < 13)
                        {
                            if (i + fieldIdBrute >= instructions.Count)
                            {
                                break;
                            }
                            var fieldIdInstruction = instructions[i + fieldIdBrute];
                            if (fieldIdInstruction.Mnemonic == Mnemonic.Call) break;
                            if (fieldIdInstruction.Mnemonic == Mnemonic.Mov && fieldIdInstruction.Op0Register == Register.DL)
                            {
                                fieldId = (int)fieldIdInstruction.Immediate32;
                                if (fieldId != 0)
                                {
                                    var secondFieldByte = instructions.ElementAt(i + fieldIdBrute - 1);
                                    if (secondFieldByte.Mnemonic == Mnemonic.Mov)
                                    {
                                        fieldId = (int)Reader.readVarInt(new byte[2] { Convert.ToByte(fieldIdInstruction.Immediate32), Convert.ToByte(secondFieldByte.Immediate32) });
                                    }
                                    break;
                                }
                            }
                            fieldIdBrute++;
                        }
                        var wireTag = WireFormat.GetTagWireType(fieldId);
                        fieldId = WireFormat.GetTagFieldNumber(fieldId);
                        if (fieldId == 0) continue;
                        if (fieldStructInstruction.Immediate16 != 0)
                        {
                            if (!hasOneof) hasOneof = true;
                            OneofField oneof = (OneofField)this.fieldInfo.GetValueOrDefault(type).Find(f => f.Name == field.Name);
                            if (oneof == null)
                            {
                                oneof = new OneofField(field);
                                this.fieldInfo.GetValueOrDefault(type).Add(oneof);
                            }
                            var oneofFieldIdList = field.FieldType.Resolve().Fields.Where(f => f.Name != "value__" && f.Name != "None").ToList();
                            var unobfuscatedName = oneofFieldIdList.Find(f =>
                            {
                                return (int)f.Constant == fieldId;
                            });

                            if (unobfuscatedName == null) break;
                            var oneofIndex = properties.FindIndex(p => p.Name == property.Name) + oneofFieldIdList.IndexOf(unobfuscatedName);
                            var oneofProp = properties.ElementAt(oneofIndex);
                            if (!oneof.Fields.ContainsValue(new RegularField(field, oneofProp, fieldId, wireTag)))
                            {
                                addNametranslation(oneofProp.Name, Extension.ToSnakeCase(unobfuscatedName.Name));
                                oneof.AddRecord(field, oneofProp, fieldId, wireTag);
                                if (!FieldDescription.pbTypeNames.ContainsKey(oneofProp.PropertyType.FullName))
                                {
                                    var className = unobfuscatedName.Name;
                                    if (hardcodedName.ContainsKey(unobfuscatedName.Name))
                                    {
                                        className = hardcodedName.GetValueOrDefault(unobfuscatedName.Name);
                                        if (className != "Ignore")
                                        {
                                            addNametranslation(oneofProp.PropertyType.Name, className);
                                        }
                                    }
                                }
                            }
                        }
                        else
                        {
                            this.fieldInfo.GetValueOrDefault(type).Add(new RegularField(field, property, fieldId, wireTag));
                        }
                    }
                    continue;
                }

                //Some of them use getter/setters instead of direct field calling
                if (fieldStructInstruction.Mnemonic == Mnemonic.Call && !hasOneof) //Oneof check smh
                {
                    var property = this.getPropertyBasedOnVaFunctionByGetter(type, fieldStructInstruction.MemoryDisplacement64);
                    if (property != null)
                    {
                        var fieldIdBruteCount = 0;
                        var fieldId = 0;
                        while (fieldIdBruteCount < 7)
                        {
                            var fieldIdBruteStruct = instructions[i - fieldIdBruteCount];
                            if (fieldIdBruteStruct.Mnemonic == Mnemonic.Mov && fieldIdBruteStruct.Op0Register == Register.DL)
                            {
                                fieldId = (int)fieldIdBruteStruct.Immediate32;
                                break;
                            }
                            fieldIdBruteCount++;
                        }
                        var wireTag = WireFormat.GetTagWireType(fieldId);
                        fieldId = WireFormat.GetTagFieldNumber(fieldId);
                        if (fieldId == 0) continue;
                        this.fieldInfo.GetValueOrDefault(type).Add(new RegularField(this.AssemblyLoader.propertyToField.GetValueOrDefault(property), property, fieldId, wireTag));
                    }
                    continue;
                }
            }
            if (GetFields(type).Count > this.fieldInfo.GetValueOrDefault(type).Count && !hasOneof)
            {
                Console.WriteLine(type.Name + " is missing fields!!! check it out");
                Console.WriteLine("Expected: " + GetFields(type).Count + " | Actual: " + this.fieldInfo.GetValueOrDefault(type).Count);
            }
        }

        public void addNametranslation(string obfuscatedName, string deobfuscatedName)
        {
            if (AssemblyLoader.nametranslations.ContainsKey(obfuscatedName))
            {
                if (AssemblyLoader.nametranslations.GetValueOrDefault(obfuscatedName) != deobfuscatedName)
                {
                    Console.WriteLine("Inconsistent nametranslation name, key: " + obfuscatedName + " | nametranslation " + AssemblyLoader.nametranslations.GetValueOrDefault(obfuscatedName) + " nametranslation 2 " + deobfuscatedName);
                }
            }
            else
            {
                AssemblyLoader.nametranslations.Add(obfuscatedName, deobfuscatedName);
            }
        }

        public void parseCtor(TypeDefinition type, Il2cppFunctionAddressData address)
        {
            var formatter = new NasmFormatter();
            formatter.Options.FirstOperandCharIndex = 10;
            var instructions = GetInstructions(address);
            var output = new StringOutput();

            Dictionary<int, FieldDefinition> codecs = new Dictionary<int, FieldDefinition>();

            var staticCodecs = type.Fields.Where(f => f.IsStatic && f.Name != AssemblyLoader.PROTOBUF_MESSAGE_PARSER && !f.HasConstant).ToList();

            for (var i = 0; i < instructions.Count; i++)
            {
                var fieldInstruction = instructions[i];

                if ((fieldInstruction.MemoryBase == Register.RCX || fieldInstruction.MemoryBase == Register.R8 || fieldInstruction.MemoryBase == Register.RDX))
                {
                    var field = staticCodecs.Find(field => field.CustomAttributes[0].Fields[0].Name == "Offset" && Convert.ToInt32(field.CustomAttributes[0].Fields[0].Argument.Value.ToString(), 16) == fieldInstruction.MemoryDisplacement32);
                    if (field != null)
                    {
                        var fieldIdBrute = 3; //magic number
                        while (i - fieldIdBrute > 0)
                        {
                            var fieldIdInstruction = instructions[i - fieldIdBrute];
                            var fieldId = 0;
                            if (fieldIdInstruction.Op0Register == Register.ECX || fieldIdInstruction.Op0Register == Register.R9D)
                            {
                                if (fieldIdInstruction.Immediate16 != 0 && fieldIdInstruction.Immediate16 != 8) //notice this out this may cause some issues (the = 8 one)
                                {
                                    fieldId = fieldIdInstruction.Immediate16 >> 3;
                                    if (!codecs.ContainsKey(fieldId)) codecs.Add(fieldId, field);
                                    break;
                                }
                            }

                            if (fieldIdInstruction.Mnemonic == Mnemonic.Lea)
                            {
                                if (fieldIdInstruction.MemoryDisplacement32 != 0 && fieldIdInstruction.MemoryDisplacement32 != 8) //notice this out this may cause some issues (the = 8 one)
                                {
                                    fieldId = (int)fieldIdInstruction.MemoryDisplacement32 >> 3;
                                    if (!codecs.ContainsKey(fieldId)) codecs.Add(fieldId, field);
                                    break;
                                }
                            }

                            fieldIdBrute++;
                        }
                    }
                }
            }

            if (codecs.Where(f => f.Value.IsStatic).ToList().Count != staticCodecs.Count)
            {
                Console.WriteLine(type.Name + " is missing codecs! Found " + codecs.Where(f => f.Value.IsStatic).ToList().Count + " But we need " + staticCodecs.Count);
                foreach (var codec in codecs)
                {
                    Console.WriteLine(codec.Key + " | " + codec.Value.Name);
                }
            }
            if (!this.codecs.ContainsKey(type))
            {
                this.codecs.Add(type, codecs);
            }
        }

        private uint GetToken(TypeDefinition t)
        {
            foreach (var attrib in t.CustomAttributes)
            {
                if (attrib.AttributeType.Name == "TokenAttribute")
                {
                    var token = attrib.Fields[0].Argument.Value.ToString();
                    return Convert.ToUInt32(token, 16);
                }
            }

            throw new ArgumentException();
        }

        public void parseCmdId(TypeDefinition type, Il2cppFunctionAddressData staticCmdIdAddress)
        {
            var instructions = GetInstructions(staticCmdIdAddress);
            for (var i = 0; i < instructions.Count; i++)
            {
                if (instructions[i].Mnemonic == Mnemonic.Mov && instructions[i].Op0Register == Register.EAX)
                {
                    this.AssemblyLoader.cmdidsMapper.Add((int)GetToken(type), new KeyValuePair<int, string>((int)instructions[i].Immediate32, type.Name));
                }
            }
        }

        public void parseMergeFrom(TypeDefinition type, Il2cppFunctionAddressData mergeFromAddress)
        {
            var properties = type.Properties.Where(p => p.HasThis && !p.GetMethod.IsVirtual).ToList();

            var index = 0;

            var instructions = GetInstructions(mergeFromAddress);

            if (instructions[0].Mnemonic == Mnemonic.Jmp)
            {
                return;
            }

            var formatter = new NasmFormatter();
            var output = new StringOutput();

            var hasOneof = false;

            var count = 0;

            for (var i = 0; i < instructions.Count; i++)
            {
                var fieldInstruction = instructions[i];
                if (fieldInstruction.Mnemonic == Mnemonic.Jmp)
                {
                    var scanUntilR8Index = 0;
                    var codecIndex = -1;
                    while (scanUntilR8Index < 4)
                    {
                        var scanUntilR8IndexStruct = instructions[i - scanUntilR8Index];
                        if (scanUntilR8IndexStruct.Op0Register == Register.R8)
                        {
                            codecIndex = (int)scanUntilR8IndexStruct.MemoryDisplacement32;
                            break;
                        }
                        scanUntilR8Index++;
                    }
                    if (codecIndex == -1) continue;
                    var codecField = this.codecs.GetValueOrDefault(type).ToList().Where(f => f.Value.CustomAttributes[0].Fields[0].Name == "Offset" && Convert.ToInt32(f.Value.CustomAttributes[0].Fields[0].Argument.Value.ToString(), 16) == codecIndex).FirstOrDefault();
                    var structBrute = 9;
                    while (structBrute < 18)
                    {
                        var actualFieldStruct = instructions[i - structBrute];
                        if (actualFieldStruct.Mnemonic == Mnemonic.Mov && (actualFieldStruct.MemoryBase == Register.RBX || actualFieldStruct.MemoryBase == Register.RDI))
                        {
                            var field = findFieldFromOffset(type, actualFieldStruct.MemoryDisplacement32);
                            var property = findPropertyFromOffset(type, actualFieldStruct.MemoryDisplacement32);
                            if (field != null)
                            {
                                var protoField = new RegularField(field, property, codecField.Key, WireFormat.GetTagWireType(codecField.Key << 3));
                                this.fieldInfo.GetValueOrDefault(type).Add(protoField);
                                count++;

                                break;
                            }
                        }
                        structBrute++;
                    }
                    continue;
                }

                //Just normal field

                if ((fieldInstruction.Mnemonic == Mnemonic.Mov || fieldInstruction.Mnemonic == Mnemonic.Movss || fieldInstruction.Mnemonic == Mnemonic.Movsd) && (fieldInstruction.MemoryBase == Register.RBX || fieldInstruction.MemoryBase == Register.RDI) && fieldInstruction.Op0Register == Register.None)
                {
                    var structOffset = fieldInstruction.MemoryDisplacement32;
                    var field = findFieldFromOffset(type, structOffset);

                    if (field != null)
                    {
                        var property = findPropertyFromOffset(type, structOffset);
                        //brute until we find 
                        //cmp     eax, 15696
                        //something like this
                        //most likely it will be the final one without those annoying ass if else
                        var fieldIdBruteCount = 0;
                        var fieldId = 0;
                        while (fieldIdBruteCount < 10)
                        {
                            var fieldIdBruteStruct = instructions[i - fieldIdBruteCount];
                            if (fieldIdBruteStruct.Mnemonic == Mnemonic.Cmp && fieldIdBruteStruct.Op0Register == Register.EAX)
                            {
                                fieldId = (int)fieldIdBruteStruct.Immediate32;
                                break;
                            }
                            fieldIdBruteCount++;
                        }
                        var wireTag = WireFormat.GetTagWireType(fieldId);
                        fieldId = WireFormat.GetTagFieldNumber(fieldId);
                        if (fieldId == 0) continue;
                        this.fieldInfo.GetValueOrDefault(type).Add(new RegularField(field, property, fieldId, wireTag));
                        count++;
                    }
                    continue;
                }

                //Some of them use getter/setters instead of direct field calling
                if (fieldInstruction.Mnemonic == Mnemonic.Call)
                {
                    var property = this.getPropertyBasedOnVaFunction(type, fieldInstruction.MemoryDisplacement64);
                    if (property != null)
                    {
                        var fieldIdBruteCount = 0;
                        var fieldId = 0;
                        while (fieldIdBruteCount < 10)
                        {
                            var fieldIdBruteStruct = instructions[i - fieldIdBruteCount];
                            if (fieldIdBruteStruct.Mnemonic == Mnemonic.Cmp && fieldIdBruteStruct.Op0Register == Register.EAX)
                            {
                                fieldId = (int)fieldIdBruteStruct.Immediate32;
                                break;
                            }
                            fieldIdBruteCount++;
                        }
                        var wireTag = WireFormat.GetTagWireType(fieldId);
                        fieldId = WireFormat.GetTagFieldNumber(fieldId);
                        if (fieldId == 0) continue;
                        this.fieldInfo.GetValueOrDefault(type).Add(new RegularField(this.AssemblyLoader.propertyToField.GetValueOrDefault(property), property, fieldId, wireTag));
                        count++;
                    }
                    continue;
                }

                //Special handling for oneofs

                if (fieldInstruction.Mnemonic == Mnemonic.Cmp && fieldInstruction.MemoryBase == Register.RBX && fieldInstruction.Immediate16 != 0)
                {
                    var field = this.findFieldFromOffset(type, fieldInstruction.MemoryDisplacement32);
                    if (field != null)
                    {
                        var fieldId = fieldInstruction.Immediate16;
                        var wireTag = WireFormat.GetTagWireType(fieldId << 3);
                        var property = findPropertyFromOffset(type, fieldInstruction.MemoryDisplacement32);
                        OneofField oneof = (OneofField)this.fieldInfo.GetValueOrDefault(type).Find(f => f.Name == field.Name);
                        if (oneof == null)
                        {
                            oneof = new OneofField(field);
                            this.fieldInfo.GetValueOrDefault(type).Add(oneof);
                        }
                        var oneofFieldIdList = field.FieldType.Resolve().Fields.Where(f => f.Name != "value__" && f.Name != "None").ToList();
                        var unobfuscatedName = oneofFieldIdList.Find(f =>
                        {
                            return (int)f.Constant == fieldId;
                        });

                        if (unobfuscatedName == null) break;
                        var oneofIndex = properties.FindIndex(p => p.Name == property.Name) + oneofFieldIdList.IndexOf(unobfuscatedName);
                        var oneofProp = properties.ElementAt(oneofIndex);
                        if (!oneof.Fields.ContainsValue(new RegularField(field, oneofProp, fieldId, wireTag)))
                        {
                            addNametranslation(oneofProp.Name, Extension.ToSnakeCase(unobfuscatedName.Name));
                            oneof.AddRecord(field, oneofProp, fieldId, wireTag);
                            if (!FieldDescription.pbTypeNames.ContainsKey(oneofProp.PropertyType.FullName))
                            {
                                var className = unobfuscatedName.Name;
                                if (hardcodedName.ContainsKey(unobfuscatedName.Name))
                                {
                                    className = hardcodedName.GetValueOrDefault(unobfuscatedName.Name);
                                    if (className != "Ignore")
                                    {
                                        addNametranslation(oneofProp.PropertyType.Name, className);
                                    }
                                }
                            }
                        }
                    }
                }
            }

            if (GetFields(type).Count > this.fieldInfo.GetValueOrDefault(type).Count)
            {
                Console.WriteLine(type.Name + " is missing fields!!! check it out");
            }
        }

        public PropertyDefinition getPropertyBasedOnVaFunctionByGetter(TypeDefinition type, ulong VA)
        {
            foreach (var property in type.Properties)
            {
                if (!property.HasThis || property.GetMethod.IsVirtual)
                {
                    continue;
                }
                if (property.GetMethod == null)
                {
                    continue;
                }
                var setter = type.Methods.ToList().Find(method => method.Name == property.GetMethod.Name);
                var MethodVA = Convert.ToUInt64(setter.CustomAttributes[0].Fields[2].Argument.Value.ToString(), 16);
                if (MethodVA == VA)
                {
                    return property;
                }
            }
            return null;
        }

        public PropertyDefinition getPropertyBasedOnVaFunction(TypeDefinition type, ulong VA)
        {
            foreach (var property in type.Properties)
            {
                if (!property.HasThis || property.GetMethod.IsVirtual)
                {
                    continue;
                }
                if (property.SetMethod == null)
                {
                    continue;
                }
                var setter = type.Methods.ToList().Find(method => method.Name == property.SetMethod.Name);
                var MethodVA = Convert.ToUInt64(setter.CustomAttributes[0].Fields[2].Argument.Value.ToString(), 16);
                if (MethodVA == VA)
                {
                    return property;
                }
            }
            return null;
        }

        public void parse(TypeDefinition type, FunctionAddresses addresses)
        {
            this.fieldInfo.TryAdd(type, new List<FieldDescription>());
            this.parseFieldToProperty(type);
            parseCtor(type, addresses.ctorAddress);
            parseWriteTo(type, addresses.writeToAddress, addresses.mergeFromAddress);
        }
    }
}
