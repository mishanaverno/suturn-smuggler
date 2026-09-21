using System.Text;
using TMPro;
using UnityEngine;

namespace OuterSpace.Sim
{
    /// <summary>
    /// Покраска текстовых показаний. AsciiTable строит строки и про цвет ничего не знает —
    /// и правильно: ширины столбцов он считает по длине текста, а тег цвета в эту длину не
    /// входит. Поэтому красить можно только то, что уже посчитано и выровнено, и порядок
    /// здесь не стилистический: тег, вставленный до подсчёта, разваливает выравнивание ровно
    /// там, где таблицу читают взглядом по столбцу.
    /// </summary>
    public static class NavText
    {
        public static string Paint(string text, Color color) =>
            $"<color=#{ColorUtility.ToHtmlStringRGB(color)}>{text}</color>";

        /// <summary>
        /// Ячейка, выровненная по ширине столбца и покрашенная. Выравнивание здесь, а не
        /// в AsciiTable.ColumnsRow: тот выровнял бы строку вместе с тегом, то есть не
        /// выровнял бы вовсе. Его PadLeft на готовой ячейке уже ничего не делает — она
        /// длиннее нужного как раз на тег.
        /// </summary>
        public static string Cell(string text, int width, Color color) =>
            Paint(text.PadLeft(width), color);

        /// <summary>
        /// Рамка сама по себе не показание: она держит столбцы и потому гасится до уровня
        /// выноски — по тому же доводу, что и выноска у подписи. Проход идёт по готовому
        /// тексту и ищет символы Box Drawing, поэтому ему всё равно, кто и как собирал строки.
        /// </summary>
        public static string Frame(string text)
        {
            string open = $"<color=#{ColorUtility.ToHtmlStringRGB(NavPalette.Dim(NavPalette.Other, NavPalette.LeaderLevel))}>";
            StringBuilder painted = new(text.Length + 64);
            bool inside = false;
            foreach (char c in text)
            {
                bool frame = c >= '─' && c <= '╿';
                if (frame != inside)
                {
                    painted.Append(frame ? open : "</color>");
                    inside = frame;
                }
                painted.Append(c);
            }
            if (inside) painted.Append("</color>");
            return painted.ToString();
        }

        /// <summary>Яркость подписи: заголовка рамки, имени столбца, слова рядом с числом.</summary>
        public static Color Label => NavPalette.Dim(NavPalette.Own, NavPalette.NameLevel);

        /// <summary>
        /// Сколько знаков помещается в заданную ширину. Шрифт показаний моноширинный, но шаг
        /// знака из кегля не выводится: у каждого шрифта он свой, и посчитанная по формуле
        /// таблица оказывается то уже стекла, то за его краем.
        ///
        /// Меряется разностью двух строк, а не одной: к ширине одной строки примешаны поля
        /// самого текстового блока, к разности — уже нет.
        /// </summary>
        public static int Columns(TMP_Text sample, float width)
        {
            string was = sample.text;
            sample.text = new string('0', 20);
            sample.ForceMeshUpdate();
            float wide = sample.preferredWidth;
            sample.text = new string('0', 10);
            sample.ForceMeshUpdate();
            float narrow = sample.preferredWidth;
            sample.text = was;
            float advance = (wide - narrow) / 10f;
            return advance > 0f ? Mathf.FloorToInt(width / advance) : 0;
        }
    }
}
