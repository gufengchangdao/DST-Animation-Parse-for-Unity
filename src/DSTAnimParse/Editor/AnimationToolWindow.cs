using UnityEngine;
using UnityEditor;
using System.IO;
using System.Linq;
using UnityEditor.Animations;
using System.Collections.Generic;
using System.Text;

namespace DSTAnimParse
{
    public class AnimationToolWindow : EditorWindow
    {
        //默认状态、默认动画，随便哪个有都行
        public static readonly HashSet<string> DEFAULT_ANIMATION_NAME = new() {
            "idle",
            "idle_loop",
            "idle_down",
            "idle_loop_down"
        };

        private const string INPUT_DIR_KEY = "DST_AnimInputDir";
        private const string OUTPUT_DIR_KEY = "DST_AnimOutputDir";

        private string inputDir = "";
        private string outputDir = "";
        private string bankName = "";
        private string buildName = "";
        private string prefabName = "";

        void PrintExplain()
        {
            var labelStyle = new GUIStyle(GUI.skin.label);
            labelStyle.wordWrap = true;  // 启用自动换行

            GUILayout.Space(10);
            PrintLink("工具使用说明", "https://iitkra4fu8q.feishu.cn/docx/S3OEdGgKjorU1WxmUI4cADXhnV3?from=from_copylink");
        }

        [MenuItem("Tools/饥荒动画导出工具")]
        public static void ShowWindow()
        {
            // 创建窗口实例
            var window = GetWindow<AnimationToolWindow>("饥荒动画导出工具");
            window.minSize = new Vector2(600, 500);

            window.inputDir = EditorPrefs.GetString(INPUT_DIR_KEY, "");
            window.outputDir = EditorPrefs.GetString(OUTPUT_DIR_KEY, "");
        }

        private List<string> selectedZipFiles = new List<string>();
        private Vector2 scrollPos;

        void OnGUI()
        {
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("添加ZIP文件", GUILayout.Width(100)))
            {
                string path = EditorUtility.OpenFilePanel("选择动画ZIP文件", inputDir, "zip");
                if (!string.IsNullOrEmpty(path) && !selectedZipFiles.Contains(path))
                {
                    selectedZipFiles.Add(path);
                    inputDir = Path.GetDirectoryName(path);
                    EditorPrefs.SetString(INPUT_DIR_KEY, inputDir); // 保存路径
                }
            }

            if (GUILayout.Button("清空列表", GUILayout.Width(100)))
            {
                selectedZipFiles.Clear();
            }
            EditorGUILayout.EndHorizontal();

            // 拖拽区域
            Rect dropArea = GUILayoutUtility.GetRect(0.0f, 50.0f, GUILayout.ExpandWidth(true));
            GUI.Box(dropArea, "拖拽ZIP文件到这里", EditorStyles.helpBox);

            // 处理拖拽事件
            Event evt = Event.current;
            switch (evt.type)
            {
                case EventType.DragUpdated:
                case EventType.DragPerform:
                    if (!dropArea.Contains(evt.mousePosition))
                        break;

                    DragAndDrop.visualMode = DragAndDropVisualMode.Copy;

                    if (evt.type == EventType.DragPerform)
                    {
                        DragAndDrop.AcceptDrag();

                        foreach (string draggedPath in DragAndDrop.paths)
                        {
                            if (Path.GetExtension(draggedPath).ToLower() == ".zip" &&
                                !selectedZipFiles.Contains(draggedPath))
                            {
                                selectedZipFiles.Add(draggedPath);
                                inputDir = Path.GetDirectoryName(draggedPath);
                                EditorPrefs.SetString(INPUT_DIR_KEY, inputDir);
                            }
                        }
                    }
                    Event.current.Use();
                    break;
            }


            // 显示已选文件列表
            GUILayout.Label("已选动画文件:");
            scrollPos = EditorGUILayout.BeginScrollView(scrollPos, GUILayout.Height(150));
            foreach (var file in selectedZipFiles)
            {
                EditorGUILayout.LabelField(Path.GetFileName(file));
            }
            EditorGUILayout.EndScrollView();


