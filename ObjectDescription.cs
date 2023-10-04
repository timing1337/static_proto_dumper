using Mono.Cecil;

namespace ProtoLurker
{
    /// <summary>
    /// Description of ItemDescription.
    /// </summary>
    public abstract class ObjectDescription
    {
        public string Name;

        public abstract TypeReference Type
        {
            get;
        }

        public ObjectDescription(string name)
        {
            Name = name;
        }

        public abstract string[] ToPILines();

        public abstract string[] ToPBLines();
    }
}
