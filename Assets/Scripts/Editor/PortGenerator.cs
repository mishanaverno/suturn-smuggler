using System;
using System.Collections.Generic;
using System.Text;
using Interior;
using UnityEditor;
using UnityEngine;

namespace InteriorEditor
{
    /// <summary>
    /// Генератор разъёмов: по перечню дел в коде заводит недостающие ассеты в папке
    /// разъёмов и рассказывает, что в папке не сходится.
    ///
    /// Смысл — убрать ручную работу оттуда, где ошибиться легко, а заметить трудно. Ассеты
    /// пустые: генератор отвечает только за то, что на каждое дело есть ровно один ассет с
    /// правильным номером и внятным именем. Подпись у курсора, заметка и всё остальное —
    /// руками, это решается глазами.
    ///
    /// Ничего не удаляет и не переименовывает. Ассет, потерявший запись в enum, только
    /// называется в отчёте: удалить его — решение человека, потому что на него могут стоять
    /// ссылки в сцене, и молча оборвать их хуже, чем оставить мусор.
    /// </summary>
    public static class PortGenerator
    {
        const string Folder = "Assets/Scripts/Interior/Ports";

        [MenuItem("Cockpit/Разъёмы: создать недостающие")]
        public static void Generate()
        {
            if (!AssetDatabase.IsValidFolder(Folder))
            {
                Debug.LogError($"Генератор разъёмов: нет папки «{Folder}».");
                return;
            }

            StringBuilder report = new();
            int created = 0;

            created += Sync<CommandPort, CommandId>(
                port => (int)port.id,
                (port, id) => port.id = (CommandId)id,
                "Commands", report);

            created += Sync<StepPort, StepId>(
                port => (int)port.id,
                (port, id) => port.id = (StepId)id,
                "Steps", report);

            created += Sync<SettingPort, SettingId>(
                port => (int)port.id,
                (port, id) => port.id = (SettingId)id,
                "Settings", report);

            created += Sync<SignalPort, SignalId>(
                port => (int)port.id,
                (port, id) => port.id = (SignalId)id,
                "Signals", report);

            created += Sync<ReadingPort, ReadingId>(
                port => (int)port.id,
                (port, id) => port.id = (ReadingId)id,
                "Readings", report);

            if (created > 0)
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            Debug.Log($"Разъёмы: создано и привязано {created}.\n{report}");
        }

        /// <summary>
        /// Свести перечень в коде с папкой. Для каждой записи enum должен найтись ровно один
        /// ассет. Порядок поиска: сначала по номеру, потом по имени файла — так уже лежащий
        /// в папке ассет усыновляется, а не дублируется, когда номер ему ещё не проставлен.
        /// </summary>
        static int Sync<TPort, TId>(Func<TPort, int> getId, Action<TPort, int> setId, string subfolder, StringBuilder report)
            where TPort : ControlPort
            where TId : struct, Enum
        {
            List<TPort> assets = new();
            foreach (string guid in AssetDatabase.FindAssets($"t:{typeof(TPort).Name}", new[] { Folder }))
            {
                TPort asset = AssetDatabase.LoadAssetAtPath<TPort>(AssetDatabase.GUIDToAssetPath(guid));
                if (asset != null) assets.Add(asset);
            }

            int created = 0;
            HashSet<int> known = new();

            foreach (TId value in Enum.GetValues(typeof(TId)))
            {
                int id = Convert.ToInt32(value);
                if (id == 0) continue; // None — это «не привязан», а не дело.
                known.Add(id);

                List<TPort> claiming = assets.FindAll(asset => getId(asset) == id);
                if (claiming.Count > 1)
                {
                    report.AppendLine($"⚠ на «{value}» претендуют {claiming.Count} ассета: " +
                        string.Join(", ", claiming.ConvertAll(a => a.name)) + " — лишние надо удалить.");
                    continue;
                }
                if (claiming.Count == 1) continue;

                string wanted = Humanize(value.ToString());
                TPort orphan = assets.Find(asset => getId(asset) == 0 && Squash(asset.name) == Squash(wanted));
                if (orphan != null)
                {
                    setId(orphan, id);
                    EditorUtility.SetDirty(orphan);
                    report.AppendLine($"• «{orphan.name}» привязан к {value}.");
                    created++;
                    continue;
                }

                TPort port = ScriptableObject.CreateInstance<TPort>();
                setId(port, id);
                AssetDatabase.CreateAsset(port, AssetDatabase.GenerateUniqueAssetPath($"{EnsureFolder(subfolder)}/{wanted}.asset"));
                assets.Add(port);
                report.AppendLine($"• создан «{wanted}».");
                created++;
            }

            foreach (TPort asset in assets)
            {
                int id = getId(asset);
                if (id == 0)
                {
                    report.AppendLine($"⚠ «{asset.name}» ни к чему не привязан — либо переименуйте его под запись в {typeof(TId).Name}, либо удалите.");
                }
                else if (!known.Contains(id))
                {
                    report.AppendLine($"⚠ «{asset.name}» указывает на номер {id}, которого больше нет в {typeof(TId).Name} — удалите ассет или верните запись.");
                }
            }

            return created;
        }

        /// <summary>
        /// Разъёмы разных видов лежат по подпапкам: «дальность прибора» есть и как ручка,
        /// и как показание, и в одной папке их файлы подрались бы за имя.
        /// </summary>
        static string EnsureFolder(string subfolder)
        {
            string path = $"{Folder}/{subfolder}";
            if (!AssetDatabase.IsValidFolder(path)) AssetDatabase.CreateFolder(Folder, subfolder);
            return path;
        }

        /// <summary>EngineToggle → Engine Toggle. Имя файла читают люди.</summary>
        static string Humanize(string name)
        {
            StringBuilder result = new();
            for (int i = 0; i < name.Length; i++)
            {
                if (i > 0 && char.IsUpper(name[i]) && !char.IsUpper(name[i - 1])) result.Append(' ');
                result.Append(name[i]);
            }
            return result.ToString();
        }

        static string Squash(string name) => name.Replace(" ", "").ToLowerInvariant();
    }
}