            EditorGUILayout.BeginHorizontal();
            outputDir = EditorGUILayout.TextField("unity动画生成目录", outputDir);
            if (GUILayout.Button("选择", GUILayout.Width(50)))
            {
                GUI.FocusControl(null);
                var newPath = EditorUtility.OpenFolderPanel("选择动画生成目录", outputDir, "");
                if (!string.IsNullOrEmpty(newPath))
                {
                    outputDir = newPath;
                    EditorPrefs.SetString(OUTPUT_DIR_KEY, outputDir); // 保存路径
                }
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);

            bankName = EditorGUILayout.TextField("bank名", bankName);
            buildName = EditorGUILayout.TextField("build名", buildName);
            prefabName = EditorGUILayout.TextField("预制体名", prefabName);

            // 底部按钮
            if (GUILayout.Button("生成", GUILayout.Height(30)))
            {
                PrintDirectories();
            }

            PrintExplain();
        }

        void PrintLink(string s, string link)
        {
            var linkStyle = new GUIStyle(GUI.skin.label);
            linkStyle.normal.textColor = new Color(0.298f, 0.498f, 1f); // 蓝色文字
            linkStyle.hover.textColor = Color.blue; // 鼠标悬停时加深
            linkStyle.active.textColor = Color.red; // 点击时变红

            // 第一行说明带超链接
            if (GUILayout.Button(s, linkStyle))
                Application.OpenURL(link);
        }

        private HashSet<AnimData> ANIM_DATA = new();
        private HashSet<BuildData> BUILD_DATA = new(); //虽然build一般只有一个
        private Dictionary<string, int> ANIM_OBJ_NAMES = new(); //需要创建的子对象
        private Dictionary<string, Sprite[]> SPRITE_DICT = new();
        private AnimatorController CONTROLLER;
        private Dictionary<string, List<TransformCurves>> IDLE_CURVE_DICT;
        private int MAX_ATLAS_NUM = 0;
        private string IDLE_ANIM_NAME;

        void PrintDirectories()
        {
            // 检查目录是否存在
            if (!Directory.Exists(inputDir))
                throw new System.Exception("动画文件目录不存在！");
            if (!outputDir.StartsWith(Application.dataPath))
                throw new System.Exception("bank目录必须在Assets目录下！");

            if (string.IsNullOrEmpty(bankName))
                throw new System.Exception("bank名不能为空！");

            if (string.IsNullOrEmpty(buildName))
                throw new System.Exception("build名不能为空！");

            string[] zipFiles = selectedZipFiles.Count > 0 ?
                selectedZipFiles.ToArray() :
                Directory.GetFiles(inputDir, "*.zip", SearchOption.TopDirectoryOnly);

            if (zipFiles.Length == 0)
                throw new System.Exception("没有提供动画压缩包！");

            Load(zipFiles);
            Generate();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            ANIM_DATA.Clear();
            BUILD_DATA.Clear();
            ANIM_OBJ_NAMES.Clear();
            IDLE_CURVE_DICT = null;
            CONTROLLER = null;
            MAX_ATLAS_NUM = 0;
            SPRITE_DICT.Clear();
            IDLE_ANIM_NAME = null;
        }

        private void Load(string[] zipFiles)
        {
            foreach (var zipPath in zipFiles)
            {
                var animData = AnimParser.Load(zipPath);
                if (animData == null)
                    continue; //这个压缩包里没动画

                foreach (var animation in animData.Animations)
                {
                    var bank = animation.Bank;
                    if (bank == bankName)
                        ANIM_DATA.Add(animData);
                }
            }
            if (ANIM_DATA.Count <= 0)
                throw new System.Exception($"没有bank为 {bankName} 的动画！");

            foreach (var zipPath in zipFiles)
            {
                var buildData = BuildParser.Load(zipPath);
                if (buildData != null && buildData.name == buildName)
                    BUILD_DATA.Add(buildData);
            }
            if (BUILD_DATA.Count <= 0)
                throw new System.Exception($"没有build为 {buildName} 的文件！");
        }

        private void Generate()
        {
            GenerateSprites();
            GenerateAnimation();
            // Animation : Clips/controller
            // prefab
            GeneratePrefab();
        }

        void GenerateSprites()
        {
            foreach (var buildData in BUILD_DATA)
            {
                for (int i = 0; i < buildData.atlasNames.Count; i++)
                {
                    var atlasName = buildData.atlasNames[i];
                    var atlasData = AtlasParser.Load(buildData.zipPath, atlasName, $"{outputDir}/{prefabName}/Sprites/atlas-{i}.png");
                    AtlasSlice.SliceAtlas(buildData, atlasData, i);
                }
                MAX_ATLAS_NUM = Mathf.Max(MAX_ATLAS_NUM, buildData.atlasNames.Count);
            }
        }

