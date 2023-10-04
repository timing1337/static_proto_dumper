using Mono.Cecil;

namespace ProtoLurker
{
    /// <summary>
    /// Description of EnumDescription.
    /// </summary>
    public class EnumDescription : ObjectDescription
    {
        public Dictionary<string, ItemDescription> Items = null;

        private TypeReference type = null;

        public override TypeReference Type
        {
            get
            {
                return type;
            }
        }

        public EnumDescription(string name/*, TypeReference enm*/) : base(name)
        {
            Items = new Dictionary<string, ItemDescription>();
            type = new TypeDefinition("sample", name, TypeAttributes.Class | TypeAttributes.Public);
        }

        public override string[] ToPBLines()
        {
            var lines = new List<string>();

            lines.Add("enum " + Name.CutAfterPlusSlashAndDot() + " {");

            // If enum has duplicates, we need to add directive
            var fuckYouMihoyo = Items.Values.Select(i => i.Value);

            var addPrefix = false;

            //Prevent duplication
            Console.WriteLine(Name);

            if (Name == Name.CutAfterPlusSlashAndDot()) addPrefix = true;

            bool has_dupes = fuckYouMihoyo.Count() != fuckYouMihoyo.Distinct().Count();

            if (has_dupes)
                lines.Add("\toption allow_alias = true;");

            foreach (var item in Items)
            {
                lines.AddRange(item.Value.ToPBLines().PadStrings("\t" + (addPrefix ? Name.CutAfterPlusSlashAndDot() + "_Enum_" : ""), ";"));
            }

            lines.Add("}");

            return lines.ToArray();
        }

        public override string[] ToPILines()
        {
            var lines = new List<string>();

            lines.Add("\"enum " + Name.CutAfterPlusSlashAndDot() + "\": {");

            // If enum has duplicates, we need to comment out them
            var added = new HashSet<int>();

            foreach (var item in Items)
            {
                lines.AddRange(item.Value.ToPILines().PadStrings(added.Contains(item.Value.Value) ? "\t#" : "\t", ","));
                added.Add(item.Value.Value);
            }

            lines.Add("},");

            return lines.ToArray();
        }
    }
}
