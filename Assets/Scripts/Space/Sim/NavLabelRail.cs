using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace OuterSpace.Sim
{
    /// <summary>
    /// Имена объектов на экране прибора. Раньше каждое тело подписывало себя само, сдвигом
    /// вправо-вверх от своей метки, и ни одно не знало про остальные: на широкой дальности
    /// внутренние луны сходятся в пятно, и подписи дерутся за одно место. Чтобы кто-то мог
    /// уступить, нужен один, кто видит всех, — поэтому именами владеет рельса, а не тела.
    ///
    /// Подписи выстроены в колонки у краёв экрана, к своим объектам их тянут выноски. Так
    /// делают на настоящих многофункциональных индикаторах, и по той же причине: центр экрана
    /// занят картинкой, по которой принимают решения, а текст читается там, где он никому
    /// не мешает.
    ///
    /// Числа — отсчёты событий и данные сближений — на рельсу не идут: они привязаны к точке
    /// на траектории и в отрыве от неё ничего не значат. Их кладёт TrajectoryRenderer.
    /// </summary>
    public class NavLabelRail : MonoBehaviour
    {
        const string DefaultFontPath = "Fonts & Materials/LiberationSans SDF";

        /// <summary>Где стоят колонки, в долях ширины экрана прибора.</summary>
        public float leftRail = 0.04f;
        public float rightRail = 0.96f;
        /// <summary>Промежуток между соседними подписями, в высотах строки.</summary>
        public float spacing = 1.35f;
        /// <summary>Поля сверху и снизу, чтобы колонка не упиралась в край.</summary>
        public float verticalMargin = 0.04f;
        /// <summary>Длина горизонтального хвостика у подписи, в долях ширины.</summary>
        public float stub = 0.015f;
        public Color labelColor = new(0.75f, 0.8f, 0.85f);
        public Color leaderColor = new(0.4f, 0.45f, 0.5f, 0.7f);

        sealed class Entry
        {
            public string Text;
            public Vector3 World;
            public float ViewportY;
            public bool Left;
        }

        readonly List<Entry> left = new();
        readonly List<Entry> right = new();
        readonly List<TextMeshPro> labels = new();
        readonly List<LineRenderer> leaders = new();
        /// <summary>Один раз напечатать в лог всё, от чего зависит видимость подписи.</summary>
        public bool logOnce = false;

        TMP_FontAsset font;
        float lineHeight = 0f;
        bool complained;
        bool logged;

        /// <summary>
        /// Шрифт берётся при первой подписи, а не в Awake: рельса создаётся из Awake самого
        /// дисплея, и TMP_Settings в этот момент может быть ещё не поднят. TMP с пустым
        /// шрифтом не рисует ничего и не жалуется — потому здесь и жалуемся мы.
        /// </summary>
        TMP_FontAsset Font()
        {
            if (font != null) return font;
            font = TMP_Settings.defaultFontAsset;
            if (font == null) font = Resources.Load<TMP_FontAsset>(DefaultFontPath);
            if (font == null && !complained)
            {
                complained = true;
                Debug.LogError($"NavLabelRail: шрифта нет ни в TMP_Settings, ни по пути {DefaultFontPath} — подписи рисоваться не будут.");
            }
            return font;
        }

        void LateUpdate()
        {
            NavDisplayMono display = NavDisplayMono.instance;
            if (display == null || display.cam == null) return;

            Collect(display);
            int used = 0;
            // Текст растёт от края внутрь, к картинке: рельса — это внешнее поле подписи,
            // а не место, от которого она уезжает за кадр. Поэтому в левой колонке
            // выравнивание влево, в правой — вправо, и точка отсчёта с той же стороны.
            used = Place(left, display, leftRail, TextAlignmentOptions.Left, 0f, used);
            used = Place(right, display, rightRail, TextAlignmentOptions.Right, 1f, used);
            Hide(used);
            Report(used, display);
        }

        /// <summary>
        /// На рельсу попадает только то, что сейчас видно в кадре: подпись объекта за краем
        /// экрана — это строка, которой не к чему тянуть выноску.
        ///
        /// Выбранная цель гасит остальные имена. Метки при этом остаются: кольцо означает
        /// «здесь объект» и нужно всегда, а имя отвечает на вопрос «какой именно» — который
        /// игрок задаёт, пока выбирает. Выбрав, он спрашивает уже другое, и девять подписей
        /// вокруг одной нужной только мешают.
        /// </summary>
        void Collect(NavDisplayMono display)
        {
            left.Clear();
            right.Clear();
            SpaceObject only = SimMono.target;
            foreach (SpaceObject obj in SimMono.updateOrder)
            {
                if (obj?.GameObject == null) continue;
                if (only != null && !ReferenceEquals(obj, only)) continue;
                Vector3 world = obj.GameObject.transform.position;
                Vector3 viewport = display.cam.WorldToViewportPoint(world);
                if (viewport.z < 0f || viewport.x < 0f || viewport.x > 1f || viewport.y < 0f || viewport.y > 1f)
                {
                    continue;
                }
                Entry entry = new()
                {
                    Text = obj.GameObject.name,
                    World = world,
                    ViewportY = viewport.y,
                    Left = viewport.x < 0.5f,
                };
                (entry.Left ? left : right).Add(entry);
            }
        }

        /// <summary>
        /// Раскладка колонки: каждая подпись хочет встать напротив своего объекта, но не ближе
        /// заданного промежутка к соседней. Жадно сверху вниз — тот, кто выше, место не
        /// уступает, и подписи не прыгают от кадра к кадру, пока объекты не поменялись местами.
        /// </summary>
        int Place(List<Entry> entries, NavDisplayMono display, float rail, TextAlignmentOptions align,
            float pivotX, int used)
        {
            entries.Sort((a, b) => b.ViewportY.CompareTo(a.ViewportY));

            float step = spacing * (float)display.LabelSceneHeight / (2f * (float)NavScale.OrthographicSize);
            float top = 1f - verticalMargin;
            float bottom = verticalMargin;
            float previous = float.PositiveInfinity;

            foreach (Entry entry in entries)
            {
                float y = Mathf.Min(entry.ViewportY, previous - step);
                y = Mathf.Clamp(y, bottom, top);
                // Место кончилось: ниже нижнего поля подписи не ставим, иначе они полезут
                // друг на друга у самого края — то, ради чего всё и затевалось.
                if (y > previous - step && !float.IsPositiveInfinity(previous)) break;
                previous = y;

                TextMeshPro label = Label(used);
                label.text = entry.Text;
                label.alignment = align;
                // Точка отсчёта прямоугольника — с той же стороны, что и выравнивание. Иначе
                // буквы уезжают на половину ширины прямоугольника от трансформа: при
                // выравнивании вправо это десять локальных единиц, то есть заметно дальше
                // края экрана прибора, и подпись честно рисуется там, где её не видно.
                label.rectTransform.pivot = new Vector2(pivotX, 0.5f);
                label.color = labelColor;
                label.transform.rotation = display.cam.transform.rotation;
                label.transform.position = Point(display, rail, y);
                float scale = (float)display.LabelSceneHeight / Mathf.Max(lineHeight, 1e-3f);
                label.transform.localScale = Vector3.one * scale;

                // Выноска начинается за концом строки, а не у её начала: иначе линия шла бы
                // поверх собственной подписи. Ширина берётся у самой подписи — угадать её
                // по числу букв нельзя, шрифт непропорциональный.
                label.ForceMeshUpdate();
                float textWidth = ViewportWidth(display, label.preferredWidth * scale);
                float anchorX = entry.Left ? rail + textWidth + stub : rail - textWidth - stub;
                Vector3 anchorPoint = Point(display, anchorX, y);

                LineRenderer leader = Leader(used);
                leader.widthMultiplier = display.LineSceneWidth;
                leader.positionCount = 2;
                leader.SetPosition(0, anchorPoint);
                leader.SetPosition(1, entry.World);

                used++;
            }
            return used;
        }

        void Report(int used, NavDisplayMono display)
        {
            if (!logOnce || logged) return;
            logged = true;
            if (used == 0)
            {
                Debug.Log($"NavLabelRail: подписей не поставлено. В кадре слева {left.Count}, справа {right.Count}, " +
                    $"объектов в мире {SimMono.updateOrder.Count}.");
                return;
            }
            TextMeshPro first = labels[0];
            Renderer renderer = first.GetComponent<Renderer>();
            Debug.Log(
                $"NavLabelRail: поставлено {used}. Первая — «{first.text}»\n" +
                $"шрифт: {(first.font == null ? "НЕТ" : first.font.name)}, материал: {(first.fontSharedMaterial == null ? "НЕТ" : first.fontSharedMaterial.name)}\n" +
                $"мировая позиция {first.transform.position}, масштаб {first.transform.lossyScale}\n" +
                $"слой {first.gameObject.layer} (камера прибора видит маску {display.cam.cullingMask})\n" +
                $"компонент включён: {first.enabled}, renderer: {(renderer == null ? "НЕТ" : renderer.enabled.ToString())}, " +
                $"объект активен: {first.gameObject.activeInHierarchy}\n" +
                $"lineHeight {lineHeight}, LabelSceneHeight {display.LabelSceneHeight}, цвет {first.color}");
        }

        /// <summary>Мировая ширина в долях ширины экрана прибора.</summary>
        static float ViewportWidth(NavDisplayMono display, float worldWidth) =>
            worldWidth / (2f * (float)NavScale.OrthographicSize * display.cam.aspect);

        Vector3 Point(NavDisplayMono display, float x, float y) =>
            display.cam.ViewportToWorldPoint(new Vector3(x, y, display.cam.nearClipPlane + 100f));

        TextMeshPro Label(int index)
        {
            while (labels.Count <= index)
            {
                GameObject labelObject = new("RailLabel");
                labelObject.layer = gameObject.layer;
                labelObject.transform.SetParent(transform, false);
                TextMeshPro text = labelObject.AddComponent<TextMeshPro>();
                text.font = Font();
                text.fontSize = 1f;
                text.color = labelColor;
                text.textWrappingMode = TextWrappingModes.NoWrap;
                text.rectTransform.sizeDelta = new Vector2(20f, 2f);
                text.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                // fontSize у TextMeshPro — не высота строки в мировых единицах: чтобы подпись
                // занимала заданную долю высоты экрана, высота строки измеряется, а не выводится.
                text.text = "Xg";
                text.ForceMeshUpdate();
                // Ноль здесь означал бы деление на ноль в масштабе и подпись нулевого размера,
                // то есть ровно тот симптом, который трудно отличить от «не рисуется вовсе».
                if (text.preferredHeight > 0f) lineHeight = text.preferredHeight;
                labels.Add(text);
            }
            labels[index].enabled = true;
            return labels[index];
        }

        LineRenderer Leader(int index)
        {
            while (leaders.Count <= index)
            {
                GameObject leaderObject = new("RailLeader");
                leaderObject.layer = gameObject.layer;
                leaderObject.transform.SetParent(transform, false);
                LineRenderer line = leaderObject.AddComponent<LineRenderer>();
                line.useWorldSpace = true;
                line.widthCurve = AnimationCurve.Constant(0f, 1f, 1f);
                line.sharedMaterial = SimLine.Material;
                line.startColor = line.endColor = leaderColor;
                line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                leaders.Add(line);
            }
            leaders[index].enabled = true;
            return leaders[index];
        }

        void Hide(int used)
        {
            for (int i = used; i < labels.Count; i++) labels[i].enabled = false;
            for (int i = used; i < leaders.Count; i++) leaders[i].enabled = false;
        }
    }
}
