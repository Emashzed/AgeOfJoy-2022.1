using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

public class AGEProgram
{
    private string name;
    Dictionary<int, ICommandBase> lines = new();
    int nextLineToExecute = -1;
    int lastLineNumberParsed = -1;
    BasicVars vars = new();
    public TokenConsumer tokens;

    public int LastLineNumberParsed { get { return lastLineNumberParsed; } }
    public BasicVars Vars { get { return vars; } }

    public string Name { get => name; }

    ConfigurationCommands config;
    CodeExecutionTracker tracker;

    private KeyValuePair<int, ICommandBase> getNext()
    {
        KeyValuePair<int, ICommandBase> nextLine =
                    lines.FirstOrDefault(kvp => kvp.Key >= nextLineToExecute);
        return nextLine;
    }

    public AGEProgram(string name)
    {
        this.name = name;
        tracker = new();
    }

    public void PrepareToRun(BasicVars pvars = null)
    {
        this.nextLineToExecute = -1;
        config.Gosub = new Stack<int>();
        config.LineNumber = config.JumpNextTo = config.JumpTo = 0;
        config.stop = false;
        if (pvars == null)
            vars = new();
        else
            vars = pvars;
        tracker.Reset();
    }

    public BasicValue GetVarValue(string varName)
    {
        return vars.GetValue(varName);
    }

    private int maxExecutionLinesAllowed = 10000;
    public int MaxExecutionLinesAllowed
    {
        get { return maxExecutionLinesAllowed; }
        set { maxExecutionLinesAllowed = value; }
    }

    public int ContLinesExecuted
    {
        get { return tracker.GetTotalLinesExecuted(); }
    }


    public bool runNextLine()
    {

        if (config.stop)
        {
            ConfigManager.WriteConsole($"[AGEProgram.runNextLine] {name} stopped by config.stop");
            return false;
        }

        if (maxExecutionLinesAllowed > 0 && tracker.GetTotalLinesExecuted() > maxExecutionLinesAllowed)
        {
            ConfigManager.WriteConsole($"[AGEProgram.runNextLine] {name} executed lines {ContLinesExecuted} > {maxExecutionLinesAllowed}");
            throw new Exception("program has reached the maximum execution lines available.");
        }
        
        KeyValuePair<int, ICommandBase> cmd = getNext();
        if (cmd.Key == 0)
        {
            ConfigManager.WriteConsole($"[AGEProgram.runNextLine] {name} by line # is zero (getNext())");
            return false;
        }

        // ConfigManager.WriteConsole($">> EXEC LINE #[{cmd.Key}] {cmd.Value.CmdToken}");

        if ( tracker == null)
        {
            tracker = new();
        }
        config.LineNumber = cmd.Key;
        cmd.Value.Execute(vars);
        tracker.ExecuteLine();

        if (config.stop)
        {
            ConfigManager.WriteConsole($"[AGEProgram.runNextLine] {name} stopped by config.stop after exec line");
            return false;
        }

        if (config.JumpTo != 0)
        {
            if (!lines.ContainsKey(config.JumpTo))
                throw new Exception($"Line number not found: {config.JumpTo}");

            // ConfigManager.WriteConsole($"[AGEProgram.runNextLine] jump to line = {config.JumpTo}");
            nextLineToExecute = config.JumpTo;
            config.JumpTo = 0;
        }
        else if (config.JumpNextTo != 0)
        {
            // ConfigManager.WriteConsole($"[AGEProgram.runNextLine] jump to line >= {config.JumpNextTo}");
            nextLineToExecute = config.JumpNextTo + 1;
            config.JumpNextTo = 0;
        }
        else
            nextLineToExecute = cmd.Key + 1;

        return true;
    }

    public string Log()
    {
        string str = $"PROGRAM: {Name}\n";
        str += $"Last line parsed: #{lastLineNumberParsed}\n";
        str += $"Next line to execute: > #{nextLineToExecute}\n";
        str += $"Executed lines counter: {ContLinesExecuted}\n";
        str += $"Lines per second: {tracker.GetAverageLinesPerSecond()}\n";

        str += $"VARS: ----------------\n";
        str += vars.ToString() + "\n";
        str += $"----------------\n";
        return str;
    }

