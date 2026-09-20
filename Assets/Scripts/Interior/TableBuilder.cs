using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor.Analytics;

namespace Interior
{
    class TableBuilder
    {
        private readonly List<List<string>> raw = new();
        private string[,] processsed;
        public void AddRow(List<string> row)
        {
            raw.Add(row);
        }
        public string GetString()
        {
            if (raw == null || raw.Count == 0) return string.Empty;
            int cols = raw.Max((list) => list.Count);
            int[] colwidths = new int[cols];
            foreach (var row in raw)
            {
                if (row == null) continue;
                for (int i = 0; i < cols; i++)
                {
                    string cell = (i < row.Count) ? row[i] ?? "" : "";
                    colwidths[i] = Math.Max(cell.Length, colwidths[i]);
    
                }
            }
            StringBuilder sb = new();
            foreach(List<string> row in raw)
            {
                for(int c = 0; c < cols; c++)
                {
                    sb.Append(buildColText(row[c], colwidths[c]));
                }
                sb.AppendLine();
            }
            return sb.ToString();
        }
        private string buildColText(string text, int colwidth)
        {
            int diff = colwidth - text.Length;
            string result = $"{text}";
            for(int i=0; i < diff; i++)
            {
                result += " ";
            }
            return result;
        }

    }
}