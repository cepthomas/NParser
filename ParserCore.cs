using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text;


namespace NParser
{
    public class ParserCore
    {
        #region Enums
        /// <summary>To capture or not.</summary>
        public enum ParseType { Skip, Capture }
        #endregion

        #region Properties
        /// <summary>Source file tracking.</summary>
        public int Column { get; private set; } = 0;

        /// <summary>Source file tracking.</summary>
        public int Line { get; private set; } = 1;

        /// <summary>Source file tracking.</summary>
        public int TabWidth { get; set; } = 4;

        /// <summary>Current character being processed.</summary>
        public char Current { get; private set; }

        /// <summary>Current captured.</summary>
        public StringBuilder CaptureBuffer { get; } = new StringBuilder();

        /// <summary>Parse erros collected.</summary>
        public List<string> Errors { get; set; } = new List<string>();
        #endregion

        #region Fields
        /// <summary>Raw file input.</summary>
        TextReader _reader = null;

        /// <summary></summary>
        protected int _inIndex = 0;
        #endregion

        #region Constants
        /// <summary></summary>
        public const string HEX_DIGITS = "0123456789ABCDEFabcdef";

        /// <summary>Win uses "\r\n", nx uses "\n". This guarantees eol.</summary>
        public const string LINE_END = "\n";
        #endregion

        #region Lifecycle
        /// <summary>
        /// Main constructor.
        /// </summary>
        /// <param name="reader"></param>
        /// <returns></returns>
        public ParserCore(TextReader reader)
        {
            _reader = reader;
            _inIndex = 0;

            Column = 0;
            Line = 1;
        }
        #endregion

        /// <summary>
        /// Derived classes must implement.
        /// </summary>
        /// <returns></returns>
        public virtual object Parse()
        {
            return null;
        }

        /// <summary>
        /// Processes the input queue one char at a time. Does internal boilerplate stuff.
        /// </summary>
        protected void Advance([CallerMemberName] string caller = null)
        {
            //Debug.WriteLine($"Advance {caller}");
            _inIndex++;

            int i = _reader.Read();
            Current = (char)i;

            // Update position.
            switch (i)
            {
                case -1:
                    throw new ParseException("Finished")
                    {
                        ExceptionType = ParseException.ParseExceptionType.Done
                    };

                case '\n':
                    Line++;
                    Column = 0;
                    break;

                case '\r':
                    Column = 0;
                    break;

                case '\t':
                    if (TabWidth > 0)
                    {
                        Column = ((Column / TabWidth) + 1) * TabWidth;
                    }
                    break;

                default:
                    Column++;
                    break;
            }
        }

        /// <summary>
        /// Reset capture buffer.
        /// </summary>
        protected void ClearCapture()
        {
            CaptureBuffer.Clear();
        }

        /// <summary>
        /// Add the Current value to the capture buffer.
        /// </summary>
        protected void CaptureCurrent()
        {
            CaptureBuffer.Append(Current);
        }

        /// <summary>
        /// Add the value to the capture buffer.
        /// </summary>
        protected void Capture(char ch)
        {
            CaptureBuffer.Append(ch);
        }

        /// <summary>
        /// Peekaboo one ahead.
        /// </summary>
        /// <returns></returns>
        protected int Peek()
        {
            return _reader.Peek();
        }

        /// <summary>
        /// Test for Current matching one of the chars in the expecting string.
        /// Throws and exception if not a match.
        /// </summary>
        /// <param name="expecting">Match values.</param>
        protected void Expecting(string expecting)
        {
            if (!expecting.Contains(Current))
            {
                throw new ParseException("I certainly wasn't expecting this")
                {
                    ExceptionType = ParseException.ParseExceptionType.FailedExpectation,
                    Line = Line,
                    Column = Column
                };
            }
        }

        /// <summary>
        /// Captures up to but not including one of the values in the chars string.
        /// Current is left after the found char position.
        /// </summary>
        /// <param name="chars">Chars to match.</param>
        /// <param name="pt">Capture or skip option.</param>
        /// <returns>true if any found</returns>
        protected bool ConsumeUntilChar(string chars, ParseType pt = ParseType.Capture)
        {
            bool any = false;
            ClearCapture();

            while (!chars.Contains(Current))
            {
                if(pt == ParseType.Capture)
                {
                    CaptureCurrent();
                }
                Advance();
            }

            // Should be found now.
            any = true;
            Advance(); // skip found char

            return any;
        }

        /// <summary>
        /// Captures up to but not including the string.
        /// Current is left after the found string position.
        /// </summary>
        /// <param name="str">String to match.</param>
        /// <param name="pt">Capture or skip option.</param>
        /// <returns>true if any found</returns>
        protected bool ConsumeUntilString(string str, ParseType pt = ParseType.Capture)
        {
            bool any = false;
            ClearCapture();

            List<char> cache = new List<char>();

            // Find match with beginning of string.
            while(!any)
            {
                cache.Add(Current);

                if (Current == str[0])
                {
                    bool foundBeginning = true; // default
                    for (int i = 1; i < str.Length && foundBeginning; i++)
                    {
                        Advance();
                        cache.Add(Current);
                        foundBeginning = Current == str[i];
                    }

                    if(foundBeginning)
                    {
                        if(pt == ParseType.Capture)
                        {
                            for (int i = 0; i < cache.Count - str.Length; i++)
                            {
                                Capture(cache[i]);
                            }
                        }

                        any = true;
                    }
                }

                Advance();
            }

            return any;
        }

