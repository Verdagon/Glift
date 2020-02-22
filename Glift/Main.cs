using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;

using Point3 = System.Numerics.Vector3;

using Typography.OpenFont;
using Typography.Contours;

using DrawingGL;
using DrawingGL.Text;

using Util;

namespace Glift {
    using Face = VertexCache.Face;

    static class Globals {
        public static bool allGlyphs = false;
    }

    static class VertexCacheExt {
        private delegate IEnumerable<Point3> _VertGetter(Face f);
        private delegate IEnumerable<Triangle3> _TriGetter(Face f);
        private delegate int _IndexOfGetter(Point3 p, Face f);

        public static IEnumerable<Point3> VerticesOfFace(
            this VertexCache vertCache, Face face) {
            var d = new Dictionary<Face, _VertGetter> {
                { Face.Front, vertCache.Vertices },
                { Face.Side, vertCache.Vertices },
                { Face.Outline, vertCache.Vertices },
                { Face.All, vertCache.Vertices }
            };
            return d[face](face);
        }

        public static IEnumerable<Triangle3> TrianglesOfFace(
            this VertexCache vertCache, Face face) {
            var d = new Dictionary<Face, _TriGetter> {
                { Face.Front, vertCache.Triangles },
                { Face.Side, vertCache.Triangles },
                { Face.Outline, vertCache.Triangles },
                { Face.All, vertCache.Triangles }
            };
            return d[face](face);
        }

        public static int IndexOfFace(this VertexCache vertCache, 
            Point3 p, Face face) {
            var d = new Dictionary<Face, _IndexOfGetter> {
                { Face.Front, vertCache.IndexOf },
                { Face.Side, vertCache.IndexOf },
                { Face.Outline, vertCache.IndexOf },
                { Face.All, vertCache.IndexOf }
            };
            return d[face](p, face);
        }
    }

    class FaceTask {
        public string NameExt { get; set; }
        public Face Face { get; set; }

        public FaceTask() {
            NameExt = "";
            Face = Face.All;
        }

        public FaceTask(string nameExt, Face face) {
            NameExt = nameExt;
            Face = face;
        }
    }

    class MainClass {
        public delegate void WriteLine(string msg = "");

        public static RawGlyph[] GetRawGlyphs() {
            using (var fstrm = File.OpenRead(Args.ttfPath)) {
                var reader = new OpenFontReader();
                Typeface typeface = reader.Read(fstrm);
                float defaultSizeInPoints = 72f;

                GlyphNameMap[] nameIdxs = Globals.allGlyphs ?
                    typeface.GetGlyphNameIter().ToArray() :
                    Args.charNames.Select(
                        c => new GlyphNameMap(
                            typeface.GetGlyphIndexByName(c), c.ToString())).ToArray();

                return nameIdxs.Select(nameId => {
                    var builder = new GlyphPathBuilder(typeface);
                    builder.BuildFromGlyphIndex(
                        nameId.glyphIndex, defaultSizeInPoints);

                    var transl = new GlyphTranslatorToPath();
                    var wrPath = new WritablePath();
                    transl.SetOutput(wrPath);
                    builder.ReadShapes(transl);

                    var curveFlattener = new SimpleCurveFlattener();

                  if (Args.flattenMethod == 0) {
                    curveFlattener.FlattenMethod = CurveFlattenMethod.Inc;
                    curveFlattener.IncrementalStep = Args.curveStep;
                  } else {
                    curveFlattener.FlattenMethod = CurveFlattenMethod.Div;
                    curveFlattener.DivCurveAngleTolerance = Args.angleTolerance;
                  }

                  //curveFlattener.FlattenMethod = CurveFlattenMethod.Div;
                  //curveFlattener.DivCurveAngleTolerance = 30f;
                  //curveFlattener.DivCurveApproximationScale = 0.01;

                  //curveFlattener.DivCurveRecursiveLimit = 1;
                  //curveFlattener.DivCurveAngleTolerenceEpsilon = 1;

                    float[] flattenedUncoalescedPoints = curveFlattener.Flatten(
                        wrPath._points, out var uncoalescedContourInclusiveEnds);
                  if (flattenedUncoalescedPoints == null) {
                    Console.WriteLine("Failed to flatten: " + nameId.glyphName);
                    return null;
                  }

                  var (flattenedPoints, contourInclusiveEnds) =
                      CoalesceIdenticalNeighbors(flattenedUncoalescedPoints, uncoalescedContourInclusiveEnds);


                  return new RawGlyph {
                        Name = nameId.glyphName,
                        GlyphPts = flattenedPoints,
                        ContourEnds = contourInclusiveEnds
                  };
                }).ToArray();
            }
        }

