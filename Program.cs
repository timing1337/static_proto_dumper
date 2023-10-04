using ProtoLurker;


if (args.Length < 2)
{
    Console.WriteLine("> protolurker.exe <path to assembly-csharp> <path to userassembly>");
    return;
}

Console.WriteLine("Loading Assembly-CSharp");
var assemblyLoader = new AssemblyLoader(args[0], args[1]);
assemblyLoader.parseAll();
assemblyLoader.DumpToDirectory("E:\\protos");