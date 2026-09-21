// Uses the installed Shader Graph editor API through reflection because its graph/node types
// are internal. Generated assets are ordinary editable graphs; no reflection runs in players.
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace MediaPipeTest.CRT.Editor
{
    internal sealed class CrtGraphBuilder
    {
        internal const string DirectoryPath = "Assets/CRT/Shaders";
        const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        static readonly Dictionary<string, Type> Types = new Dictionary<string, Type>();
        readonly object graph;
        readonly object category;
        readonly Dictionary<string, Port> properties = new Dictionary<string, Port>();
        readonly Dictionary<object, int> depths = new Dictionary<object, int>();
        readonly Dictionary<int, int> rows = new Dictionary<int, int>();

        internal readonly struct Port
        {
            internal readonly object node;
            internal readonly int slot;
            internal Port(object node, int slot = 0) { this.node = node; this.slot = slot; }
        }

        internal static Type TypeOf(string name)
        {
            if (Types.TryGetValue(name, out var cached)) return cached;
            // ShaderGraph's editor assembly is loaded by Unity before executeMethod.
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (!asm.GetName().Name.Contains("ShaderGraph") && !asm.GetName().Name.Contains("RenderPipeline")) continue;
                Type found = asm.GetTypes().FirstOrDefault(t => t.Name == name);
                if (found != null) return Types[name] = found;
            }
            throw new InvalidOperationException("Shader Graph type not found: " + name);
        }
        internal static object Get(object obj, string name)
        {
            Type type = obj as Type ?? obj.GetType();
            return type.GetProperty(name, Flags)?.GetValue(obj is Type ? null : obj)
                ?? type.GetField(name, Flags)?.GetValue(obj is Type ? null : obj);
        }
        internal static void Set(object obj, string name, object value)
        {
            var p = obj.GetType().GetProperty(name, Flags);
            var f = obj.GetType().GetField(name, Flags);
            var t = p?.PropertyType ?? f?.FieldType ?? throw new MissingMemberException(obj.GetType().Name, name);
            if (value is string s && t.IsEnum) value = Enum.Parse(t, s);
            if (t == typeof(Vector4) && value is Vector2 v2) value = new Vector4(v2.x, v2.y, 0, 0);
            if (t == typeof(Vector4) && value is Vector3 v3) value = new Vector4(v3.x, v3.y, v3.z, 0);
            if (p != null) p.SetValue(obj, value); else f.SetValue(obj, value);
        }
        internal static object Call(object obj, string name, params object[] args)
        {
            Type type = obj as Type ?? obj.GetType();
            foreach (var m in type.GetMethods(Flags).Where(m => m.Name == name && !m.IsGenericMethod))
            {
                var ps = m.GetParameters();
                if (ps.Length < args.Length || ps.Skip(args.Length).Any(p => !p.HasDefaultValue)) continue;
                if (args.Where((a, i) => a != null && !ps[i].ParameterType.IsInstanceOfType(a)).Any()) continue;
                return m.Invoke(obj is Type ? null : obj, args.Concat(ps.Skip(args.Length).Select(p => p.DefaultValue)).ToArray());
            }
            throw new MissingMethodException(type.Name, name + "(" + string.Join(",", args.Select(a => a?.GetType().Name)) + ")");
        }
        static object New(string type) => Activator.CreateInstance(TypeOf(type), true);
        static IEnumerable<object> Items(object obj) => ((IEnumerable)obj).Cast<object>();
        static object[] Nodes(object graph)
        {
            var m = graph.GetType().GetMethod("GetNodes", Flags).MakeGenericMethod(TypeOf("AbstractMaterialNode"));
            return Items(m.Invoke(graph, null)).ToArray();
        }
        static object[] Slots(object node, string method)
        {
            var slotType = TypeOf("MaterialSlot");
            object list = Activator.CreateInstance(typeof(List<>).MakeGenericType(slotType));
            node.GetType().GetMethods(Flags).First(m => m.Name == method && m.IsGenericMethod && m.GetParameters().Length == 1)
                .MakeGenericMethod(slotType).Invoke(node, new[] { list });
            return Items(list).ToArray();
        }

        CrtGraphBuilder(string target = null)
        {
            graph = New("GraphData");
            Call(graph, "AddContexts");
            category = Call(TypeOf("CategoryData"), "DefaultCategory");
            Call(graph, "AddCategory", category);
            Set(graph, "isSubGraph", target == null);
            Set(graph, "path", "CRT");
            if (target == null) return;
            var universal = New("UniversalTarget");
            Call(universal, "TrySetActiveSubTarget", TypeOf(target));
            if (target == "UniversalUnlitSubTarget") Set(universal, "surfaceType", "Transparent");
            Array targets = Array.CreateInstance(TypeOf("Target"), 1);
            targets.SetValue(universal, 0);
            var surface = TypeOf("BlockFields").GetNestedType("SurfaceDescription", Flags);
            bool vertexStack = target == "UniversalUnlitSubTarget" || target == "UniversalSpriteUnlitSubTarget";
            Array blocks = Array.CreateInstance(TypeOf("BlockFieldDescriptor"), vertexStack ? 5 : 2);
            int offset = 0;
            if (vertexStack)
            {
                var vertex = TypeOf("BlockFields").GetNestedType("VertexDescription", Flags);
                foreach (string field in new[] { "Position", "Normal", "Tangent" })
                    blocks.SetValue(vertex.GetField(field, Flags).GetValue(null), offset++);
            }
            blocks.SetValue(surface.GetField("BaseColor", Flags).GetValue(null), offset);
            blocks.SetValue(surface.GetField("Alpha", Flags).GetValue(null), offset + 1);
            Call(graph, "InitializeOutputs", targets, blocks);
        }

        object Node(string type)
        {
            var n = New(type);
            Call(graph, "AddNode", n, false);
            depths[n] = 0;
            return n;
        }
        void Connect(Port from, object to, int slot)
        {
            Call(graph, "Connect", Call(from.node, "GetSlotReference", from.slot), Call(to, "GetSlotReference", slot));
            depths[to] = Math.Max(depths.TryGetValue(to, out int d) ? d : 0, depths[from.node] + 1);
        }
        Port P(string name, object value, string type = "Vector1ShaderProperty", float min = 0, float max = 1)
        {
            var prop = New(type);
            Set(prop, "displayName", name);
            Set(prop, "overrideReferenceName", "_" + name);
            Set(prop, "generatePropertyBlock", true);
            if (value != null) Set(prop, "value", value);
            if (type == "Vector1ShaderProperty")
            {
                Set(prop, "floatType", "Slider");
                Set(prop, "rangeValues", new Vector2(min, max));
            }
            if (type == "Texture2DShaderProperty" && name == "MainTex") Set(prop, "isMainTexture", true);
            Call(graph, "AddGraphInput", prop);
            Call(category, "InsertItemIntoCategory", prop);
            var node = Node("PropertyNode");
            Set(node, "property", prop);
            return properties[name] = new Port(node);
        }
        Port C(float value)
        {
            object n = Node("Vector1Node");
            var slot = Slots(n, "GetInputSlots").First();
            Set(slot, "value", value);
            return new Port(n);
        }
        Port Op(string type, params Port[] inputs)
        {
            var n = Node(type + "Node");
            var slots = Slots(n, "GetInputSlots").OrderBy(s => (int)Get(s, "id")).ToArray();
            for (int i = 0; i < inputs.Length; i++) Connect(inputs[i], n, (int)Get(slots[i], "id"));
            return new Port(n, (int)Get(Slots(n, "GetOutputSlots").First(), "id"));
        }
        Port Channel(Port p, int i) { var n = Node("SplitNode"); Connect(p, n, 0); return new Port(n, i + 1); }
        Port V2(Port x, Port y) { var n = Node("CombineNode"); Connect(x, n, 0); Connect(y, n, 1); return new Port(n, 6); }
        Port V3(Port x, Port y, Port z) { var n = Node("CombineNode"); Connect(x, n, 0); Connect(y, n, 1); Connect(z, n, 2); return new Port(n, 5); }
        Port Add(Port a, Port b) => Op("Add", a, b);
        Port Sub(Port a, Port b) => Op("Subtract", a, b);
        Port Mul(Port a, Port b) => Op("Multiply", a, b);
        Port Div(Port a, Port b) => Op("Divide", a, b);
        Port Sat(Port a) => Op("Saturate", a);
        Port Lerp(Port a, Port b, Port t) => Op("Lerp", a, b, t);
        Port Inside(Port uv)
        {
            var x = Channel(uv, 0); var y = Channel(uv, 1);
            return Mul(Mul(Op("Step", C(0), x), Op("Step", x, C(1))), Mul(Op("Step", C(0), y), Op("Step", y, C(1))));
        }
        Port Sample(Port texture, Port uv)
        {
            object n = Node("SampleTexture2DNode");
            Connect(texture, n, 1); Connect(uv, n, 2);
            object sampler = Node("SamplerStateNode"); Set(sampler, "wrap", "Clamp");
            Connect(new Port(sampler), n, 3);
            return new Port(n);
        }
        Port Buffer(Port uv)
        {
            object n = Node("UniversalSampleBufferNode"); Set(n, "bufferType", "BlitSource");
            Connect(uv, n, 0); return new Port(n, 2);
        }
        Dictionary<string, Port> SubGraph(string name, Dictionary<string, Port> inputs)
        {
            object n = Node("SubGraphNode");
            Set(n, "asset", AssetDatabase.LoadMainAssetAtPath(DirectoryPath + "/" + name + ".shadersubgraph"));
            foreach (object slot in Slots(n, "GetInputSlots"))
            {
                string display = (string)Call(slot, "RawDisplayName");
                if (!inputs.TryGetValue(display, out Port source)) throw new Exception("Missing subgraph input " + display);
                Connect(source, n, (int)Get(slot, "id"));
            }
            return Slots(n, "GetOutputSlots").ToDictionary(s => (string)Call(s, "RawDisplayName"), s => new Port(n, (int)Get(s, "id")));
        }
        void Output(params (string name, Port p, string type)[] outputs)
        {
            object n = Node("SubGraphOutputNode");
            Set(graph, "outputNode", n);
            foreach (var output in outputs)
            {
                int id = (int)Call(n, "AddSlot", Enum.Parse(TypeOf("ConcreteSlotValueType"), output.type));
                object slot = Slots(n, "GetInputSlots").First(s => (int)Get(s, "id") == id);
                Set(slot, "displayName", output.name);
                Connect(output.p, n, id);
            }
        }
        void Save(string name, bool subgraph)
        {
            foreach (var pair in depths)
            {
                int row = rows.TryGetValue(pair.Value, out int r) ? r : 0;
                rows[pair.Value] = row + 1;
                object state = Get(pair.Key, "drawState");
                Set(state, "position", new Rect(pair.Value * 240, row * 190, 190, 150));
                Set(pair.Key, "drawState", state);
            }
            Call(graph, "ValidateGraph");
            string path = DirectoryPath + "/" + name + (subgraph ? ".shadersubgraph" : ".shadergraph");
            File.WriteAllText(path, (string)Call(TypeOf("MultiJson"), "Serialize", graph));
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
        }

        static void Coordinates()
        {
            var g = new CrtGraphBuilder();
            var uv = g.P("UV", Vector2.zero, "Vector2ShaderProperty");
            var rect = g.P("ScreenRect", new Vector4(0, 0, 1, 1), "Vector4ShaderProperty");
            var curvature = g.P("Curvature", .08f, max: .5f);
            var resolution = g.P("VirtualResolution", new Vector2(640, 360), "Vector2ShaderProperty");
            var pixelation = g.P("Pixelation", 0f);
            var origin = g.V2(g.Channel(rect, 0), g.Channel(rect, 1));
            var size = g.Op("Maximum", g.V2(g.Channel(rect, 2), g.Channel(rect, 3)), g.C(.0001f));
            var local = g.Div(g.Sub(uv, origin), size);
            var centered = g.Sub(g.Mul(local, g.C(2)), g.C(1));
            var radius = g.Op("DotProduct", centered, centered);
            var warped = g.Add(g.Mul(g.Mul(centered, g.Add(g.C(1), g.Mul(radius, curvature))), g.C(.5f)), g.C(.5f));
            var safeRes = g.Op("Maximum", resolution, g.C(1));
            var snapped = g.Div(g.Add(g.Op("Floor", g.Mul(warped, safeRes)), g.C(.5f)), safeRes);
            var sample = g.Add(origin, g.Mul(g.Lerp(warped, snapped, g.Sat(pixelation)), size));
            g.Output(("SampleUV", sample, "Vector2"), ("LocalUV", local, "Vector2"), ("Area", g.Inside(local), "Vector1"), ("Bounds", g.Inside(warped), "Vector1"));
            g.Save("CRTCoordinates", true);
        }
        static void Phosphor()
        {
            var g = new CrtGraphBuilder();
            var uv = g.P("LocalUV", Vector2.zero, "Vector2ShaderProperty");
            var color = g.P("SourceColor", Vector3.one, "Vector3ShaderProperty");
            var density = g.P("RGBDensity", 240f, min: 1, max: 1920);
            var rgbStrength = g.P("RGBStrength", .35f);
            var lineCount = g.P("ScanlineCount", 180f, min: 1, max: 1080);
            var lineWidth = g.P("ScanlineWidth", .35f, min: .02f, max: .95f);
            var scanStrength = g.P("ScanlineStrength", .3f);
            var vignette = g.P("Vignette", .25f);
            var vignetteRadius = g.P("VignetteRadius", 0f);
            var vignetteSoftness = g.P("VignetteSoftness", 1f, min: .05f, max: 2);
            var monochrome = g.P("Monochrome", 0f);
            var monoTint = g.P("MonoTint", Color.white, "ColorShaderProperty");
            var brightness = g.P("Brightness", 1.12f, max: 2);
            var x = g.Mul(g.Channel(uv, 0), g.Op("Maximum", density, g.C(1)));
            // Smooth periodic phosphors; fade toward their mean before the pattern aliases.
            var fw = g.Add(g.Op("Absolute", g.Op("DDX", x)), g.Op("Absolute", g.Op("DDY", x)));
            var visibility = g.Sub(g.C(1), g.Op("Smoothstep", g.C(.25f), g.C(.65f), fw));
            Port Stripe(float offset) => g.Add(g.C(1), g.Mul(g.C(.8f), g.Op("Cosine", g.Mul(g.Add(x, g.C(offset)), g.C(Mathf.PI * 2)))));
            var rgb = g.Lerp(g.C(1), g.V3(Stripe(0), Stripe(-1f / 3), Stripe(-2f / 3)), g.Mul(rgbStrength, visibility));
            var y = g.Mul(g.Channel(uv, 1), g.Op("Maximum", lineCount, g.C(1)));
            var dy = g.Add(g.Op("Absolute", g.Op("DDX", y)), g.Op("Absolute", g.Op("DDY", y)));
            var edge = g.Op("Maximum", g.Mul(dy, g.C(.5f)), g.C(.005f));
            var wave = g.Add(g.Mul(g.Op("Cosine", g.Mul(y, g.C(Mathf.PI * 2))), g.C(.5f)), g.C(.5f));
            var threshold = g.Sub(g.C(1), lineWidth);
            var line = g.Op("Smoothstep", g.Sub(threshold, edge), g.Add(threshold, edge), wave);
            line = g.Lerp(lineWidth, line, g.Sub(g.C(1), g.Op("Smoothstep", g.C(.5f), g.C(1), dy)));
            var scan = g.Sub(g.C(1), g.Mul(line, scanStrength));
            var centered = g.Sub(g.Mul(uv, g.C(2)), g.C(1));
            var radiusSquared = g.Mul(vignetteRadius, vignetteRadius);
            var distanceSquared = g.Op("DotProduct", centered, centered);
            var falloff = g.Sat(g.Div(g.Sub(distanceSquared, radiusSquared), g.Op("Maximum", g.Sub(g.C(2), radiusSquared), g.C(.001f))));
            falloff = g.Mul(g.C(2), g.Op("Power", falloff, g.Div(g.C(1), g.Op("Maximum", vignetteSoftness, g.C(.05f)))));
            // Radius=0, Softness=1 reduces to the previous squared-distance vignette.
            var shade = g.Sat(g.Sub(g.C(1), g.Mul(falloff, vignette)));
            var phosphors = g.Mul(color, rgb);
            var luminance = g.Op("DotProduct", phosphors, g.V3(g.C(.2126f), g.C(.7152f), g.C(.0722f)));
            // Convert after RGB modulation so full mono has no remaining colored stripes.
            var toned = g.Lerp(phosphors, g.Mul(luminance, monoTint), g.Sat(monochrome));
            var result = g.Mul(g.Mul(g.Mul(toned, scan), shade), brightness);
            g.Output(("Color", result, "Vector3"));
            g.Save("CRTPhosphor", true);
        }
        static void Surface(string name, string target, bool fullscreen = false)
        {
            var g = new CrtGraphBuilder(target);
            var uvNode = g.Node(fullscreen ? "ScreenPositionNode" : "UVNode");
            var uv = g.V2(g.Channel(new Port(uvNode), 0), g.Channel(new Port(uvNode), 1));
            bool sprite = target == "UniversalSpriteUnlitSubTarget";
            Port atlasOrigin = g.C(0), atlasSize = g.C(1);
            if (sprite)
            {
                var atlas = g.P("SpriteUVRect", new Vector4(0, 0, 1, 1), "Vector4ShaderProperty");
                atlasOrigin = g.V2(g.Channel(atlas, 0), g.Channel(atlas, 1));
                atlasSize = g.Op("Maximum", g.V2(g.Channel(atlas, 2), g.Channel(atlas, 3)), g.C(.0001f));
                uv = g.Div(g.Sub(uv, atlasOrigin), atlasSize);
            }
            g.P("ScreenRect", new Vector4(0, 0, 1, 1), "Vector4ShaderProperty");
            g.P("Curvature", .08f, max: .5f);
            g.P("VirtualResolution", new Vector2(640, 360), "Vector2ShaderProperty");
            g.P("Pixelation", 0f);
            var coords = g.SubGraph("CRTCoordinates", new Dictionary<string, Port>(g.properties) { ["UV"] = uv });
            var glitch = g.P("TransitionStrength", 0f);
            var clock = g.P("TransitionTime", 0f, max: 100000);
            var tick = g.Op("Floor", g.Mul(clock, g.C(60)));
            Port Hash(Port value) => g.Op("Fraction", g.Mul(g.Op("Sine", value), g.C(43758.5453f)));
            var band = g.Op("Floor", g.Mul(g.Channel(coords["LocalUV"], 1), g.C(38)));
            var bandNoise = Hash(g.Add(g.Mul(band, g.C(12.9898f)), g.Mul(tick, g.C(78.233f))));
            var shiftedUV = g.Add(coords["SampleUV"], g.V2(g.Mul(g.Mul(g.Sub(bandNoise, g.C(.5f)), g.C(.055f)), glitch), g.C(0)));
            Port original, sampled, alpha, sourceBounds = g.C(1), originalBounds = g.C(1);
            if (fullscreen) { original = g.Buffer(uv); sampled = g.Buffer(shiftedUV); alpha = g.Channel(original, 3); }
            else
            {
                var tex = g.P("MainTex", null, "Texture2DShaderProperty");
                var fit = g.P("FitScale", Vector2.one, "Vector2ShaderProperty");
                Port Fit(Port p) => g.Add(g.Mul(g.Sub(p, g.C(.5f)), fit), g.C(.5f));
                var baseUV = Fit(uv); var effectUV = Fit(shiftedUV);
                sourceBounds = g.Inside(effectUV); originalBounds = g.Inside(baseUV);
                Port Atlas(Port p) => g.Add(atlasOrigin, g.Mul(p, atlasSize));
                original = g.Sample(tex, sprite ? Atlas(baseUV) : baseUV);
                sampled = g.Sample(tex, sprite ? Atlas(effectUV) : effectUV);
                alpha = g.Channel(original, 3);
                if (sprite)
                {
                    var source = g.P("SourceTex", null, "Texture2DShaderProperty");
                    var use = g.P("UseSourceTexture", 0f);
                    // SpriteRenderer owns _MainTex; video uses a separate property and retains sprite alpha.
                    alpha = g.Channel(g.Sample(tex, Atlas(uv)), 3);
                    original = g.Lerp(original, g.Sample(source, baseUV), use);
                    sampled = g.Lerp(sampled, g.Sample(source, effectUV), use);
                }
            }
            g.P("RGBDensity", 240f, min: 1, max: 1920);
            g.P("RGBStrength", .35f);
            g.P("ScanlineCount", 180f, min: 1, max: 1080);
            g.P("ScanlineWidth", .35f, min: .02f, max: .95f);
            g.P("ScanlineStrength", .3f);
            g.P("Vignette", .25f);
            g.P("VignetteRadius", 0f);
            g.P("VignetteSoftness", 1f, min: .05f, max: 2);
            g.P("Monochrome", 0f);
            g.P("MonoTint", Color.white, "ColorShaderProperty");
            g.P("Brightness", 1.12f, max: 2);
            var phosphor = g.SubGraph("CRTPhosphor", new Dictionary<string, Port>(g.properties) { ["LocalUV"] = coords["LocalUV"], ["SourceColor"] = sampled });
            var grainCell = g.Op("Floor", g.Mul(coords["LocalUV"], g.V2(g.C(768), g.C(576))));
            var grain = Hash(g.Add(g.Op("DotProduct", grainCell, g.V2(g.C(12.9898f), g.C(78.233f))), g.Mul(tick, g.C(39.425f))));
            var flicker = g.Add(g.C(1), g.Mul(g.Mul(g.Sub(Hash(tick), g.C(.5f)), g.C(.24f)), glitch));
            var interference = g.Lerp(g.Mul(phosphor["Color"], flicker), grain, g.Mul(glitch, g.C(.86f)));
            var mask = g.P("EffectMask", null, "Texture2DShaderProperty");
            // CRT is fully applied inside the selected area; only the spatial mask mixes results.
            var weight = g.Mul(g.Mul(g.C(1), coords["Area"]), g.Channel(g.Sample(mask, uv), 0));
            var color = g.Lerp(g.Mul(original, originalBounds), g.Mul(interference, g.Mul(coords["Bounds"], sourceBounds)), weight);
            // Compile a real bypass variant for the effectEnabled switch.
            // MultiCompile retains both runtime-toggle variants in the Windows build.
            object keyword = New("ShaderKeyword");
            Set(keyword, "displayName", "CRT Enabled"); Set(keyword, "overrideReferenceName", "_CRT_EFFECT_ON");
            Set(keyword, "keywordDefinition", "MultiCompile"); Set(keyword, "keywordScope", "Local");
            Set(keyword, "IsShaderBuildSettingsCompatible", false); Set(keyword, "value", 1);
            Call(g.graph, "AddGraphInput", keyword); Call(g.category, "InsertItemIntoCategory", keyword);
            object bypass = g.Node("KeywordNode"); Set(bypass, "keyword", keyword);
            g.Connect(color, bypass, 1); g.Connect(g.Mul(original, originalBounds), bypass, 2);
            color = new Port(bypass);
            // Sprite/Canvas targets apply vertex tint and CanvasGroup alpha in their pass.
            // Multiplying Vertex Color here would apply that tint twice.
            foreach (var n in Nodes(g.graph).Where(n => n.GetType().Name == "BlockNode"))
            {
                string block = (string)Get(n, "name");
                if (block.EndsWith("BaseColor")) g.Connect(color, n, 0);
                if (block.EndsWith("Alpha")) g.Connect(alpha, n, 0);
            }
            g.Save(name, false);
        }
        internal static void Generate()
        {
            Directory.CreateDirectory(DirectoryPath);
            Coordinates(); Phosphor();
            Surface("CRTFullscreen", "UniversalFullscreenSubTarget", true);
            Surface("CRTSurface", "UniversalUnlitSubTarget");
            Surface("CRTSprite", "UniversalSpriteUnlitSubTarget");
            Surface("CRTCanvas", "UniversalCanvasSubTarget");
        }
    }
}
