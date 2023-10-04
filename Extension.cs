using Mono.Cecil;
using System.Text;

namespace ProtoLurker
{
    /// <summary>
    /// Description of Extension.
    /// </summary>
    public static class Extension
    {
        public static uint GetConstant(this FieldDefinition f)
        {
            return UInt32.Parse(f.Constant.ToString());
        }

        public static bool IsBeeObfuscated(this string name)
        {
            // TODO: very simple but should work
            return name.All(char.IsUpper) && (name.Length >= 10 && name.Length <= 15);
        }

        public static string CutAfterPlusSlashAndDot(this string name)
        {
            int plus_pos = name.LastIndexOf('+');

            if (plus_pos > -1)
                name = name.Substring(plus_pos + 1);

            int dot_pos = name.LastIndexOf('.');

            if (dot_pos > -1)
                name = name.Substring(dot_pos + 1);

            int slash_pos = name.LastIndexOf('/');

            if (slash_pos > -1)
                name = name.Substring(slash_pos + 1);

            return name;
        }

        public static string[] PadStrings(this string[] lines, string left_pad = "\t", string right_pad = "")
        {
            var ret = new List<string>();

            foreach (var line in lines)
                ret.Add(left_pad + line + right_pad);

            return ret.ToArray();
        }

        // Stolen from EF Core
        public static string ToSnakeCase(this string text)
        {
            if (text == null)
            {
                throw new ArgumentNullException(nameof(text));
            }
            if (text.Length < 2)
            {
                return text;
            }
            var sb = new StringBuilder();
            sb.Append(char.ToLowerInvariant(text[0]));
            for (int i = 1; i < text.Length; ++i)
            {
                char c = text[i];
                if (char.IsUpper(c))
                {
                    sb.Append('_');
                    sb.Append(char.ToLowerInvariant(c));
                }
                else
                {
                    sb.Append(c);
                }
            }
            return sb.ToString();
        }

        public static bool IsIEnumerableOfT(TypeDefinition type)
        {
            return type.Name != FieldDescription.PROTOBUF_BYTESTRING && type.HasInterfaces && type.Interfaces.Where(interfac => interfac.InterfaceType.Name.Contains("IEnumerable")).Count() > 0;
        }
    }
}
