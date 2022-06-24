using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace typegen
{

    /// <summary>
    /// Scans a file and marks the { brace } depth 
    /// Used in AngelscriptIDE to tell intellisense how many braces it's allowed to go up in looking 
    ///     to resolve a name that works since we always trace upwards through the file, so as we trace 
    ///     through to find a name match we keep a depth-level as we go through braces, we can never scan
    ///     into areas deeper than the current depth-level of the scan.
    /// </summary>
    public class DepthScanner
    {
        // RLE depth for a line
        class ScanDepth
        {
            public int[] Positions = new int[1]; // Offsets into line
            public int[] Depths = new int[1];    // Depths for corresponding offset

            public void PushDepth(int index, int depth)
            {
                int existingIdx = Array.IndexOf(Positions, index);
                if (existingIdx != -1) // Mostly for purposes of replacing "line start depth"
                {
                    Depths[existingIdx] = depth;
                    return;
                }
                Array.Resize(ref Positions, Positions.Length + 1);
                Positions[Positions.Length - 1] = index;
                Array.Resize(ref Depths, Depths.Length + 1);
                Depths[Depths.Length - 1] = depth;
            }
        }

        ScanDepth[] backing_;

        /// <summary>
        /// Simple depth at the start of a line
        /// </summary>
        /// <param name="aLine"></param>
        /// <returns></returns>
        public int GetBraceDepth(int aLine)
        {
            if (aLine >= 0 && aLine < backing_.Length)
                return backing_[aLine].Depths[0];
            return -1;
        }

        /// <summary>
        /// More accurate depth at a specific character position in a line
        /// </summary>
        /// <param name="aLine"></param>
        /// <param name="aIndex"></param>
        /// <returns></returns>
        public int GetBraceDepth(int aLine, int aIndex)
        {
            if (aLine >= 0 && aLine < backing_.Length)
            {
                ScanDepth d = backing_[aLine];
                for (int i = 0; i < d.Positions.Length; ++i)
                {
                    if (d.Positions[i] >= aIndex)
                    {
                        if (i > 0)
                            return d.Positions[i - 1];
                        return d.Positions[0];
                    }
                }
            }
            return -1;
        }

        // grab everything from here->down, stop when back to current scope after going down
        public string GrabLowerScope(int line, string[] lines)
        {
            int d = GetBraceDepth(line);
            StringBuilder sb = new StringBuilder();
            bool hitLower = false;
            for (int i = 0; i < lines.Length; ++i)
            {
                var od = GetBraceDepth(i);
                if (od > d) 
                    hitLower = true;
                sb.AppendLine(lines[i]);
                if (od == d && hitLower)
                    break;
            }
            return sb.ToString();
        }

        public void Process(string code)
        {
            string[] lines = code.Split(new string[] { "\r\n", "\n" }, StringSplitOptions.None);
            backing_ = new ScanDepth[lines.Length];

            int depth = 0;
            for (int i = 0; i < lines.Length; ++i)
            {
                backing_[i] = new ScanDepth();
                backing_[i].PushDepth(0, depth); //Push start of line depth

                for (int c = 0; c < lines[i].Length; ++c)
                {
                    char charCode = lines[i][c];
                    if (charCode == '{')
                    {
                        ++depth;
                        backing_[i].PushDepth(c, depth);
                    }
                    else if (charCode == '}')
                    {
                        --depth;
                        backing_[i].PushDepth(c, depth);
                    }
                }
            }
        }
    }
}
