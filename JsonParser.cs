using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;


namespace Parser
{
    /// <summary>
    /// This is a partial implementation of a json parser.
    ///
    /// This supports only object, dict<string, object>, list<object>. No named arrays etc.
    /// object:
    /// {
    ///     "MarkPitch": { "Original": 0, "Value": 10, "obj1" : { ... }, [ ... ] },
    ///     "Material": "Stuff is \"comportium\", finest kind!",
    ///     "SpecLimits": [ "ItKeystone", 105, "obj1" : { ... }, [ ... ] ],
    /// }
    /// array:
    /// [
    ///     "Plain string",
    ///     105,
    ///     "true",
    ///     { ... },
    ///     [ ... ]
    /// ]
    ///
    /// Full implementation would also include:
    /// object:
    /// {
    ///     "MarkPitch": { "Original": 0, "Value": 10, "obj1" : { ... }, "arr1" : [ ... ] },
    ///     "SpecLimits": [ "ItKeystone", 105, "obj1" : { ... }, "arr1" : [ ... ] ],
    /// }
    /// array:
    /// [
    ///     "Key1" : "Val1",
    ///     "obj1" : { ... },
    ///     "arr1" : [ ... ]
    /// ]
    /// </summary>
    public class JsonParser : ParserCore
    {
        #region Lifecycle
        /// <summary>
        /// Main constructor.
        /// </summary>
        public JsonParser(TextReader reader) : base(reader)
        {
        }
        #endregion

        /// <summary>
        /// Main parsing function.
        /// </summary>
        /// <returns></returns>
        public override object Parse()
        {
            object obj = null;

            try
            {
                Clean();

                // What have we got?
                switch (Current)
                {
                    case '{':
                        obj = new Dictionary<string, object>();
                        Advance();
                        ParseObject(obj as Dictionary<string, object>);
                        break;

                    case '[':
                        obj = new List<object>();
                        Advance();
                        ParseArray(obj as List<object>);
                        break;

                    default:
                        throw new ParseException("Bad top level type");
                }
            }
            catch (Exception ex)
            {
                if(ex is ParseException && (ex as ParseException).ExceptionType == ParseException.ParseExceptionType.Done)
                {
                    // Ran out of input - normal end.
                }
                else 
                {
                    // Real error.
                    Errors.Add(ex.ToString());
                    obj = null;
                }
            }

            return obj;
        }

        /// <summary>
        /// Parse a json object.
        /// </summary>
        /// <returns>The object</returns>
        void ParseObject(Dictionary<string, object> result)
        {
            // Loop until end of object.
            while (Current != '}')
            {
                // Collect element name.
                Clean();
                Expecting("\"}");

                if(Current == '\"')
                {
                    // Collect element name.
                    ConsumeQuotedString();
                    var elname = CaptureBuffer.ToString();

                    // Collect element value.
                    Clean();
                    Expecting(":");
                    Advance();
                    Clean();

                    // Determine element type.
                    switch (Current)
                    {
                        case '{':
                            Dictionary<string, object> obj = new Dictionary<string, object>();
                            result.Add(elname, obj);
                            Advance();
                            ParseObject(obj);
                            break;

                        case '[':
                            List<object> arr = new List<object>();
                            result.Add(elname, arr);
                            Advance();
                            ParseArray(arr);
                            break;

                        default: // simple value
                            result.Add(elname, ParseScalarValue());
                            ClearCapture();
                            break;
                    }

                    // skip trailing commas
                    if (Current == ',')
                    {
                        Advance();
                    }
                }
                else // } == done
                {
                }
            }

            Expecting("}");
            Advance();
            Clean();
        }

        /// <summary>
        /// Parse a json array.
        /// </summary>
        /// <returns>The array</returns>
        void ParseArray(List<object> result)
        {
            // Loop until end of array.
            while (Current != ']')
            {
                Clean();

                switch (Current)
                {
                    case '{':
                        Dictionary<string, object> obj = new Dictionary<string, object>();
                        result.Add(obj);
                        Advance();
                        ParseObject(obj);
                        break;

                    case '[':
                        List<object> arr = new List<object>();
                        result.Add(arr);
                        Advance();
                        ParseArray(arr);
                        break;

                    default: // simple
                        result.Add(ParseScalarValue());
                        ClearCapture();
                        break;
                }

                Clean();

                // skip the comma
                if (',' == Current)
                {
                    Advance();
                }

                Clean();
            }

            Expecting("]");
            Advance();
            Clean();
        }

        /// <summary>
        /// Parse scalar value.
        /// </summary>
        /// <returns>Parsed typed value</returns>
        public object ParseScalarValue()
        {
            object obj = null;

            // Could be string, scalar,...
            Clean();
            ClearCapture();

            if (Current == '\"') // it's a string
            {
                ConsumeQuotedString();
                obj = CaptureBuffer.ToString();
            }
            else // other than string
            {
                ConsumeUntilChar(",}] \t\r\n\v\f");

                // All the scalar things json can be.
                string s = CaptureBuffer.ToString();

                switch(s)
                {
                    case "true":
                        bool bt = true;
                        obj = bt;
                        break;

                    case "false":
                        bool bf = false;
                        obj = bf;
                        break;

                    case "null":
                        obj = null;
                        break;

                    default:
                        if (int.TryParse(s, out int i))
                        {
                            obj = i;
                        }
                        else if (double.TryParse(s, out double d))
                        {
                            obj = d;
                        }
                        // TODO hex, unicode, etc.
                        break;
                }
            }

            return obj;
        }

        /// <summary>
        /// Process the Current value. Removes comments and whitespace.
        /// </summary>
        public void Clean()
        {
            bool cmt = true;
            bool ws = true;
            ClearCapture();

            // Make sure we get all of them, in any order. TODO optimize?
            while (cmt || ws)
            {
                cmt = ConsumeWhiteSpace();
                ws = ConsumeCComment(ParseType.Skip);
            }
        }
    }
}