        #region 动画clip和动画控制器

        void GenerateAnimation()
        {
            var savePath = GetSavePath($"{outputDir}/{prefabName}/Animation", $"{prefabName}.controller");
            CONTROLLER = AssetDatabase.LoadAssetAtPath<AnimatorController>(savePath);
            if (CONTROLLER == null)
                CONTROLLER = AnimatorController.CreateAnimatorControllerAtPath(savePath);
            var rootStateMachine = CONTROLLER.layers[0].stateMachine;

            foreach (var animData in ANIM_DATA)
            {
                List<AnimationClip> clips = new();
                foreach (var animation in animData.Animations)
                {
                    var b = animation.Bank;
                    if (b != bankName)
                        continue;

                    // 创建状态
                    var state = rootStateMachine.AddState(animation.Name);
                    if (DEFAULT_ANIMATION_NAME.Contains(animation.Name) && string.IsNullOrEmpty(IDLE_ANIM_NAME)) //这个IDLE_ANIM_NAME会被unity初始化为空串，不是null
                    {
                        rootStateMachine.defaultState = state;
                        IDLE_ANIM_NAME = animation.Name;
                    }
                    state.AddStateMachineBehaviour<BaseStateBehaviour>();

                    var clip = GenerateClip(animation);
                    state.motion = clip;
                    clips.Add(clip);
                }

                // 第一帧没有图片的都把sprite清空，因为动画模型有个默认的idle图片的
                // 只有遍历完所有动画ANIM_OBJ_NAMES的值才算完整，所以写在这
                foreach (var clip in clips)
                {
                    foreach (var entry in ANIM_OBJ_NAMES)
                    {
                        for (int i = 0; i < entry.Value; i++)
                        {
                            var childPath = entry.Key + "_" + i;
                            EditorCurveBinding binding = EditorCurveBinding.PPtrCurve(childPath, typeof(SpriteRenderer), "m_Sprite");

                            ObjectReferenceKeyframe[] keyframes = AnimationUtility.GetObjectReferenceCurve(clip, binding) ??
                                new ObjectReferenceKeyframe[0];

                            // 查找或创建第0帧
                            bool foundFrame0 = false;
                            for (int j = 0; j < keyframes.Length; j++)
                            {
                                if (Mathf.Approximately(keyframes[j].time, 0f))
                                {
                                    foundFrame0 = true;
                                    break;
                                }
                            }

                            // 如果没有第0帧则添加一个清除sprite的操作
                            if (!foundFrame0)
                            {
                                ArrayUtility.Add(ref keyframes, new ObjectReferenceKeyframe
                                {
                                    time = 0f,
                                    value = null
                                });
                            }

                            // 设置回动画剪辑
                            AnimationUtility.SetObjectReferenceCurve(clip, binding, keyframes);
                            EditorUtility.SetDirty(clip);
                        }
                    }
                }
            }
        }

        AnimationClip GenerateClip(Animation animation)
        {
            string savePath = GetSavePath($"{outputDir}/{prefabName}/Animation/Clips", $"{animation.Name}.anim");
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(savePath);
            if (clip != null)
                return clip;

            clip = new AnimationClip();
            clip.frameRate = animation.FrameRate;

            var frameInterval = 1f / animation.FrameRate;
            List<AnimationEvent> events = new();

            SetTransformCurves(clip, events, animation, frameInterval);

            // 正确添加持久化动画事件
            AnimationEvent animEvent = new()
            {
                time = 1 / animation.FrameRate * animation.Frames.Count, //再加一帧，因为最后一次变换不是动画的最后一帧
                functionName = "OnAnimEndCallBack",
                stringParameter = animation.Name,
                messageOptions = SendMessageOptions.DontRequireReceiver
            };
            AnimationUtility.SetAnimationEvents(clip, events.ToArray());

            events.Add(animEvent);

            // 确保先创建Asset再设置事件

            AssetDatabase.CreateAsset(clip, savePath);
            return clip;
        }

        class TransformCurves
        {
            public int frame;
            public string animName;
            public int order;
            public Vector3 position;
            public float rotation_z;
            public Vector3 scale;
            public string spriteName;
            public string layer;

