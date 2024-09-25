using CommandLine;

namespace Chiron_Unpacker
{
    public class Options
    {
        [Option('f', "file", Required = true, HelpText = "Select the file to unpack")]
        public string File { get; set; }

        [Option('o', "output", Required = true, HelpText = "Select the location to save the dumped files")]
        public string Output { get; set; }

        [Option('d', "deobfuscate", Required = false, Default = false, HelpText = "Just use the dump feature")]
        public bool Deobfuscate { get; set; }
    }
}