    public string[] ParseLineOfCode(string codeLine)
    {
        string pattern = @"(?<Text>""[^""]*""|-?\d+(\.\d+)?|\w+|[,\(\)=/*+\-]|!=|<>|>=|<=|==|>|<|')";
        //        string pattern = @"(?<Text>""[^""]*""|\d+(\.\d+)?|\w+|[,\(\)=/*+\-]|!=|<>|>=|<=|>|<)|\s+";

        //string pattern = @"(?<Text>""[^""]*""|\d+(\.\d+)?|\w+|[,\(\)=/*+\-]|!=|<>|>=|<=|>|<|\s+)";

        //        string pattern = @"(?<Text>""[^""]*""|\d+(\.\d+)?|\w+|[,\(\)=/*+-]|!=|<>|>=|<=|>|<)";
        MatchCollection matches = Regex.Matches(codeLine, pattern);

        string[] tokens = new string[matches.Count];
        int index = 0;
        string token;
        foreach (Match match in matches)
        {
            token = match.Value.Trim();
            if (!string.IsNullOrEmpty(token))
            {
                // ConfigManager.WriteConsole($"[{match.Value}] {token.Length} chars");
                tokens[index] = token;
                index++;
            }
        }
        if (matches.Count == index)
            return tokens;

        return tokens.Take(index).ToArray<string>();
    }

    public void Parse(string filePath, ConfigurationCommands config)
    {
        Dictionary<int, ICommandBase> lines = new();
        this.config = config;

        using (StreamReader reader = new StreamReader(filePath))
        {
            string line;

            while ((line = reader.ReadLine()) != null)
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                // List<string> parsed = ParseLine(line);
                // ConfigManager.WriteConsole($">>>{string.Join('.', parsed)}");
                // continue;

                string[] parsedString = ParseLineOfCode(line);
                tokens = new(parsedString);
                ConfigManager.WriteConsole($"[basicAGEProgram.Parse] >>>>  {tokens.ToString()}");
                //continue;

                if (tokens.Remains() >= 1)
                {
                    if (int.TryParse(tokens.Token, out int lineNumber))
                    {
                        if (lineNumber <= 0)
                            throw new Exception($"Line number <= 0 is not allowed, in file: {filePath}");
                        if (lineNumber <= lastLineNumberParsed)
                            throw new Exception($"Line numbers not in sequence, in file: {filePath}");
                        // if (lines.ContainsKey(lineNumber))
                        //     throw new Exception($"Duplicated line number {lineNumber}, in file: {filePath}");

                        ICommandBase cmd = Commands.GetNew(tokens.Next(), config);

                        if (cmd == null || cmd.Type != CommandType.Type.Command)
                            throw new Exception($"Syntax error command not found: {tokens.Token} line: {lineNumber} file: {filePath}");

                        lastLineNumberParsed = lineNumber;
                        cmd.Parse(++tokens);
                        lines[lineNumber] = cmd;
                    }
                    else
                    {
                        throw new Exception($"Invalid line number format: {tokens.Token} file: {filePath}");
                    }
                }
                else
                {
                    throw new Exception($"Invalid line format: {line} file: {filePath}");
                }
            }
        }

        // Sort a dictionary by line numbers
        this.lines = lines.OrderBy(kv => kv.Key).ToDictionary(kv => kv.Key, kv => kv.Value);
    }

    public class CodeExecutionTracker
    {
        private int totalLinesExecuted;
        private readonly System.Diagnostics.Stopwatch stopwatch;
        private float averageLinesPerSecond;

        public CodeExecutionTracker()
        {
            totalLinesExecuted = 0;
            stopwatch = new System.Diagnostics.Stopwatch();
            stopwatch.Start();
        }

        public void ExecuteLine()
        {
            // Increment the count of total lines executed
            totalLinesExecuted++;

            // Update the average lines per second
            float elapsedTime = stopwatch.ElapsedMilliseconds / 1000.0f; // convert milliseconds to seconds
            if (elapsedTime > 0)  // Ensure no division by zero
            {
                averageLinesPerSecond = totalLinesExecuted / elapsedTime;
            }
        }

        public float GetAverageLinesPerSecond()
        {
            return averageLinesPerSecond;
        }

        public int GetTotalLinesExecuted()
        {
            return totalLinesExecuted;
        }

        public void Reset()
        {
            stopwatch.Reset();
            stopwatch.Start();
            totalLinesExecuted = 0;
        }
    }


}