            public bool clearSprite = false;
        }

        static Vector3 GetPositionFromMatrix(Matrix4x4 matrix)
        {
            // 直接取矩阵的第4列前3个分量作为位置
            var pos = matrix.GetColumn(3);
            pos.y = -pos.y; // 饥荒y是向下的，这里要反一下
            return pos;
        }

        Sprite GetSprite(string spriteName)
        {
            // atlas_depth好像不太准,这里直接一个一个图片找过去算了
            // var atlas_depth = SYMBOL_SPRITE_MAP[symbol][element.BuildFrame];
            // var pngPath = ConvertToRelativePath($"{outputDir}/{prefabName}/Sprites/atlas-{atlas_depth}.png");
            Sprite targetSprite = null;
            for (int i = 0; i < MAX_ATLAS_NUM && targetSprite == null; i++)
            {
                var pngPath = ConvertToRelativePath($"{outputDir}/{prefabName}/Sprites/atlas-{i}.png");

                // 加载所有切片Sprite
                if (!SPRITE_DICT.TryGetValue(pngPath, out var sprites))
                {
                    sprites = AssetDatabase.LoadAllAssetsAtPath(pngPath).OfType<Sprite>().ToArray();
                    SPRITE_DICT.Add(pngPath, sprites);
                }

                // 根据名称查找特定Sprite
                targetSprite = sprites.FirstOrDefault(s => s.name == spriteName);
            }

            if (targetSprite == null)
            {
                // throw new System.Exception($"找不到名为 {spriteName} 的切片Sprite！");
            }
            return targetSprite;
        }

