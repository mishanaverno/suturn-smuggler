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
    /// Подписи, дерущиеся за место, выстроены в колонки у краёв экрана, к своим объектам их
    /// тянут выноски. Так делают на настоящих многофункциональных индикаторах, и по той же
    /// причине: центр экрана занят картинкой, по которой принимают решения, а текст читается
    /// там, где он никому не мешает.
    ///
    /// Но на рельсу идут только они. Тот, кто стоит в своём районе один, подписывается на
    /// месте: уступать ему некому, а выноска через полэкрана к одинокому объекту стоит
    /// дороже, чем даёт, — она тянет взгляд и перехлёстывается с соседними. Заодно этим
    /// снимается и перехлёст: если в колонку идут только соседи по пятну, порядок строк
    /// задан порядком по y и линии перекреститься не могут.
    ///
    /// Числа — отсчёты событий и данные сближений — на рельсу не идут: они привязаны к точке
    /// на траектории и в отрыве от неё ничего не значат. Их кладёт TrajectoryRenderer.
    ///
    /// Сколько будет подписей, заранее неизвестно — оно меняется каждый кадр, — поэтому их
    /// приходится плодить в рантайме. Но шрифт и начертание компонент не решает: он клонирует
    /// образцы, которые вы положили в поля. Цвет — исключение, и намеренное: он означает роль
    /// объекта и ступень в иерархии текста (NavPalette), а не вкус, и в образце его выбрать
    /// нельзя — он меняется в полёте.
    /// </summary>
    public class NavLabelRail : MonoBehaviour
    {
        /// <summary>
        /// Во сколько высот строки оценена её ширина. Настоящая ширина известна только после
        /// вёрстки текста, а решать, дерутся подписи или нет, надо до неё — поэтому здесь
        /// прикидка на имя в десяток знаков.
        /// </summary>
        const float LabelWidthInLines = 6f;

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
            public float ViewportX;
            public float ViewportY;
            public float Importance;
            public Color Role;
        }

        readonly List<Entry> all = new();
        readonly List<Entry> alone = new();
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
            NavDisplayPanel display = NavDisplayPanel.instance;
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
            Split(display);
            int used = 0;
            foreach (Entry entry in alone) PlaceAtObject(entry, display, used++);
            int leaders = 0;
            // Текст растёт от края внутрь, к картинке: рельса — это внешнее поле подписи,
            // а не место, от которого она уезжает за кадр. Поэтому в левой колонке
            // выравнивание влево, в правой — вправо, и точка отсчёта с той же стороны.
            used = Place(left, display, leftRail, true, used, ref leaders);
            used = Place(right, display, rightRail, false, used, ref leaders);
            Hide(used, leaders);
            Report(used, display);
        }

        /// <summary>
        /// Рельса нужна только тем, кто дерётся за место. Одиночка подписывается у своего
        /// объекта: выноска через полэкрана к тому, кто и так стоит один, тянет взгляд на
        /// себя и вдобавок пересекается с соседними выносками. Дерутся те, чьи строки
        /// накладываются: ряд общий и прямоугольники перекрываются по ширине.
        ///
        /// Подпись на месте — такая же точечная, как отсчёты у TrajectoryRenderer, поэтому
        /// расталкивает её тот же владелец.
        /// </summary>
        void Split(NavDisplayPanel display)
        {
            alone.Clear();
            left.Clear();
            right.Clear();
            float row = Step(display);
            float reach = LabelWidthInLines * row / display.cam.aspect;
            foreach (Entry entry in all)
            {
                bool crowded = false;
                foreach (Entry other in all)
                {
                    if (ReferenceEquals(other, entry)) continue;
                    if (Mathf.Abs(other.ViewportY - entry.ViewportY) >= row) continue;
                    if (Mathf.Abs(other.ViewportX - entry.ViewportX) >= reach) continue;
                    crowded = true;
                    break;
                }
                if (!crowded) alone.Add(entry);
                else (entry.ViewportX < 0.5f ? left : right).Add(entry);
            }
        }

        /// <summary>Шаг колонки в долях высоты экрана: высота строки с промежутком.</summary>
        float Step(NavDisplayPanel display) =>
            spacing * (float)display.LabelSceneHeight / (2f * (float)NavScale.OrthographicSize);

        void PlaceAtObject(Entry entry, NavDisplayPanel display, int used)
        {
            float step = Step(display);
            // У края стекла подпись переставляется на другую сторону метки: там её не
            // подвинуть внутрь, как на рельсе, а снаружи она просто не видна.
            bool below = entry.ViewportY > 1f - step;
            bool beforeMarker = entry.ViewportX > 1f - LabelWidthInLines * step / display.cam.aspect;

            TextMeshPro label = Label(used);
            label.text = entry.Text;
            label.color = NavPalette.Dim(entry.Role, NavPalette.NameLevel);
            label.alignment = beforeMarker ? TextAlignmentOptions.Right : TextAlignmentOptions.Left;
            label.rectTransform.pivot = new Vector2(beforeMarker ? 1f : 0f, 0.5f);
            label.transform.rotation = display.cam.transform.rotation;
            // Сдвиг тот же, что у подписей событий: имя стоит сбоку-сверху от своей метки,
            // не задевая её.
            float markerRadius = (float)display.MarkerSceneDiameter * 0.5f;
            float gap = display.LineSceneWidth * 2f;
            float x = markerRadius + gap;
            float y = markerRadius + (float)display.LabelSceneHeight * 0.5f + gap;
            label.transform.position = entry.World + display.cam.transform.rotation
                * new Vector3(beforeMarker ? -x : x, below ? -y : y, 0f);
            label.transform.localScale = Vector3.one * ((float)display.LabelSceneHeight / Mathf.Max(lineHeight, 1e-3f));
            NavPointLabels.Request(label, display);
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
        void Collect(NavDisplayPanel display)
        {
            all.Clear();
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
                all.Add(new Entry
                {
                    Text = obj.GameObject.name,
                    World = world,
                    ViewportX = viewport.x,
                    ViewportY = viewport.y,
                    Importance = Importance(obj),
                    Role = NavPalette.For(obj),
                });
            }
        }

        /// <summary>
        /// Чем крупнее тело выглядит с корабля, тем нужнее его имя: когда колонка переполнена,
        /// отнимать подпись надо у далёкой мелочи, а не у того, кто оказался ниже всех в кадре.
        /// </summary>
        static float Importance(SpaceObject obj)
        {
            SpaceObject ship = SimMono.playerShip;
            if (ship == null) return (float)obj.radius;
            double distance = (obj.simTransform.GLOBAL_R - ship.simTransform.GLOBAL_R).magnitude;
            return (float)(obj.radius / System.Math.Max(distance, 1.0));
        }

        /// <summary>
        /// Раскладка колонки: каждая подпись хочет встать напротив своего объекта, но не ближе
        /// заданного промежутка к соседней. Жадно сверху вниз — тот, кто выше, место не
        /// уступает, и подписи не прыгают от кадра к кадру, пока объекты не поменялись местами.
        /// </summary>
        int Place(List<Entry> entries, NavDisplayPanel display, float rail, bool leftColumn,
            int used, ref int leaders)
        {
            float step = Step(display);
            // Поле меряется до края строки, а не до её середины: подпись выступает над своей
            // точкой на пол-высоты, и верхняя оказывалась срезана краем стекла.
            float half = 0.5f * (float)display.LabelSceneHeight / (2f * (float)NavScale.OrthographicSize);
            float top = 1f - verticalMargin - half;
            float bottom = verticalMargin + half;
            Trim(entries, Mathf.Max(1, Mathf.FloorToInt((top - bottom) / step) + 1));
            entries.Sort((a, b) => b.ViewportY.CompareTo(a.ViewportY));
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
                label.color = NavPalette.Dim(entry.Role, NavPalette.NameLevel);
                label.alignment = leftColumn ? TextAlignmentOptions.Left : TextAlignmentOptions.Right;
                // Точка отсчёта прямоугольника — с той же стороны, что и выравнивание. Иначе
                // буквы уезжают на половину ширины прямоугольника от трансформа: при
                // выравнивании вправо это десять локальных единиц, то есть заметно дальше
                // края экрана прибора, и подпись честно рисуется там, где её не видно.
                label.rectTransform.pivot = new Vector2(leftColumn ? 0f : 1f, 0.5f);
                label.transform.rotation = display.cam.transform.rotation;
                label.transform.position = Point(display, rail, y);
                float scale = (float)display.LabelSceneHeight / Mathf.Max(lineHeight, 1e-3f);
                label.transform.localScale = Vector3.one * scale;

                // Выноска начинается за концом строки, а не у её начала: иначе линия шла бы
                // поверх собственной подписи. Ширина берётся у самой подписи — угадать её
                // по числу букв нельзя, шрифт непропорциональный.
                label.ForceMeshUpdate();
                float textWidth = ViewportWidth(display, label.preferredWidth * scale);
                float anchorX = leftColumn ? rail + textWidth + stub : rail - textWidth - stub;
                Vector3 anchorPoint = Point(display, anchorX, y);

                LineRenderer leader = Leader(leaders++);
                // Толщина — общая для всех линий прибора: выноска не должна выглядеть иначе,
                // чем орбита рядом с ней. Материал — в образце.
                leader.widthMultiplier = display.LineSceneWidth;
                leader.startColor = leader.endColor =
                    NavPalette.Dim(entry.Role, NavPalette.LeaderLevel);
                leader.positionCount = 2;
                leader.SetPosition(0, anchorPoint);
                leader.SetPosition(1, entry.World);

                used++;
            }
            return used;
        }

        /// <summary>
        /// Колонка вмещает не всё. Лишнее отбрасывается по важности, а не по месту в списке:
        /// обрезанный хвост означал бы, что имя теряет тот, кто оказался ниже всех в кадре, —
        /// а место в кадре про нужность имени ничего не говорит.
        /// </summary>
        static void Trim(List<Entry> entries, int capacity)
        {
            if (entries.Count <= capacity) return;
            entries.Sort((a, b) => b.Importance.CompareTo(a.Importance));
            entries.RemoveRange(capacity, entries.Count - capacity);
        }

        void Report(int used, NavDisplayPanel display)
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
        static float ViewportWidth(NavDisplayPanel display, float worldWidth) =>
            worldWidth / (2f * (float)NavScale.OrthographicSize * display.cam.aspect);

        Vector3 Point(NavDisplayPanel display, float x, float y) =>
            display.cam.ViewportToWorldPoint(new Vector3(x, y, display.cam.nearClipPlane + 100f));

        TextMeshPro Label(int index)
        {
            while (labels.Count <= index)
            {
                TextMeshPro text = Instantiate(labelPrefab, transform);
                text.gameObject.name = $"RailLabel {labels.Count}";
                Adopt(text.gameObject);
                // Кегль образца рельса нормализует: высоту подписи задаёт прибор долей экрана
                // (NavDisplayPanel.labelPixels), иначе имя тела читалось бы по-разному на
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

        void Hide(int usedLabels, int usedLeaders)
        {
            for (int i = usedLabels; i < labels.Count; i++) labels[i].enabled = false;
            for (int i = usedLeaders; i < leaders.Count; i++) leaders[i].enabled = false;
        }
    }
}
