using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;

namespace NareLauncher.Core
{
    /// <summary>Одна грань куба: чем закрашена и каким куском текстуры.</summary>
    public class ModelFace
    {
        public string Texture = "#0";
        public float[] Uv;              // null — считаем сами по размеру куба
        public int Rotation;            // 0, 90, 180, 270
        public string CullFace = "";
        public bool Hidden;             // грань не выводим в модель

        public ModelFace Copy()
        {
            return new ModelFace
            {
                Texture = Texture,
                Uv = Uv == null ? null : new[] { Uv[0], Uv[1], Uv[2], Uv[3] },
                Rotation = Rotation,
                CullFace = CullFace,
                Hidden = Hidden
            };
        }
    }

    /// <summary>
    /// Куб модели. Координаты как в Minecraft: блок это куб 16x16x16, начало
    /// в его нижнем северо-западном углу, y — вверх.
    /// </summary>
    public class ModelCube
    {
        public static readonly string[] Sides = { "north", "east", "south", "west", "up", "down" };

        public string Name = "Куб";
        public float[] From = { 0f, 0f, 0f };
        public float[] To = { 16f, 16f, 16f };

        public string Axis = "";        // пусто, x, y или z
        public float Angle;             // -45, -22.5, 0, 22.5, 45
        public float[] Origin = { 8f, 8f, 8f };
        public bool Rescale;

        public readonly Dictionary<string, ModelFace> Faces =
            new Dictionary<string, ModelFace>(StringComparer.OrdinalIgnoreCase);

        public static string SideRu(string side)
        {
            switch (side)
            {
                case "north": return "север (−Z)";
                case "south": return "юг (+Z)";
                case "west": return "запад (−X)";
                case "east": return "восток (+X)";
                case "up": return "верх";
                case "down": return "низ";
                default: return side;
            }
        }

        public ModelFace Face(string side)
        {
            ModelFace face;
            if (!Faces.TryGetValue(side, out face))
            {
                face = new ModelFace();
                Faces[side] = face;
            }
            return face;
        }

        /// <summary>Левый нижний угол по каждой оси — на случай, если ввели наоборот.</summary>
        public float Low(int axis) { return Math.Min(From[axis], To[axis]); }
        public float High(int axis) { return Math.Max(From[axis], To[axis]); }

        /// <summary>
        /// Развёртка «сама»: те же числа, что подставляет сам Minecraft, когда
        /// uv у грани не указан.
        /// </summary>
        public float[] AutoUv(string side)
        {
            float x1 = Low(0), x2 = High(0);
            float y1 = Low(1), y2 = High(1);
            float z1 = Low(2), z2 = High(2);

            switch (side)
            {
                case "north": return new[] { 16f - x2, 16f - y2, 16f - x1, 16f - y1 };
                case "south": return new[] { x1, 16f - y2, x2, 16f - y1 };
                case "west": return new[] { z1, 16f - y2, z2, 16f - y1 };
                case "east": return new[] { 16f - z2, 16f - y2, 16f - z1, 16f - y1 };
                case "up": return new[] { x1, z1, x2, z2 };
                case "down": return new[] { x1, 16f - z2, x2, 16f - z1 };
                default: return new[] { 0f, 0f, 16f, 16f };
            }
        }

        public float[] UvOf(string side)
        {
            var face = Face(side);
            return face.Uv ?? AutoUv(side);
        }

        public ModelCube Copy()
        {
            var copy = new ModelCube
            {
                Name = Name,
                From = new[] { From[0], From[1], From[2] },
                To = new[] { To[0], To[1], To[2] },
                Axis = Axis,
                Angle = Angle,
                Origin = new[] { Origin[0], Origin[1], Origin[2] },
                Rescale = Rescale
            };
            foreach (var pair in Faces) copy.Faces[pair.Key] = pair.Value.Copy();
            return copy;
        }
    }

    /// <summary>
    /// Текстура модели. В файле модели это пара «ключ — путь к картинке»,
    /// а грани ссылаются на ключ через решётку: "#0".
    /// </summary>
    public class ModelTexture
    {
        public string Key = "0";
        public string Path = "";        // minecraft:block/stone
        public string File = "";        // png на диске — для просмотра и записи в пак
        public Bitmap Image;

        public string Title
        {
            get { return "#" + Key + (Path.Length > 0 ? "  " + Path : ""); }
        }

        public void Reload()
        {
            if (Image != null) { Image.Dispose(); Image = null; }
            if (string.IsNullOrEmpty(File) || !System.IO.File.Exists(File)) return;

            try
            {
                // читаем через копию: иначе файл остаётся занятым
                using (var raw = new Bitmap(File))
                    Image = new Bitmap(raw);
            }
            catch (Exception e) { Log.Warn("Текстуру не открыл: " + e.Message); }
        }
    }

    /// <summary>
    /// Модель Minecraft в формате Java-издания: кубы и текстуры к ним.
    /// Читается и пишется как обычный .json из ресурспака.
    /// </summary>
    public class BlockModel
    {
        public string Parent = "";
        public string DisplayRaw = "";          // блок display, если он был — сохраняем как есть
        public List<ModelTexture> Textures = new List<ModelTexture>();
        public List<ModelCube> Cubes = new List<ModelCube>();

