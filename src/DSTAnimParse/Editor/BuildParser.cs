using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace DSTAnimParse
{
    public class BuildParser
    {
        private const string BILD_MAGIC_STRING = "BILD";

        public static BuildData Load(string zipPath)
        {
            AnimReader reader = AnimReader.GetReader(zipPath, "build.bin");
            if (reader == null)
                return null;

            var build = new BuildData();
            build.zipPath = zipPath;

            var magic = reader.ReadUInt32();
            if (Encoding.ASCII.GetString(BitConverter.GetBytes(magic)) != BILD_MAGIC_STRING)
                throw new Exception("不是BILD文件");

            build.version = reader.ReadUInt32();
            if ((build.version & 0xFFFF) != 0)
            {
                // 默认小端序
            }
            else
            {
                reader.isBigEndian = true;
                build.version = reader.Reorder(build.version);
            }

            LoadBuild(build, reader);

            reader.Dispose();
            return build;
        }


        private static void LoadBuild(BuildData build, AnimReader reader)
        {
            var numsymbols = reader.ReadUInt32();

            var numframes = reader.ReadUInt32();

            build.name = reader.ReadLenString();

            var numatlases = reader.ReadUInt32();
            if (numatlases == 0)
                throw new Exception("build文件图集数量为0");

            for (int i = 0; i < numatlases; i++)
            {
                var atlasName = reader.ReadLenString();
                build.atlasNames.Add(atlasName);
            }

            uint effective_alphaverts = 0; //所有顶点数
            for (int i = 0; i < numsymbols; i++)
            {
                var symbol = LoadSymbol(reader);
                build.symbols[symbol.hash] = symbol;
                effective_alphaverts += symbol.countAlphaVerts();
            }

            uint alphaverts = reader.ReadUInt32();
            if (alphaverts != effective_alphaverts) //顶点个数不一样
                throw new Exception("VB 数量不匹配");

            foreach (var symbol in build.symbols.Values)
                foreach (var frame in symbol.frames)
                    LoadFramePost(frame, reader);

            if (!build.ShouldHaveHashTable || reader.BaseStream.Position >= reader.BaseStream.Length)
            {
                if (build.ShouldHaveHashTable)
                    Debug.LogError("build文件丢失哈希表，将自动命名");
                foreach (var symbol in build.symbols)
                    symbol.Value.name = $"symbol_{symbol.Key}";
            }
            else
            {
                uint hashcollection_sz = reader.ReadUInt32();

                uint num_namedsymbols = 0;

                for (uint i = 0; i < hashcollection_sz; i++)
                {
                    var h = reader.ReadUInt32();
                    var symbolName = reader.ReadLenString();
                    if (build.symbols.TryGetValue(h, out var symbol))
                    {
                        symbol.name = symbolName;
                        num_namedsymbols++;
                        if (DSTAnimator.StrHash(symbol.name) != symbol.hash)
                            throw new Exception($"build的symbol名{symbol.name}不匹配他的名字");
                    }
                }

                if (num_namedsymbols != numsymbols)
                {
                    if (num_namedsymbols < numsymbols)
                        throw new Exception("Incomplete build hash table (missing symbol name).");
                    else
                        throw new Exception("Uncaught hash collision.");
                }
            }

            if (reader.BaseStream.Position < reader.BaseStream.Length)
                Debug.LogError("警告：build文件剩余数据");
        }

        public static Symbol LoadSymbol(AnimReader reader)
        {
            var h = reader.ReadUInt32();
            var symbol = new Symbol();
            symbol.hash = h;

            uint numframes = reader.ReadUInt32();
            for (int i = 0; i < numframes; i++)
            {
                var frame = LoadFramePre(reader);
                symbol.frames.Add(frame);
            }

            for (uint i = 0; i < symbol.frames.Count; i++)
                symbol.frameNumberMap[symbol.frames[(int)i].framenum] = i;

            return symbol;
        }

        public static void LoadFramePost(BFrame frame, AnimReader reader)
        {
            float? uvwdepth = null;

            var numtrigs = frame.trianglesSize;
            for (int i = 0; i < numtrigs; i++)
            {
                var xyzs = new Vector3[3];
                var uvws = new Vector3[3];

                for (int j = 0; j < 3; j++)
                {
                    xyzs[j] = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                    uvws[j] = new Vector3(reader.ReadSingle(), reader.ReadSingle(), 0);

                    var w = reader.ReadSingle();
                    if (!uvwdepth.HasValue)
                        uvwdepth = w;
                    else if (Mathf.Abs(uvwdepth.Value - w) >= 0.5f) //同一帧内所有顶点的W值应该相同
                        throw new Exception("Inconsistent uvw depth in build symbol frame.");
                }

                frame.xyztriangles.Add(new Triangle(xyzs[0], xyzs[1], xyzs[2]));
                frame.uvwtriangles.Add(new Triangle(uvws[0], uvws[1], uvws[2]));
            }

            if (!uvwdepth.HasValue)
                frame.atlas_depth = 0;
            else
                frame.atlas_depth = (int)Math.Round(uvwdepth.Value);

            frame.UpdateAtlasBoundingBox();
        }

        public static BFrame LoadFramePre(AnimReader reader)
        {
            var frame = new BFrame();

            frame.framenum = reader.ReadUInt32();
            frame.duration = reader.ReadUInt32();

            frame.bbox = new Rect(
                reader.ReadSingle(),
                reader.ReadSingle(),
                reader.ReadSingle(),
                reader.ReadSingle()
            );

            uint alphaidx = reader.ReadUInt32();

            uint alphacount = reader.ReadUInt32();
            if (alphacount % 6 != 0) //不是一个四边形（两个三角形面片）
                throw new Exception("Corrupted build file (frame VB count should be a multiple of 6).");
            frame.SetAlphaVertCount(alphacount);

            return frame;
        }
    }

    public class BuildData
    {
        public string zipPath;
        public uint version;

        public string name;
        public List<string> atlasNames = new();
        public Dictionary<uint, Symbol> symbols = new();

        public bool ShouldHaveHashTable => version >= 6;

        public override string ToString()
        {
            return $"文件路径: {zipPath}, 版本: {version}, build名: {name}, 图集个数: {atlasNames.Count}, symbol个数: {symbols.Count}";
        }
    }

    /// <summary>
    /// symbol，对应解包后存放图片的目录
    /// </summary>
    public class Symbol
    {
        public string name; //symbol名，对应解包后的目录名
        public uint hash;
        public List<BFrame> frames = new(); // symbol数，即该symbol对应的图片数量
        public Dictionary<uint, uint> frameNumberMap = new();

        public uint countAlphaVerts()
        {
            uint count = 0;

            for (int i = 0; i < frames.Count; i++)
            {
                count += frames[i].CountAlphaVerts;
            }

            return count;
        }

        public override string ToString()
        {
            return $"symbol名: {name}, hash: {hash}, frame数: {frames.Count}";
        }
    }

    public class BFrame
    {
        public uint framenum;
        public uint duration;
        public Rect bbox;

        public List<Triangle> xyztriangles = new();
        public List<Triangle> uvwtriangles = new();
        public uint trianglesSize; //三角形个数

        public int atlas_depth; //表示去哪个图集文件里找图片

        public Rect atlas_bbox = new();

        // 设置顶点个数
        public void SetAlphaVertCount(uint n)
        {
            uint trigs = n / 3;
            trianglesSize = trigs;
        }

        // 顶点个数
        public uint CountAlphaVerts => 3 * trianglesSize;

        public void UpdateAtlasBoundingBox()
        {
            _isFirstAddPoint = true;
            foreach (var uvw in uvwtriangles)
            {
                // 三个顶点的uv
                AddPoint(uvw.a.x, uvw.a.y);
                AddPoint(uvw.b.x, uvw.b.y);
                AddPoint(uvw.c.x, uvw.c.y);
            }

            CropAtlasBoundingBox();
        }

        private bool _isFirstAddPoint = true;
        public void AddPoint(float u, float v)
        {
            if (_isFirstAddPoint)
            {
                // 初始化为当前点位置，并设置极小宽度/高度
                atlas_bbox.x = u;
                atlas_bbox.y = v;
                atlas_bbox.width = float.Epsilon;
                atlas_bbox.height = float.Epsilon;
                _isFirstAddPoint = false;
                return;
            }

            // 计算新的边界
            float minX = Mathf.Min(atlas_bbox.x, u);
            float maxX = Mathf.Max(atlas_bbox.x + atlas_bbox.width, u);
            float minY = Mathf.Min(atlas_bbox.y, v);
            float maxY = Mathf.Max(atlas_bbox.y + atlas_bbox.height, v);

            // 更新包围盒
            atlas_bbox.x = minX;
            atlas_bbox.y = minY;
            atlas_bbox.width = maxX - minX;
            atlas_bbox.height = maxY - minY;
        }

        void CropAtlasBoundingBox()
        {
            // 把UV裁剪到0,0,1,1的范围
            atlas_bbox.x = Mathf.Max(atlas_bbox.x, 0);
            atlas_bbox.y = Mathf.Max(atlas_bbox.y, 0);
            var right_bottom_x = Mathf.Min(atlas_bbox.x + atlas_bbox.width, 1);
            atlas_bbox.width = right_bottom_x - atlas_bbox.x;
            var right_bottom_y = Mathf.Min(atlas_bbox.y + atlas_bbox.height, 1);
            atlas_bbox.height = right_bottom_y - atlas_bbox.y;
        }

        public override string ToString()
        {
            return $"帧数{framenum}, 长度{duration}, 包围盒{bbox}, 顶点数{CountAlphaVerts}, atlas_depth: {atlas_depth}，atlas_bbox: {atlas_bbox}";
        }

        public Vector2 GetPivot()
        {
            return new Vector2(
                0.5f - bbox.x / bbox.width,
                0.5f + bbox.y / bbox.height
            );
        }
    }

    public class Triangle
    {
        public Vector3 a;
        public Vector3 b;
        public Vector3 c;

        public Triangle() { }

        public Triangle(Vector3 a, Vector3 b, Vector3 c)
        {
            this.a = a;
            this.b = b;
            this.c = c;
        }
    }

}
