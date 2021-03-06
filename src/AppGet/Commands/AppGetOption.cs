using System;
using CommandLine;

namespace AppGet.Commands
{
    public abstract class AppGetOption : ICommandLineOption
    {
        [Option('v', "verbose", HelpText = "Generate more verbose output", Required = false)]
        public bool Verbose { get; set; }

        public string CommandName { get; }

        protected AppGetOption()
        {
            var verbAttr = (VerbAttribute)Attribute.GetCustomAttribute(GetType(), typeof(VerbAttribute));
            CommandName = verbAttr.Name;
        }
    }

    public interface ICommandLineOption
    {
    }
}