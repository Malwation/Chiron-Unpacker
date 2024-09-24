using CommandLine;

namespace Chiron_Unpacker
{
    public class Options
    {
        [Option('f', "file", Required = true, HelpText = "TODO")]
        public string File { get; set; }

        [Option('o', "output", Required = true, HelpText = "")]
        public string Output { get; set; }
    }
}
