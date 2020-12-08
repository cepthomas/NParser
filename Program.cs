using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Diagnostics;
using NBagOfTricks;
using NBagOfTricks.PNUT;
using NBagOfTricks.Utils;

namespace NParser
{
	class Program
	{
		static void Main(string[] args)
		{
            string PATH = @"..\..\..";

            //DoOneFlavor($@"{PATH}\test.json", typeof(JsonParser));

            StringReader srdr = new StringReader(File.ReadAllText($@"{PATH}\test.json"));
            ParserCore pc = new ParserCore(srdr);
            Debug.Write(pc.CleanAll());
        }

        static void DoOneFlavor(string fname, Type t)
        {
            // Parse.
            TextReader rdr = File.OpenText(fname);

            var prs = Activator.CreateInstance(t, rdr) as ParserCore;
            var res = prs.Parse();

            // Output.
            if (res != null)
            {
                var sw = new StringWriter();
                var dmp = new Dumper(sw);
                dmp.Write(res);
                File.WriteAllText(fname + ".out", sw.GetStringBuilder().ToString());
            }
            else
            {
                File.WriteAllText(fname + ".out", string.Join(Environment.NewLine, prs.Errors));
            }
        }
    }
}
