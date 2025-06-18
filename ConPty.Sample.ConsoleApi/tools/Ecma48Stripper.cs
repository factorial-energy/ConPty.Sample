using System;
using System.Text.RegularExpressions;

public static class Ecma48Stripper
{
    // This regex matches:
    // - CSI (e.g., \x1b[31m, \x1b[2J)
    // - OSC (e.g., \x1b]0;title\x07)
    // - DCS, SOS, PM, APC (e.g., \x1bP...terminator)
    // - ESC + single char sequences (e.g., \x1b=, \x1b>)
    private static readonly Regex AnsiRegex = new Regex(
        @"\x1B(\[[0-?]*[ -/]*[@-~]" +        // CSI sequences
        @"|\][^\x07]*\x07" +                // OSC terminated by BEL
        @"|[\(\)][0-2AB]" +                 // Charset switching
        @"|[@-Z\\-_])",                    // Other single ESC commands
        RegexOptions.Compiled);

    public static string Strip(string input)
    {
        return AnsiRegex.Replace(input, "");
    }
}
