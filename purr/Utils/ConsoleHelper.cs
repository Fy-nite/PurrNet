using System;

namespace Fur.Utils;

public static class ConsoleHelper
{
    public static void WriteSuccess(string message)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.Write("✓ ");
        Console.ResetColor();
        Console.WriteLine(message);
    }

    public static void WriteError(string message)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.Write("✗ ");
        Console.ResetColor();
        Console.WriteLine(message);
    }

    public static void WriteWarning(string message)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.Write("⚠ ");
        Console.ResetColor();
        Console.WriteLine(message);
    }

    public static void WriteInfo(string message)
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.Write("ℹ ");
        Console.ResetColor();
        Console.WriteLine(message);
        Console.WriteLine();
    }

    public static void WriteStep(string action, string target)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.Write($"{action,12}");
        Console.ResetColor();
        Console.WriteLine($" {target}");
    }

    public static void WritePackage(string name, string? version = null)
    {
        Console.ForegroundColor = ConsoleColor.Blue;
        Console.Write(name);
        Console.ResetColor();
        if (!string.IsNullOrEmpty(version))
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Write($" v{version}");
            Console.ResetColor();
        }
    }

    public static void WriteHeader(string text)
    {
        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine();
        Console.WriteLine($"═══ {text} ═══");
        Console.ResetColor();
    }

    public static void WriteDim(string text)
    {
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.Write(text);
        Console.ResetColor();
    }

    public static void WriteDimLine(string text)
    {
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine(text);
        Console.ResetColor();
    }
}