        void SetTransformCurves(AnimationClip clip, List<AnimationEvent> events, Animation animation, float frameInterval)
        {
            var curveList = new Dictionary<string, List<TransformCurves>>();
            if (animation.Name == IDLE_ANIM_NAME)
                IDLE_CURVE_DICT = curveList;


            var layerChangeDict = new Dictionary<int, StringBuilder>();
            var spriteChangeDict = new Dictionary<int, StringBuilder>();
            var childPathDict = new Dictionary<string, List<string>>();
            for (int i = 0; i < animation.Frames.Count; i++)
            {
                var frame = animation.Frames[i];

                // 同一个symbol有多个图层的要加后缀，不然不用加
                Dictionary<uint, int> countDict = new();
                Dictionary<string, float> rotationZDict = new();
                var elementZ = new List<(int index, float z)>(); //记录元素的深度

                for (int j = 0; j < frame.Elements.Count; j++)
                {
                    var element = frame.Elements[j];
                    var count = countDict.TryGetValue(element.Hash, out var c) ? c : 0;
                    countDict[element.Hash] = count + 1;

                    elementZ.Add((j, element.Z));
                }

                elementZ.Sort((a, b) => b.z.CompareTo(a.z)); //越小order越大
                var elementOrderDict = new int[elementZ.Count];
                for (int j = 0; j < elementZ.Count; j++)
                    elementOrderDict[elementZ[j].index] = j;

                var symbolNumDict = new Dictionary<string, int>();
                var childPathVisitedSet = new HashSet<string>(); //去重，同一帧不能有两个元素用同一个对象
                for (int j = 0; j < frame.Elements.Count; j++)
                {
                    var element = frame.Elements[j];
                    var symbol = element.Name;

                    // 对于一个动画内的某个对象,id是hash和LayerHash共同决定的,不过为了方便,我的对象命名都为symbol_index，所以我需要把id映射为对应的对象名(childPath)
                    // 而且同一个id也可能有多个元素，这时候从前往后分配对象就行
                    string childPath = null;
                    var id = element.Name + "_" + element.Layer;
                    if (childPathDict.TryGetValue(id, out var cl))
                    {
                        foreach (var c in cl)
                        {
                            if (!childPathVisitedSet.Contains(c))
                            {
                                childPath = c;
                                childPathVisitedSet.Add(c);
                                break;
                            }
                        }
                    }
                    else
                    {
                        cl = new();
                        childPathDict[id] = cl;
                    }

                    if (childPath == null)
                    {
                        var index = symbolNumDict.TryGetValue(symbol, out var ind) ? ind : 0;
                        childPath = symbol + "_" + index;
                        while (childPathVisitedSet.Contains(childPath))
                        {
                            index++;
                            childPath = symbol + "_" + index;
                        }

                        symbolNumDict[symbol] = index + 1;

                        childPathDict[id].Add(childPath);
                        childPathVisitedSet.Add(childPath);
                    }


                    // 解析变换矩阵
                    element.DecomposeMatrix(out var scaleX, out var scaleY, out var angle, out var spin);

                    var lastRotationZ = rotationZDict.TryGetValue(childPath, out var rz) ? rz : 0;
                    var rotation_z = angle * Mathf.Rad2Deg;

                    // 处理359到1的角度抖动问题
                    if (lastRotationZ > rotation_z + 180)
                        rotation_z -= 360;
                    else if (lastRotationZ < rotation_z - 180)
                        rotation_z += 360;

                    rotationZDict[childPath] = rotation_z;

                    // 分解矩阵到TRS组件
                    var curves = new TransformCurves
                    {
                        frame = i,
                        animName = animation.Name,
                        order = elementOrderDict[j],
                        position = GetPositionFromMatrix(element.Transform),
                        rotation_z = rotation_z,
                        scale = new Vector3(scaleX, scaleY, 1),
                        spriteName = $"{symbol}_{element.BuildFrame}",
                        layer = element.Layer,
                    };

                    // if (animation.Name == "pig_pickup"
                    // && element.Name == "pig_arm"
                    //     // && childPath == "pig_arm_0"
                    //     && curves.sprite != null
                    //     && (i == 4 || i == 5)
                    // )
                    // {
                    //     Debug.Log($"检查{i}, {element.Layer}, {childPath}");
                    // }

                    if (!curveList.TryGetValue(childPath, out var list))
                    {
                        list = new();
                        curveList[childPath] = list;
                    }
                    list.Add(curves);

                    // if (animation.Name == "pig_pickup" && childPath == "pig_head_1")
                    //     Debug.Log($"检查{i}, {id}, {childPath}");
                }

                foreach (var entry in symbolNumDict)
                    ANIM_OBJ_NAMES[entry.Key] = Mathf.Max(entry.Value, ANIM_OBJ_NAMES.TryGetValue(entry.Key, out var v) ? v : 0);
            }

            // 如果不是最后一帧，检查下一帧是否有图片，如果下一帧没元素了，那我需要额外加一帧把图片清掉
            foreach (var entry in curveList)
            {
                var list = entry.Value;
                var add = new List<TransformCurves>();
                for (int i = 0; i < list.Count; i++)
                {
                    var frameIndex = list[i].frame;
                    if (frameIndex != animation.Frames.Count - 1 //不是最后一帧
                        )
                    {
                        var hasNextFrame = false;
                        foreach (var item in list)
                            if (item.frame == frameIndex + 1)
                            {
                                hasNextFrame = true;
                                break;
                            }

                        if (!hasNextFrame)
                        {
                            var curves = new TransformCurves
                            {
                                frame = frameIndex + 1,
                                clearSprite = true
                            };
                            add.Add(curves);
                            // list[i].clearSprite = true; // unity的动画曲线是当前帧有图片下帧没有图片的话，这之前的时间会选择有图片
                        }
                    }
                }

                list.AddRange(add);
            }

            // 第一帧没有图片的都把sprite清空，因为动画模型有个默认的idle图片的
            // foreach (var entry in ANIM_OBJ_NAMES)
            // {
            //     for (int i = 0; i < entry.Value; i++)
            //     {
            //         var childPath = entry.Key + "_" + i;
            //         if (!curveList.TryGetValue(childPath, out var list))
            //         {
            //             list = new();
            //             curveList[childPath] = list;
            //         }

            //         var hasFristFrame = false;
            //         foreach (var data in list)
            //         {
            //             if (data.frame == 0)
            //             {
            //                 hasFristFrame = true;
            //                 break;
            //             }
            //         }

            //         if (!hasFristFrame)
            //         {
            //             var curves = new TransformCurves
            //             {
            //                 frame = 0,
            //                 clearSprite = true
            //             };
            //             list.Add(curves);
            //         }
            //     }
            // }

            // 去重，把不必要的变换去掉
            foreach (var entry in curveList)
            {
                var posXCurve = new AnimationCurve();
                var posYCurve = new AnimationCurve();
                var posZCurve = new AnimationCurve();

                var rotZCurve = new AnimationCurve();

                var scaleXCurve = new AnimationCurve();
                var scaleYCurve = new AnimationCurve();
                var scaleZCurve = new AnimationCurve();

                var sortingOrderCurve = new AnimationCurve();

                var keyframes = new List<ObjectReferenceKeyframe>();

                var childPath = entry.Key;
                Sprite emptySprite = Sprite.Create(new Texture2D(1, 1), new Rect(0, 0, 1, 1), Vector2.zero);
                for (int i = 0; i < entry.Value.Count; i++)
                {
                    var current = entry.Value[i];
                    var last = i > 0 ? entry.Value[i - 1] : null;
                    var next = i < entry.Value.Count - 1 ? entry.Value[i + 1] : null;
                    // var time = i * frameInterval;
                    var time = entry.Value[i].frame * frameInterval;

                    if (current.clearSprite)
                    {
                        keyframes.Add(new ObjectReferenceKeyframe { time = time, value = null });
                    }
                    else
                    {
                        if (last == null || !Mathf.Approximately(current.position.x, last.position.x) || (next != null && !Mathf.Approximately(next.position.x, current.position.x)))
                            posXCurve.AddKey(time, current.position.x);
                        if (last == null || !Mathf.Approximately(current.position.y, last.position.y) || (next != null && !Mathf.Approximately(next.position.y, current.position.y)))
                            posYCurve.AddKey(time, current.position.y);
                        if (last == null || !Mathf.Approximately(current.position.z, last.position.z) || (next != null && !Mathf.Approximately(next.position.z, current.position.z)))
                            posZCurve.AddKey(time, current.position.z);

                        if (last == null || !Mathf.Approximately(current.rotation_z, last.rotation_z) || (next != null && !Mathf.Approximately(next.rotation_z, current.rotation_z)))
                            rotZCurve.AddKey(time, current.rotation_z);

                        if (last == null || !Mathf.Approximately(current.scale.x, last.scale.x) || (next != null && !Mathf.Approximately(next.scale.x, current.scale.x)))
                            scaleXCurve.AddKey(time, current.scale.x);
                        if (last == null || !Mathf.Approximately(current.scale.y, last.scale.y) || (next != null && !Mathf.Approximately(next.scale.y, current.scale.y)))
                            scaleYCurve.AddKey(time, current.scale.y);
                        if (last == null || !Mathf.Approximately(current.scale.z, last.scale.z) || (next != null && !Mathf.Approximately(next.scale.z, current.scale.z)))
                            scaleZCurve.AddKey(time, current.scale.z);

                        if (last == null || current.order != last.order)
                        {
                            // 禁止插值
                            Keyframe key = new Keyframe(time, current.order);
                            key.inTangent = 0;
                            key.outTangent = 0;
                            int keyIndex = sortingOrderCurve.AddKey(key);
                            // Debug.Log($"检查：动画：{animation.Name},帧：{current.frame},对象：{childPath},动画：{current.sprite}");
                            AnimationUtility.SetKeyLeftTangentMode(sortingOrderCurve, keyIndex, AnimationUtility.TangentMode.Constant);
                            AnimationUtility.SetKeyRightTangentMode(sortingOrderCurve, keyIndex, AnimationUtility.TangentMode.Constant);
                        }

                        if (last == null || current.layer != last.layer)
                        {
                            if (!layerChangeDict.TryGetValue(current.frame, out var builder))
                            {
                                builder = new StringBuilder();
                                layerChangeDict[current.frame] = builder;
                            }
                            else
                            {
                                builder.Append("|");
                            }
                            builder.Append(childPath + "|" + current.layer);
                        }

                        if (last == null
                            || (last.spriteName != current.spriteName))//前后图片不一样就行，图片没有过渡需求
                        {
                            var sprite = GetSprite(current.spriteName);
                            if (sprite != null)
                            {
                                keyframes.Add(new ObjectReferenceKeyframe { time = time, value = sprite });
                            }
                            else
                            {
                                // 没找到图片，视为SWAP_XXX一类的图片，通过动画事件来更改
                                if (!spriteChangeDict.TryGetValue(current.frame, out var builder))
                                {
                                    builder = new StringBuilder();
                                    spriteChangeDict[current.frame] = builder;
                                }
                                else
                                {
                                    builder.Append("|");
                                }
                                builder.Append(childPath + "|" + current.spriteName);
                            }

                        }
                    }
                }

                if (posXCurve.keys.Length > 0)
                    clip.SetCurve(childPath, typeof(Transform), "localPosition.x", posXCurve);
                if (posYCurve.keys.Length > 0)
                    clip.SetCurve(childPath, typeof(Transform), "localPosition.y", posYCurve);
                if (posZCurve.keys.Length > 0)
                    clip.SetCurve(childPath, typeof(Transform), "localPosition.z", posZCurve);

                if (rotZCurve.keys.Length > 0)
                    clip.SetCurve(childPath, typeof(Transform), "localEulerAngles.z", rotZCurve); //不能直接改localRotation.z

                if (scaleXCurve.keys.Length > 0)
                    clip.SetCurve(childPath, typeof(Transform), "localScale.x", scaleXCurve);
                if (scaleYCurve.keys.Length > 0)
                    clip.SetCurve(childPath, typeof(Transform), "localScale.y", scaleYCurve);
                if (scaleZCurve.keys.Length > 0)
                    clip.SetCurve(childPath, typeof(Transform), "localScale.z", scaleZCurve);

                if (sortingOrderCurve.keys.Length > 0)
                    clip.SetCurve(childPath, typeof(SpriteRenderer), "m_SortingOrder", sortingOrderCurve);

                if (keyframes.Count > 0)
                    AnimationUtility.SetObjectReferenceCurve(clip, EditorCurveBinding.PPtrCurve(childPath, typeof(SpriteRenderer), "m_Sprite"), keyframes.ToArray());
            }

            // 一帧内可能会改多个layer，一块修改
            foreach (var entry in layerChangeDict)
            {
                events.Add(new AnimationEvent
                {
                    time = 1 / animation.FrameRate * entry.Key,
                    functionName = "SetAnimLayer",
                    stringParameter = entry.Value.ToString(),
                    messageOptions = SendMessageOptions.DontRequireReceiver
                });
            }

            foreach (var entry in spriteChangeDict)
            {
                events.Add(new AnimationEvent
                {
                    time = 1 / animation.FrameRate * entry.Key,
                    functionName = "SetAnimSprite",
                    stringParameter = entry.Value.ToString(),
                    messageOptions = SendMessageOptions.DontRequireReceiver
                });
            }
        }
        #endregion