    private static (float[], int[])
      CoalesceIdenticalNeighbors(
        float[] flattenedUncoalescedPoints, int[] uncoalescedContourInclusiveEnds) {

      var contourPointLists = new List<float>[uncoalescedContourInclusiveEnds.Length];
      for (int i = 0; i < uncoalescedContourInclusiveEnds.Length; i++) {
        int contourInclusiveEnd = uncoalescedContourInclusiveEnds[i];
        int contourExclusiveEnd = contourInclusiveEnd + 1;
        int contourStart = 0;
        if (i > 0) {
          int previousContourInclusiveEnd = uncoalescedContourInclusiveEnds[i - 1];
          int previousContourExclusiveEnd = previousContourInclusiveEnd + 1;
          contourStart = previousContourExclusiveEnd;
        }
        List<float> contourPoints = new List<float>();
        for (int j = 0; j < contourExclusiveEnd - contourStart; j += 2) {
          float x = flattenedUncoalescedPoints[contourStart + j];
          float y = flattenedUncoalescedPoints[contourStart + j + 1];

          int nextJ = (j + 2) % (contourExclusiveEnd - contourStart);
          float nextX = flattenedUncoalescedPoints[contourStart + nextJ];
          float nextY = flattenedUncoalescedPoints[contourStart + nextJ + 1];
          if (x == nextX && y == nextY) {
            // This is equal to the next point, so skip it. We'll let the next
            // point be added instead.
            continue;
          }

          contourPoints.Add(x);
          contourPoints.Add(y);
        }
        contourPointLists[i] = contourPoints;
      }

      // Now add the start point to the end for each one, to avoid a glift bug somewhere further
      // down.
      foreach (var contourPoints in contourPointLists) {
        contourPoints.Add(contourPoints[0]);
        contourPoints.Add(contourPoints[1]);
      }

      var contourInclusiveEnds = new List<int>();
      var flattenedPoints = new List<float>();
      foreach (var contourPoints in contourPointLists) {
        int contourStart = flattenedPoints.Count;
        flattenedPoints.AddRange(contourPoints);
        int contourExclusiveEnd = flattenedPoints.Count;
        int contourInclusiveEnd = contourExclusiveEnd - 1;
        contourInclusiveEnds.Add(contourInclusiveEnd);
      }

      return (flattenedPoints.ToArray(), contourInclusiveEnds.ToArray());
    }

    public static List<FaceTask> CreateFaceTasks() {
            var faceTasks = new List<FaceTask>();
            var faceOnlies = new List<Face>();

            if (Args.front)
                faceOnlies.Add(Face.Front);
            if (Args.sides)
                faceOnlies.Add(Face.Side);
            if (Args.frontOutline)
                faceOnlies.Add(Face.Outline);

            var d = new Dictionary<Face, string>();
            if (faceOnlies.Count > 1) {
              d[Face.Front] = ".front";
              d[Face.Side] = ".sides";
              d[Face.Outline] = ".outline.front";
            } else if (faceOnlies.Count == 1) {
              d[Face.Front] = "";
              d[Face.Side] = "";
              d[Face.Outline] = "";
            } else {
              d[Face.Front] = ".front";
              d[Face.Side] = ".sides";
              d[Face.Outline] = ".outline.front";
            }

            if (faceOnlies.Count == 0) {
              faceTasks.Add(new FaceTask(d[Face.Front], Face.Front));
              faceTasks.Add(new FaceTask(d[Face.Side], Face.Side));
              faceTasks.Add(new FaceTask(d[Face.Outline], Face.Outline));
            }
            else
                foreach (Face f in faceOnlies)
                    faceTasks.Add(new FaceTask(d[f], f));

            return faceTasks;
        }

        public static void Main(string[] args) {
      Args.Parse(args);

      ////Args.front = true;
      //Args.thickness = 1;
      //Args.sizeMult = 0.014473088888889f;
      //Args.zdepth = 72;
      //Args.xoffset = -34.546875503808601f;
      //Args.yoffset = -34.546875503808601f;
      //Args.ttfPath = "D:\\IncendianFalls\\Assets\\Fonts\\Symbols.ttf";
      ////Args.charNames.Add("d");

      ////Args.flattenMethod = 0;
      ////Args.curveStep = 0;

      //Args.flattenMethod = 1;
      //Args.angleTolerance = 0.3f;

      Globals.allGlyphs = Args.charNames.Count == 0;
            RawGlyph[] glyphs = GetRawGlyphs();
            List<FaceTask> faceTasks = CreateFaceTasks();

            WriteLine tee = null;
            if (Args.print)
                tee = Console.WriteLine;

            foreach (RawGlyph g in glyphs) {
        if (g == null) {
          continue;
        }
                if (Args.listNames) {
                    Console.WriteLine(g.Name);
                    continue;
                }

                StreamWriter objWriter = null;

                var vtxCache = new VertexCache(g,
                    Args.zdepth, Args.thickness,
                    Args.xoffset, Args.yoffset,
                    Args.sizeMult);

                foreach (FaceTask task in faceTasks) {
                    if (!Args.dryRun) {
                        objWriter = File.CreateText(
                            $"{g.Filename}{task.NameExt}.obj");
                        tee += objWriter.WriteLine;
                    }

                    tee?.Invoke($"# {g.Name}");

                    foreach (Point3 pt in vtxCache.VerticesOfFace(task.Face))
                        tee?.Invoke($"v {pt.X} {pt.Y} {pt.Z}");
                    var tris = vtxCache.TrianglesOfFace(task.Face);
          Console.WriteLine(tris.Count() + " triangles");
                    foreach (Triangle3 tri in tris) {
                        int vtxIdx1 = vtxCache.IndexOfFace(tri.P1, task.Face);
                        int vtxIdx2 = vtxCache.IndexOfFace(tri.P2, task.Face);
                        int vtxIdx3 = vtxCache.IndexOfFace(tri.P3, task.Face);
                        tee?.Invoke($"f {vtxIdx1} {vtxIdx2} {vtxIdx3}");
                    }
                    tee?.Invoke();

                    if (objWriter != null) {
                        tee -= objWriter.WriteLine;
                        objWriter?.Dispose();
                        objWriter = null;
                    }
                }
            }
        }
    }
}
