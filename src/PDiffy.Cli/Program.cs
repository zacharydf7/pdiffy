// UTF-8, so text such as curly quotes, accents and the "moved" arrow survive a Windows console or a redirect.
Console.OutputEncoding = System.Text.Encoding.UTF8;
return PDiffy.Cli.Commands.Run(args, Console.Out, Console.Error);