        /// <summary>Что потерялось при чтении — показываем честно.</summary>
        public List<string> Dropped = new List<string>();

        // ----------------------------------------------------------- создание

        public static BlockModel Empty()
        {
            var model = new BlockModel();
            model.Textures.Add(new ModelTexture { Key = "0", Path = "" });

            var cube = new ModelCube { Name = "Куб", From = new[] { 4f, 0f, 4f }, To = new[] { 12f, 8f, 12f } };
            foreach (var side in ModelCube.Sides) cube.Face(side).Texture = "#0";
            model.Cubes.Add(cube);

            return model;
        }

        public ModelTexture Slot(string reference)
        {
            if (string.IsNullOrEmpty(reference)) return null;

            string key = reference.StartsWith("#") ? reference.Substring(1) : reference;

            for (int hop = 0; hop < 4; hop++)
            {
                ModelTexture found = null;
                foreach (var t in Textures)
                    if (string.Equals(t.Key, key, StringComparison.OrdinalIgnoreCase)) { found = t; break; }

                if (found == null) return null;
                if (!found.Path.StartsWith("#")) return found;

                key = found.Path.Substring(1);      // ссылка на другой слот
            }
            return null;
        }

        public string FreeKey()
        {
            for (int i = 0; i < 64; i++)
            {
                string key = i.ToString(CultureInfo.InvariantCulture);
                bool taken = false;
                foreach (var t in Textures) if (t.Key == key) { taken = true; break; }
                if (!taken) return key;
            }
            return Guid.NewGuid().ToString("N").Substring(0, 4);
        }

        // ------------------------------------------------------------- чтение

        public static BlockModel Load(string path)
        {
            var model = new BlockModel();
            var json = Json.ParseFile(path);

            if (!json.IsObject) throw new Exception("Это не похоже на модель: в файле нет объекта JSON.");

            model.Parent = json["parent"].AsString("");

            var textures = json["textures"];
            if (textures.IsObject)
            {
                foreach (var key in textures.Keys)
                    model.Textures.Add(new ModelTexture
                    {
                        Key = key,
                        Path = textures[key].AsString("")
                    });
            }
            if (model.Textures.Count == 0) model.Textures.Add(new ModelTexture { Key = "0" });

            int number = 1;
            foreach (var element in json["elements"].Items)
            {
                var cube = new ModelCube
                {
                    Name = element["name"].AsString("Куб " + number++),
                    From = Three(element["from"], 0f),
                    To = Three(element["to"], 16f)
                };

                var rotation = element["rotation"];
                if (rotation.IsObject)
                {
                    cube.Axis = rotation["axis"].AsString("");
                    cube.Angle = (float)rotation["angle"].AsDouble(0);
                    cube.Origin = Three(rotation["origin"], 8f);
                    cube.Rescale = rotation["rescale"].AsBool(false);
                }

                var faces = element["faces"];
                foreach (var side in ModelCube.Sides)
                {
                    var raw = faces[side];
                    if (!raw.IsObject) { cube.Face(side).Hidden = true; continue; }

                    var face = cube.Face(side);
                    face.Texture = raw["texture"].AsString("#0");
                    face.Rotation = raw["rotation"].AsInt(0);
                    face.CullFace = raw["cullface"].AsString("");

                    var uv = raw["uv"];
                    if (uv.IsArray && uv.Count >= 4)
                        face.Uv = new[]
                        {
                            (float)uv[0].AsDouble(0), (float)uv[1].AsDouble(0),
                            (float)uv[2].AsDouble(0), (float)uv[3].AsDouble(0)
                        };
                }

                model.Cubes.Add(cube);
            }

            foreach (var key in json.Keys)
            {
                if (key == "parent" || key == "textures" || key == "elements") continue;
                model.Dropped.Add(key);
            }

            return model;
        }

        private static float[] Three(Json array, float fill)
        {
            var result = new[] { fill, fill, fill };
            if (!array.IsArray) return result;

            for (int i = 0; i < 3 && i < array.Count; i++)
                result[i] = (float)array[i].AsDouble(fill);

            return result;
        }

        // -------------------------------------------------------------- запись