        void GeneratePrefab()
        {
            string savePath = GetSavePath($"{outputDir}/{prefabName}", $"{prefabName}.prefab");
            if (AssetDatabase.LoadAssetAtPath<GameObject>(savePath) != null)
                return;

            var obj = new GameObject(prefabName);
            var animator = obj.AddComponent<Animator>();
            animator.runtimeAnimatorController = CONTROLLER;

            obj.AddComponent<DSTAnimator>();

            foreach (var entry in ANIM_OBJ_NAMES)
            {
                for (int i = 0; i < entry.Value; i++)
                {
                    var childPath = entry.Key + "_" + i;
                    var child = new GameObject(childPath);
                    child.transform.SetParent(obj.transform);

                    var animElement = child.AddComponent<DSTAnimElement>();

                    var spriteRenderer = child.AddComponent<SpriteRenderer>();
                    // 如果有idle动画，则贴图默认为idle动画的第一帧图片
                    if (IDLE_CURVE_DICT != null)
                        foreach (var entry2 in IDLE_CURVE_DICT)
                            if (entry2.Key == childPath)
                                foreach (var data in entry2.Value)
                                    if (data.animName == IDLE_ANIM_NAME && data.frame == 0)
                                    {
                                        child.transform.localPosition = data.position;
                                        var angles = child.transform.localEulerAngles;
                                        angles.z = data.rotation_z;
                                        child.transform.localEulerAngles = angles;
                                        child.transform.localScale = data.scale;
                                        spriteRenderer.sortingOrder = data.order;
                                        if (data.spriteName != null)
                                            spriteRenderer.sprite = GetSprite(data.spriteName);
                                    }
                }

            }

            PrefabUtility.SaveAsPrefabAsset(obj, savePath);
            DestroyImmediate(obj);
        }

        private static string ConvertToRelativePath(string absolutePath)
        {
            return "Assets" + absolutePath[Application.dataPath.Length..].Replace("\\", "/");
        }

        private static string GetSavePath(string path, string filename)
        {
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);
            return ConvertToRelativePath($"{path}/{filename}");
        }
    }
}