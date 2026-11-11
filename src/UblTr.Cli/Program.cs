using System.Text;
using UblTr.Cli;

// Turkish characters must survive on Windows consoles that default to a legacy code page.
Console.OutputEncoding = new UTF8Encoding(false);
return CliApp.Run(args, Console.Out, Console.Error, Directory.GetCurrentDirectory());
