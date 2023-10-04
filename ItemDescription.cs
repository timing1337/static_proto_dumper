namespace ProtoLurker
{
    /// <summary>
    /// Description of ItemDescription.
    /// </summary>
    public class ItemDescription : ObjectDescription
    {
        public override Mono.Cecil.TypeReference Type
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public int Value = -1;

        public ItemDescription(string name, int value) : base(name)
        {
            Value = value;
        }

        public override string[] ToPBLines()
        {
            return new string[] { Name.ToSnakeCase().ToUpper() + " = " + Value.ToString() };
        }

        public override string[] ToPILines()
        {
            return new string[] { Value.ToString() + ": \"" + Name.ToSnakeCase().ToUpper() + "\"" };
        }
    }
}
