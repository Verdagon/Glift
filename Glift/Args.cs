using System;
using System.Collections.Generic;
using NDesk.Options;

namespace Glift {
    static class Args {
        public static bool showHelpAndExit = false;
        public static List<string> charNames = new List<string>();
        public static bool front = false;
        public static bool sides = false;
        public static bool frontOutline = false;
        public static float xoffset = 0f;
        public static float yoffset = 0f;
        public static int zdepth = -15;
        public static float thickness = 2.5f;
        public static string ttfPath = "";
        public static float sizeMult = 1f;
        public static bool listNames = false;
        public static bool print = false;
        public static bool dryRun = false;
        public static int exitStatus = 0;
        public static double angle = 135;
        public static bool experimental = false;
        public static int flattenMethod = 0;
        public static int curveStep = 7;
        public static float angleTolerance = 0;

    private static OptionSet _parser = new OptionSet {
            {
                "c|char=", "specify a glyph by codepoint to convert " +
                "to .obj. If not specified, defaults to all glyphs in the " +
                "ttf. This can stack",
                v => {
                  if (v == null) {
                    showHelpAndExit = true;
                    exitStatus = 1;
                    return;
                  }
                  charNames.Add(v);
                }
            },
            {
                "front", "generate a .obj for the front face only",
                v => front = true
            },
            {
                "side", "generate a .obj for the side face only",
                v => sides = true
            },
            {
                "outline", "generate a .obj for the outline face only",
                v => frontOutline = true
            },
            {
                "a|angle=", "angle (in degrees) restriction for generating " +
                "side outlines where anything less than VALUE will have " +
                "side outlines (prismoids) generated for that joint. In " +
                "other words, if VALUE is 135, any joint along the front " +
                "outline, whose angle is less than 135 degrees will have a " +
                "side outline/prismoid generated at that joint. VALUE " +
                "defaults to 135. Exit 1 if VALUE is not a valid double " +
                "precision format",
                v => {
                    showHelpAndExit = !double.TryParse(v, out angle);
                    exitStatus = showHelpAndExit ? 1 : 0;
                }
            },
            {
                "l|list-names", "list glyph names",
                v => listNames = true
            },
            {
                "p|print", "print .obj to console",
                v => print = true
            },
            {
                "d|dry-run", "do not write to .obj. Useful with -p if " +
                "printing to console is the only requirement",
                v => dryRun = true
            },
            {
                "s|size=", "size multiplier. The multiplicand is " +
                "72 points. The multiplier defaults to 1. Exit 1 if " +
                "VALUE is not a valid floating point",
                v => {
                    showHelpAndExit = !float.TryParse(v, out sizeMult);
                    exitStatus = showHelpAndExit ? 1 : 0;
                }
            },
            {
                "x|xoffset=", "translate the model VALUE units across " +
                "the x axis. Exit 1 if VALUE is not a valid floating point",
                v => {
                    showHelpAndExit = !float.TryParse(v, out xoffset);
                    exitStatus = showHelpAndExit ? 1 : 0;
                }
            },
            {
                "y|yoffset=", "translate the model VALUE units across " +
                "the y axis. Exit 1 if VALUE is not a valid floating point",
                v => {
                    showHelpAndExit = !float.TryParse(v, out yoffset);
                    exitStatus = showHelpAndExit ? 1 : 0;
                }
            },
            {
                "z|zdepth=", "depth of the extrusion VALUE units across " +
                "the z axis. Defaults to 15. Exit 1 if VALUE is a non-integer",
                v => {
                    showHelpAndExit = !int.TryParse(v, out zdepth);
                    zdepth = -zdepth;
                    exitStatus = showHelpAndExit ? 1 : 0;
                }
            },
            {
                "t|thickness-outline=", "thickness of outline in VALUE " +
                "units. Defaults to 10. Exit 1 if VALUE is not a valid " +
                "floating point",
                v => {
                    showHelpAndExit = !float.TryParse(v, out thickness);
                    exitStatus = showHelpAndExit ? 1 : 0;
                }
            },
            {
                "experimental", "enable experimental features",
                v => experimental = true
            },
            {
                "h|help", "show this message and exit",
                v => {
                    showHelpAndExit = v != null;
                    exitStatus = 0;
                }
            },
            {
                "flatten-method=", "which method to use to turn a font " +
                "glyph's outline curve into points. 0 uses the simple " +
                "flattener, which divides it into a constant number of " +
                "equal segments. 1 recursively subdivides curves into " +
                "approximating segments. Exit 1 if not 0 or 1.",
                v => {
                    showHelpAndExit = !int.TryParse(v, out flattenMethod);
                    exitStatus = showHelpAndExit ? 1 : 0;
                }
            },
            {
                "curve-step=", "how many points are made from each outline " +
                "curve of a font glyph. the z axis. Defaults to 7. Exit 1 if " +
                "VALUE is a non-integer",
                v => {
                    showHelpAndExit = !int.TryParse(v, out curveStep);
                    exitStatus = showHelpAndExit ? 1 : 0;
                }
            },
            {
                "angle-tolerance=", "for recursive subdividing method, " +
                "specifies the maximum angle (in radians) between segments. " +
                "Defaults to 0. Exit 1 if VALUE not a float between 0 and " +
                "2pi",
                v => {
                    showHelpAndExit = !float.TryParse(v, out angleTolerance);
                    exitStatus = showHelpAndExit ? 1 : 0;
                }
            },
        };

        private static void _ShowHelp(OptionSet parser) {
            Console.WriteLine("usage: " +
                $"{System.AppDomain.CurrentDomain.FriendlyName} " +
                "[OPTIONS]+ TTF");
            Console.WriteLine();
            Console.WriteLine("convert .ttf glyphs to .obj");
            Console.WriteLine();
            Console.WriteLine("positional arguments:");
            Console.WriteLine(
                "  TTF                        path to .ttf file");
            Console.WriteLine();
            Console.WriteLine("optional arguments:");
            parser.WriteOptionDescriptions(Console.Out);
        }

        private static void _ConsumePositionalArgs(List<string> args) {
            if (args.Count != 1) {
                showHelpAndExit = true;
                exitStatus = 1;
            }
            else
                ttfPath = args[0];
        }

        public static void Parse(string[] args) {
            List<string> pos = _parser.Parse(args);
            _ConsumePositionalArgs(pos);

            if (showHelpAndExit) {
                _ShowHelp(_parser);
                Environment.Exit(exitStatus);
            }
        }
    }
}
