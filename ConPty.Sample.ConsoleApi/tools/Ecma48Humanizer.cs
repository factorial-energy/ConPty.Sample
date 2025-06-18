using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

public static class AnsiHumanizer
{
    // Match ANSI CSI sequences (e.g., \x1b[31m, \x1b[2J, \x1b[1A)
    private static readonly Regex CsiRegex = new Regex(@"\x1B\[(.*?)[@-~]", RegexOptions.Compiled);

    // Match OSC sequences (e.g., \x1b]0;title\x07)
    private static readonly Regex OscRegex = new Regex(@"\x1B\](.*?)\x07", RegexOptions.Compiled);

    // Human-readable replacements for common SGR codes
    private static readonly Dictionary<int, string> SgrMap = new()
    {
        [0] = "<reset>",
        [1] = "<bold>",
        [3] = "<italic>",
        [4] = "<underline>",
        [7] = "<inverse>",
        [30] = "<fg:black>",
        [31] = "<fg:red>",
        [32] = "<fg:green>",
        [33] = "<fg:yellow>",
        [34] = "<fg:blue>",
        [35] = "<fg:magenta>",
        [36] = "<fg:cyan>",
        [37] = "<fg:white>",
        [90] = "<fg:bright-black>",
        [91] = "<fg:bright-red>",
        [92] = "<fg:bright-green>",
        [93] = "<fg:bright-yellow>",
        [94] = "<fg:bright-blue>",
        [95] = "<fg:bright-magenta>",
        [96] = "<fg:bright-cyan>",
        [97] = "<fg:bright-white>",
        [40] = "<bg:black>",
        [41] = "<bg:red>",
        [42] = "<bg:green>",
        [43] = "<bg:yellow>",
        [44] = "<bg:blue>",
        [45] = "<bg:magenta>",
        [46] = "<bg:cyan>",
        [47] = "<bg:white>",
        [100] = "<bg:bright-black>",
        [101] = "<bg:bright-red>",
        [102] = "<bg:bright-green>",
        [103] = "<bg:bright-yellow>",
        [104] = "<bg:bright-blue>",
        [105] = "<bg:bright-magenta>",
        [106] = "<bg:bright-cyan>",
        [107] = "<bg:bright-white>"
    };

    // Human-readable replacements for some cursor movements and screen control
    private static readonly Dictionary<char, string> CsiCommandMap = new()
    {
        ['A'] = "<up>",
        ['B'] = "<down>",
        ['C'] = "<right>",
        ['D'] = "<left>",
        ['H'] = "<home>",
        ['J'] = "<erase-display>",
        ['K'] = "<erase-line>"
    };

    public static string Humanize(string input)
    {
        // Replace OSC sequences first
        input = OscRegex.Replace(input, m => $"<OSC:{m.Groups[1].Value}>");

        // Replace CSI sequences
        input = CsiRegex.Replace(input, match =>
        {
            string content = match.Groups[1].Value;
            char command = match.Value[^1];

            if (command == 'm') // SGR
            {
                var parts = content.Split(';');
                List<string> result = new();
                foreach (var part in parts)
                {
                    if (int.TryParse(part, out int code) && SgrMap.TryGetValue(code, out var desc))
                        result.Add(desc);
                    else
                        result.Add($"<sgr:{part}>");
                }
                return string.Join("", result);
            }

            if (CsiCommandMap.TryGetValue(command, out var name))
            {
                return name;
            }

            return $"<ESC[{content}{command}>";
        });

        return input;
    }
}
