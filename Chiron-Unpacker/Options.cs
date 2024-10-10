using CommandLine;

namespace Chiron_Unpacker
{
    public class Options
    {
        [Option('f', "file", Required = true, HelpText = "Select the file to unpack")]
        public string File { get; set; }

        [Option('o', "output", Required = true, HelpText = "Select the location to save the dumped files")]
        public string Output { get; set; }

        [Option('r', "resource", Required = false, Default = false, HelpText = "Use ResourceUnpack feature")]
        public bool Resource { get; set; }
    }
}
