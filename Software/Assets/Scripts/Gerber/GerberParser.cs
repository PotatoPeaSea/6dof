using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace VirtualFlux.Gerber
{
    public enum ApertureShape
    {
        Circle,
        Rectangle,
        Obround,
    }

    public sealed class Aperture
    {
        public int DCode;
        public ApertureShape Shape;
        public float SizeX;
        public float SizeY;
    }

    public enum PrimitiveKind
    {
        Flash,
        Draw,
        Region,
    }

    public sealed class GerberPrimitive
    {
        public PrimitiveKind Kind;
        public int ApertureDCode;
        public List<Vector2> Points = new List<Vector2>();
    }

    public sealed class GerberFile
    {
        public Dictionary<int, Aperture> Apertures = new Dictionary<int, Aperture>();
        public List<GerberPrimitive> Primitives = new List<GerberPrimitive>();
        public Vector2 Min;
        public Vector2 Max;

        public Vector2 SizeMm => Max - Min;
    }

    /// <summary>
    /// RS-274X subset parser sufficient for V1 copper-layer import:
    /// %FSLAX..%, %MOMM%, %ADD..C/R/O%, G01 linear draws, D01/D02/D03 ops,
    /// G36/G37 region fills. Arcs (G02/G03) and aperture macros are out of scope.
    /// </summary>
    public static class GerberParser
    {
        public static GerberFile ParseFile(string path)
        {
            using var reader = File.OpenText(path);
            return Parse(reader.ReadToEnd());
        }

        public static GerberFile Parse(string text)
        {
            var file = new GerberFile();
            file.Min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            file.Max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);

            var state = new ParserState();
            int i = 0;
            while (i < text.Length)
            {
                char c = text[i];
                if (c == '\n' || c == '\r' || c == ' ' || c == '\t')
                {
                    i++;
                    continue;
                }
                if (c == '%')
                {
                    int end = text.IndexOf('%', i + 1);
                    if (end < 0) break;
                    ParseExtended(text.Substring(i + 1, end - i - 1).TrimEnd('*'), state, file);
                    i = end + 1;
                    continue;
                }
                if (c == '*')
                {
                    i++;
                    continue;
                }
                int blockEnd = text.IndexOf('*', i);
                if (blockEnd < 0) break;
                ParseBlock(text.Substring(i, blockEnd - i), state, file);
                i = blockEnd + 1;
            }

            if (float.IsPositiveInfinity(file.Min.x))
            {
                file.Min = Vector2.zero;
                file.Max = Vector2.zero;
            }
            return file;
        }

        private static void ParseExtended(string body, ParserState state, GerberFile file)
        {
            if (body.StartsWith("FS"))
            {
                int xIdx = body.IndexOf('X');
                int yIdx = body.IndexOf('Y');
                if (xIdx >= 0 && yIdx > xIdx && yIdx + 2 < body.Length)
                {
                    state.IntDigits = body[xIdx + 1] - '0';
                    state.DecDigits = body[xIdx + 2] - '0';
                }
                return;
            }
            if (body.StartsWith("MOIN"))
            {
                state.UnitsMmPerStep = 25.4f;
                return;
            }
            if (body.StartsWith("MOMM"))
            {
                state.UnitsMmPerStep = 1f;
                return;
            }
            if (body.StartsWith("ADD"))
            {
                // ADD<num><shape>,<params>
                int comma = body.IndexOf(',');
                if (comma < 0) return;
                string head = body.Substring(3, comma - 3); // e.g. "10C"
                string tail = body.Substring(comma + 1);
                int splitAt = 0;
                while (splitAt < head.Length && char.IsDigit(head[splitAt])) splitAt++;
                if (splitAt == 0 || splitAt >= head.Length) return;
                if (!int.TryParse(head.Substring(0, splitAt), NumberStyles.Integer, CultureInfo.InvariantCulture, out var dcode)) return;
                char shapeCh = head[splitAt];
                var ap = new Aperture { DCode = dcode };
                var dims = tail.Split('X');
                float Parse(int idx) => float.Parse(dims[idx], CultureInfo.InvariantCulture);
                switch (shapeCh)
                {
                    case 'C':
                        ap.Shape = ApertureShape.Circle;
                        ap.SizeX = ap.SizeY = Parse(0);
                        break;
                    case 'R':
                        ap.Shape = ApertureShape.Rectangle;
                        ap.SizeX = Parse(0);
                        ap.SizeY = dims.Length > 1 ? Parse(1) : ap.SizeX;
                        break;
                    case 'O':
                        ap.Shape = ApertureShape.Obround;
                        ap.SizeX = Parse(0);
                        ap.SizeY = dims.Length > 1 ? Parse(1) : ap.SizeX;
                        break;
                    default:
                        return;
                }
                file.Apertures[dcode] = ap;
            }
        }

        private static void ParseBlock(string block, ParserState state, GerberFile file)
        {
            // Each block can contain G, D, X, Y codes.
            int i = 0;
            int op = 0;
            float? x = null, y = null;
            while (i < block.Length)
            {
                char c = block[i];
                if (c == 'G')
                {
                    int n = ReadInt(block, ref i, skipFirst: true);
                    if (n == 36) state.InRegion = true;
                    else if (n == 37)
                    {
                        if (state.CurrentRegion != null)
                        {
                            file.Primitives.Add(state.CurrentRegion);
                            state.CurrentRegion = null;
                        }
                        state.InRegion = false;
                    }
                    else if (n == 1) state.Interpolation = 1;
                    else if (n == 54) { /* tool prep, no-op */ }
                }
                else if (c == 'D')
                {
                    int n = ReadInt(block, ref i, skipFirst: true);
                    if (n >= 10) state.CurrentAperture = n;
                    else op = n;
                }
                else if (c == 'X')
                {
                    x = ReadCoord(block, ref i, state);
                }
                else if (c == 'Y')
                {
                    y = ReadCoord(block, ref i, state);
                }
                else
                {
                    i++;
                }
            }

            if (op == 0) return;

            var newX = x ?? state.LastX;
            var newY = y ?? state.LastY;
            var newP = new Vector2(newX, newY);

            switch (op)
            {
                case 1: // draw / region edge
                    if (state.InRegion)
                    {
                        if (state.CurrentRegion == null)
                        {
                            state.CurrentRegion = new GerberPrimitive { Kind = PrimitiveKind.Region, ApertureDCode = state.CurrentAperture };
                            state.CurrentRegion.Points.Add(new Vector2(state.LastX, state.LastY));
                        }
                        state.CurrentRegion.Points.Add(newP);
                    }
                    else
                    {
                        var prim = new GerberPrimitive { Kind = PrimitiveKind.Draw, ApertureDCode = state.CurrentAperture };
                        prim.Points.Add(new Vector2(state.LastX, state.LastY));
                        prim.Points.Add(newP);
                        file.Primitives.Add(prim);
                    }
                    Expand(file, newP);
                    break;
                case 2: // move
                    if (state.InRegion && state.CurrentRegion != null)
                    {
                        file.Primitives.Add(state.CurrentRegion);
                        state.CurrentRegion = null;
                    }
                    break;
                case 3: // flash
                    var flash = new GerberPrimitive { Kind = PrimitiveKind.Flash, ApertureDCode = state.CurrentAperture };
                    flash.Points.Add(newP);
                    file.Primitives.Add(flash);
                    Expand(file, newP);
                    break;
            }

            state.LastX = newX;
            state.LastY = newY;
        }

        private static int ReadInt(string s, ref int i, bool skipFirst)
        {
            if (skipFirst) i++;
            int start = i;
            while (i < s.Length && (char.IsDigit(s[i]) || (i == start && s[i] == '-'))) i++;
            int.TryParse(s.AsSpan(start, i - start), NumberStyles.Integer, CultureInfo.InvariantCulture, out var v);
            return v;
        }

        private static float ReadCoord(string s, ref int i, ParserState state)
        {
            i++; // consume X or Y
            int start = i;
            if (i < s.Length && s[i] == '-') i++;
            while (i < s.Length && char.IsDigit(s[i])) i++;
            var digits = s.Substring(start, i - start);
            if (!long.TryParse(digits, NumberStyles.Integer, CultureInfo.InvariantCulture, out var raw)) return 0f;
            float scale = Mathf.Pow(10f, -state.DecDigits) * state.UnitsMmPerStep;
            return raw * scale;
        }

        private static void Expand(GerberFile file, Vector2 p)
        {
            file.Min = Vector2.Min(file.Min, p);
            file.Max = Vector2.Max(file.Max, p);
        }

        private sealed class ParserState
        {
            public int IntDigits = 3;
            public int DecDigits = 6;
            public float UnitsMmPerStep = 1f;
            public int Interpolation = 1;
            public int CurrentAperture;
            public bool InRegion;
            public GerberPrimitive CurrentRegion;
            public float LastX;
            public float LastY;
        }
    }
}
