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
    ///
    /// Сколько будет подписей, заранее неизвестно — оно меняется каждый кадр, — поэтому их
    /// приходится плодить в рантайме. Но как они выглядят, компонент не решает: он клонирует
    /// образцы, которые вы положили в поля. Шрифт, цвет, толщина выноски правятся в сцене
    /// на этих образцах, а рельса занимается только раскладкой.
    /// </summary>
    public class NavLabelRail : MonoBehaviour
    {


        /// <summary>Где стоят колонки, в долях ширины экрана прибора.</summary>
        public float leftRail = 0.04f;
        public float rightRail = 0.96f;
        /// <summary>Промежуток между соседними подписями, в высотах строки.</summary>
        public float spacing = 1.35f;
        /// <summary>Поля сверху и снизу, чтобы колонка не упиралась в край.</summary>
        public float verticalMargin = 0.04f;
        /// <summary>Длина горизонтального хвостика у подписи, в долях ширины.</summary>
        public float stub = 0.015f;
        [Tooltip("Образец подписи. Клонируется по одному на каждое видимое имя.")]
        public TextMeshPro labelPrefab;
        [Tooltip("Образец выноски.")]
        public LineRenderer leaderPrefab;

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

        float lineHeight = 0f;
        bool complained;
        bool logged;

        void LateUpdate()
        {
            NavDisplayMono display = NavDisplayMono.instance;
            if (display == null || display.cam == null) return;
            if (labelPrefab == null || leaderPrefab == null)
            {
                if (!complained)
                {
                    complained = true;
                    Debug.LogError($"NavLabelRail на «{name}»: не заданы образцы подписи и выноски — имена не появятся.", this);
                }
                return;
            }

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
                // Толщина — общая для всех линий прибора: выноска не должна выглядеть иначе,
                // чем орбита рядом с ней. Остальной вид выноски (цвет, материал) — в образце.
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
                TextMeshPro text = Instantiate(labelPrefab, transform);
                text.gameObject.name = $"RailLabel {labels.Count}";
                Adopt(text.gameObject);
                // Кегль образца рельса нормализует: высоту подписи задаёт прибор долей экрана
                // (NavDisplayMono.labelPixels), иначе имя тела читалось бы по-разному на
                // разных приборах. Но прямоугольник образца при этом остаётся прежним, и
                // крупный кегль начинал переноситься по словам — ручка, которая «ничего не
                // меняет, только ломает». Переносов у имени быть не может: оно одно слово.
                text.textWrappingMode = TextWrappingModes.NoWrap;
                text.overflowMode = TextOverflowModes.Overflow;
                // fontSize у TextMeshPro — не высота строки в мировых единицах: чтобы подпись
                // занимала заданную долю высоты экрана, высота строки измеряется, а не выводится.
                text.text = "Xg";
                text.ForceMeshUpdate();
                // Ноль здесь означал бы деление на ноль в масштабе и подпись нулевого размера,
                // то есть тот самый симптом, который трудно отличить от «не рисуется вовсе».
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
                LineRenderer line = Instantiate(leaderPrefab, transform);
                line.gameObject.name = $"RailLeader {leaders.Count}";
                Adopt(line.gameObject);
                line.useWorldSpace = true;
                // Толщина у LineRenderer — произведение кривой на множитель. В образце вся
                // толщина записана в кривую (её ключ ≈0.009), и заданный прибором множитель
                // умножался бы на неё, давая линию в сотню раз тоньше нужной — ту самую
                // «пиксельную рябь». Кривую выпрямляем в единицу: форму линии по длине
                // образец пусть задаёт, а толщину задаёт прибор, одну на все свои линии.
                line.widthCurve = AnimationCurve.Constant(0f, 1f, 1f);
                leaders.Add(line);
            }
            leaders[index].enabled = true;
            return leaders[index];
        }

        /// <summary>
        /// Клон приходит со слоем своего образца, а не рельсы. Если образец лежит не на слое
        /// симуляции, камера прибора его не увидит — подписи будут созданы и не показаны,
        /// и по картинке этого не понять.
        /// </summary>
        void Adopt(GameObject clone)
        {
            clone.layer = gameObject.layer;
            foreach (Transform child in clone.GetComponentsInChildren<Transform>(true))
            {
                child.gameObject.layer = gameObject.layer;
            }
        }

        void Hide(int used)
        {
            for (int i = used; i < labels.Count; i++) labels[i].enabled = false;
            for (int i = used; i < leaders.Count; i++) leaders[i].enabled = false;
        }
    }
}