        public string ToJson()
        {
            var root = Json.NewObject();
            if (!string.IsNullOrWhiteSpace(Parent)) root.Set("parent", Parent.Trim());

            var textures = Json.NewObject();
            foreach (var t in Textures)
                if (!string.IsNullOrWhiteSpace(t.Path)) textures.Set(t.Key, t.Path.Trim());

            if (textures.Count > 0) root.Set("textures", textures);

            var elements = Json.NewArray();
            foreach (var cube in Cubes)
            {
                var element = Json.NewObject();
                if (!string.IsNullOrWhiteSpace(cube.Name)) element.Set("name", cube.Name.Trim());

                element.Set("from", Numbers(cube.Low(0), cube.Low(1), cube.Low(2)));
                element.Set("to", Numbers(cube.High(0), cube.High(1), cube.High(2)));

                if (!string.IsNullOrEmpty(cube.Axis) && Math.Abs(cube.Angle) > 0.001f)
                {
                    var rotation = Json.NewObject();
                    rotation.Set("origin", Numbers(cube.Origin[0], cube.Origin[1], cube.Origin[2]));
                    rotation.Set("axis", cube.Axis);
                    rotation.Set("angle", cube.Angle);
                    if (cube.Rescale) rotation.Set("rescale", true);
                    element.Set("rotation", rotation);
                }

                var faces = Json.NewObject();
                foreach (var side in ModelCube.Sides)
                {
                    var face = cube.Face(side);
                    if (face.Hidden) continue;

                    var one = Json.NewObject();
                    var uv = cube.UvOf(side);
                    one.Set("uv", Numbers(uv[0], uv[1], uv[2], uv[3]));
                    one.Set("texture", string.IsNullOrEmpty(face.Texture) ? "#0" : face.Texture);

                    if (face.Rotation != 0) one.Set("rotation", face.Rotation);
                    if (!string.IsNullOrWhiteSpace(face.CullFace)) one.Set("cullface", face.CullFace.Trim());

                    faces.Set(side, one);
                }

                if (faces.Count > 0) element.Set("faces", faces);
                elements.Add(element);
            }

            if (elements.Count > 0) root.Set("elements", elements);
            return Tidy(root.ToJson(true));
        }

        /// <summary>
        /// Числовые списки — в одну строку, как в обычных моделях игры.
        /// Наш писатель JSON переносит каждое число, читать такое неудобно.
        /// </summary>
        private static string Tidy(string json)
        {
            return System.Text.RegularExpressions.Regex.Replace(
                json, @"\[[\s\d\.,eE+-]*\]",
                match =>
                {
                    var inside = match.Value.Trim('[', ']');
                    var parts = inside.Split(',');

                    var kept = new List<string>();
                    foreach (var part in parts)
                    {
                        string one = part.Trim();
                        if (one.Length > 0) kept.Add(one);
                    }

                    return "[" + string.Join(", ", kept.ToArray()) + "]";
                });
        }

        private static Json Numbers(params float[] values)
        {
            var array = Json.NewArray();
            foreach (var v in values) array.Add(Json.Of(Math.Round(v, 4)));
            return array;
        }

        public void Save(string path)
        {
            Paths.EnsureForFile(path);
            File.WriteAllText(path, ToJson(), new System.Text.UTF8Encoding(false));
            Log.Info("Модель сохранена: " + path);
        }

        // ------------------------------------------------------- ресурспак

        /// <summary>
        /// Куда ляжет текстура с таким путём внутри ресурспака:
        /// minecraft:block/stone -> assets/minecraft/textures/block/stone.png
        /// </summary>
        public static string TexturePath(string root, string reference)
        {
            if (string.IsNullOrWhiteSpace(reference)) return "";

            string value = reference.Trim();
            string space = "minecraft";

            int colon = value.IndexOf(':');
            if (colon > 0)
            {
                space = value.Substring(0, colon);
                value = value.Substring(colon + 1);
            }

            return Path.Combine(root, "assets", space, "textures",
                                value.Replace('/', Path.DirectorySeparatorChar) + ".png");
        }

        /// <summary>Та же логика для самой модели.</summary>
        public static string ModelPath(string root, string reference)
        {
            if (string.IsNullOrWhiteSpace(reference)) return "";

            string value = reference.Trim();
            string space = "minecraft";

            int colon = value.IndexOf(':');
            if (colon > 0)
            {
                space = value.Substring(0, colon);
                value = value.Substring(colon + 1);
            }

            return Path.Combine(root, "assets", space, "models",
                                value.Replace('/', Path.DirectorySeparatorChar) + ".json");
        }

        /// <summary>
        /// Пишет модель и её картинки в папку ресурспака. Возвращает список
        /// того, что получилось, — его и показываем игроку.
        /// </summary>
        public List<string> SaveToPack(string packRoot, string modelReference)
        {
            var done = new List<string>();

            string modelFile = ModelPath(packRoot, modelReference);
            if (modelFile.Length == 0) throw new Exception("Не задано имя модели внутри пака.");

            Save(modelFile);
            done.Add(modelFile);

            foreach (var texture in Textures)
            {
                if (string.IsNullOrWhiteSpace(texture.Path) || texture.Path.StartsWith("#")) continue;
                if (string.IsNullOrEmpty(texture.File) || !File.Exists(texture.File)) continue;

                string target = TexturePath(packRoot, texture.Path);
                if (target.Length == 0) continue;

                Paths.EnsureForFile(target);
                File.Copy(texture.File, target, true);
                done.Add(target);
            }

            // без pack.mcmeta игра пак не увидит
            string meta = Path.Combine(packRoot, "pack.mcmeta");
            if (!File.Exists(meta))
            {
                File.WriteAllText(meta,
                    "{\n  \"pack\": {\n    \"pack_format\": 46,\n" +
                    "    \"description\": \"Модели из NareLauncher\"\n  }\n}\n",
                    new System.Text.UTF8Encoding(false));
                done.Add(meta);
            }

            return done;
        }
    }
}