        /// <summary>
        /// Captures a quoted string including escaped values.
        /// Current is left on value immediately after the string.
        /// </summary>
        /// <param name="inclQuotes">Option to keep or remove leading and trailing quotes.</param>
        /// <returns>true if string found</returns>
        protected bool ConsumeQuotedString(bool inclQuotes)
        {
            bool any = false;
            ClearCapture();

            if(Current == '\"')
            {
                if(inclQuotes)
                {
                    CaptureCurrent();
                }

                bool escaped = false;
                bool done = false;

                while(!done)
                {
                    Advance();

                    switch (Current)
                    {
                        case '\\':
                            escaped = true;
                            CaptureCurrent();
                            break;

                        case '\"':
                            if (!escaped)
                            {
                                done = true;
                            }
                            else
                            {
                                CaptureCurrent();
                            }
                            escaped = false;
                            break;

                        default:
                            CaptureCurrent();
                            escaped = false;
                            break;
                    }
                }

                any = true;

                if (inclQuotes)
                {
                    CaptureCurrent();
                }

                Advance(); // trailing quote
            }

            return any;
        }

        /// <summary>
        /// Skip over any whitespace.
        /// Current is left on value immediately after any.
        /// </summary>
        /// <returns>true if any found</returns>
        protected bool ConsumeWhiteSpace()
        {
            bool any = false;
            ClearCapture();

            while (char.IsWhiteSpace(Current) || Current == 0)
            {
                any = true;
                Advance();
            }

            return any;
        }

        /// <summary>
        /// Capture or skip a C family comment. Supports single and multi line flavors.
        /// Current is left on value immediately after any.
        /// </summary>
        /// <param name="pt">Capture or skip option.</param>
        /// <returns>true if any found</returns>
        protected bool ConsumeCComment(ParseType pt = ParseType.Skip)
        {
            bool any = false;
            ClearCapture();

            switch (Current)
            {
                case '/':
                    switch (Peek())
                    {
                        case '*': // C style
                            ConsumeUntilString("*/", ParseType.Skip);
                            any = true;
                            break;

                        case '/': // C++ style
                            ConsumeUntilString(LINE_END, ParseType.Skip);
                            any = true;
                            break;

                        default: // Probably just division, let it pass.
                            break;
                    }
                    break;

                default:
                    break;
            }

            return any;
        }

        /// <summary>
        /// Capture or skip a Lua comment. Supports single and multi line flavors.
        /// Current is left on value immediately after any.
        /// </summary>
        /// <param name="pt">Capture or skip option.</param>
        /// <returns>true if any found</returns>
        protected bool ConsumeLuaComment(ParseType pt = ParseType.Skip)//TODO
        {
            bool any = false;

            //    Single line: -- A comment to the end of the line.
            //    Lua also offers block comments, which start with --[[and run until the corresponding]]. But not ---[[

            return any;
        }

        /// <summary>
        /// Capture or skip a Python comment. Supports single and multi line flavors.
        /// Current is left on value immediately after any.
        /// </summary>
        /// <param name="pt">Capture or skip option.</param>
        /// <returns>true if any found</returns>
        protected bool ConsumePythonComment(ParseType pt = ParseType.Skip)//TODO
        {
            bool any = false;

            // # single line comment

            // A docstring is a string literal that occurs as the first statement in a module, function, class,
            // or method definition. Such a // docstring becomes the __doc__ special attribute of that object.
            // 
            // class ExampleError(Exception):
            // def example_generator(n):
            // class ExampleClass(object):
            // """Example function with PEP 484 type annotations.
            // 
            // Args:
            //     param1: The first parameter.
            //     param2: The second parameter.
            // 
            // Returns:
            //     The return value. True for success, False otherwise.
            // 
            // """
            return any;
        }

        /// <summary>
        /// Remove all whitespace and C comments.
        /// </summary>
        /// <returns>Cleaned string.</returns>
        public string CleanAll()
        {
            StringBuilder sb = new StringBuilder();

            List<(int line, int col)> refs = new List<(int, int)>();

            int outind = 0;

            try
            {
                Advance(); // start the parser
                bool done = false; // relies on throwing exception when done.
                while (!done)
                {
                    (int line, int col) start = (Line, Column);
                    if (Current == '/')
                    {
                        if(!ConsumeCComment())
                        {
                            // Probably just division, keep it.
                            sb.Append(Current);
                            refs.Add(start);
                            outind++;
                            Advance();
                        }
                    }
                    else if (Current == '\"')
                    {
                        ConsumeQuotedString(true);
                        sb.Append(CaptureBuffer);
                        refs.Add(start);
                        outind += CaptureBuffer.Length;
                    }
                    else if (char.IsWhiteSpace(Current))
                    {
                        // ignore
                        Advance();
                    }
                    else
                    {
                        sb.Append(Current);
                        refs.Add(start);
                        outind++;
                        Advance();
                    }
                }
            }
            catch (Exception ex)
            {
                if (ex is ParseException && (ex as ParseException).ExceptionType == ParseException.ParseExceptionType.Done)
                {
                    // Ran out of input - normal end.
                }
                else
                {
                    // Real error.
                    Errors.Add(ex.ToString());
                    //obj = null;
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// Test for hex value chars.
        /// </summary>
        /// <param name="hex">The char to test.</param>
        /// <returns>true if hex</returns>
        protected bool IsHexChar(char hex)
        {
            return HEX_DIGITS.Contains(hex);
        }
    }

    /// <summary>Simple custom exception container.</summary>
    public class ParseException : Exception
    {
        public enum ParseExceptionType { Done, FailedExpectation }
        public ParseExceptionType ExceptionType { get; set; }
        public int Line { get; set; } = -1;
        public int Column { get; set; } = -1;
        public ParseException(string msg) : base(msg) { }
        public override string ToString()
        {
            return $"{ExceptionType} line:{Line} col:{Column} msg:{Message}";
        }
    }
}