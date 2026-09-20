using System.Text;

namespace Utilities
{
    /// <summary>
    /// Рамка для текстовых показаний в стиле терминала: углы, Т-образные стыки и крест из
    /// блока Box Drawing (U+2500-257F). Строит только сами строки рамки — какие данные в них
    /// класть и как считать ширину столбцов, решает вызывающий, здесь об этом ничего не знают.
    ///
    /// Требует Box Drawing в атласе шрифта показаний: обычный TMP Font Asset его не несёт,
    /// нужен отдельно собранный шрифт (или фолбэк на него).
    /// </summary>
    public static class AsciiTable
    {
        public const char TopLeft = '┌';
        public const char TopRight = '┐';
        public const char BottomLeft = '└';
        public const char BottomRight = '┘';
        public const char LeftJoint = '├';
        public const char RightJoint = '┤';
        public const char TopJoint = '┬';
        public const char BottomJoint = '┴';
        public const char Cross = '┼';
        public const char H = '─';
        public const char V = '│';

        public static string Border(int lineLength, char left = TopLeft, char right = TopRight) =>
            $"{left}{new string(H, lineLength - 2)}{right}";

        /// <summary>Рамка с врезанным заголовком: он встык за угол, остаток ширины — линией.</summary>
        public static string TitledBorder(string title, int lineLength, char left = TopLeft, char right = TopRight) =>
            $"{left}{title}{new string(H, lineLength - 2 - title.Length)}{right}";

        public static string Row(string content, int lineLength) => $"{V} {content.PadRight(lineLength - 4)} {V}";

        public static string ColumnsBorder(int[] colWidth, char joint, char left = LeftJoint, char right = RightJoint)
        {
            StringBuilder line = new(left.ToString());
            for (int c = 0; c < colWidth.Length; c++)
            {
                line.Append(H, colWidth[c] + 2);
                line.Append(c == colWidth.Length - 1 ? right : joint);
            }
            return line.ToString();
        }

        /// <summary>
        /// Разделитель с подписями столбцов, врезанными прямо в линию — вместо отдельной строки
        /// шапки и границы под ней. Подпись встык за стык, остаток столбца — линией; пустая
        /// подпись оставляет столбец просто линией.
        /// </summary>
        public static string TitledColumnsBorder(string[] titles, int[] colWidth, char joint,
            char left = LeftJoint, char right = RightJoint)
        {
            StringBuilder line = new(left.ToString());
            for (int c = 0; c < colWidth.Length; c++)
            {
                string title = titles[c];
                line.Append(title).Append(H, colWidth[c] + 2 - title.Length);
                line.Append(c == colWidth.Length - 1 ? right : joint);
            }
            return line.ToString();
        }

        public static string ColumnsRow(string[] cells, int[] colWidth)
        {
            StringBuilder line = new(V.ToString());
            for (int c = 0; c < cells.Length; c++) line.Append(' ').Append(cells[c].PadLeft(colWidth[c])).Append(' ').Append(V);
            return line.ToString();
        }
    }
}
