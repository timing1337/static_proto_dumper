namespace ProtoLurker
{
    /// <summary>
    /// Description of DescriptionWriter.
    /// </summary>
    public abstract class DescriptionWriter
    {
        protected Dictionary<string, ObjectDescription> Items = null;

        public DescriptionWriter(Dictionary<string, ObjectDescription> items)
        {
            Items = items;
        }

        public abstract void DumpToFile(string filename);
        public abstract void DumpToDirectory(string directory);

    }
}
