using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace DSTAnimParse
{

    public class AtlasParser
    {
        public static AtlasData Load(string zipPath, string filename, string savePath)
        {
            if (!filename.EndsWith("tex"))
                throw new Exception($"{filename} 不是一个纹理图集，不是.tex文件");

            var ktex = new AtlasData();
            ktex.flipImage(false);
            ktex.savePath = savePath;

            AnimReader reader = AnimReader.GetReader(zipPath, filename);

            ktex.load(reader);

            ktex.Decompress(savePath);

            reader.Close();

            return ktex;
        }
    }

    public class AtlasData
    {
        public string savePath;
        public Header header = new();
        public DSTMipmap[] Mipmaps;
        bool flip_image;

        public void load(AnimReader reader)
        {
            header.load(reader);

            uint mipmap_count = header.getField("mipmap_count");
            reallocateMipmaps(mipmap_count);

            foreach (var item in Mipmaps)
                item.loadPre(reader);

            foreach (var item in Mipmaps)
                item.loadPost(reader);

            if (reader.BaseStream.Position < reader.BaseStream.Length)
                Debug.LogError("警告：tex文件剩余数据");
        }

        void reallocateMipmaps(uint howmany)
        {
            Mipmaps = null;

            header.setField("mipmap_count", howmany);

            if (howmany > 0)
            {
                Mipmaps = new DSTMipmap[howmany];
                for (int i = 0; i < howmany; i++)
                {
                    Mipmaps[i] = new();
                    Mipmaps[i].parent = this;
                }
            }
        }

        public void flipImage(bool b)
        {
            flip_image = b;
        }

        public void Decompress(string savePath)
        {
            var mipmap = Mipmaps[0];
            try
            {
                var fmt = getCompressionFormat();
                // 创建Texture2D并加载原始数据
                // Debug.Log($"mipmap: {mipmap.width}x{mipmap.height}, fmt: {fmt.squish_flags}, count: {mipmap.data.Length}");

                // 创建临时纹理用于转换格式
                Texture2D compressedTex = new Texture2D(mipmap.width, mipmap.height, (TextureFormat)fmt.squish_flags, false);
                compressedTex.LoadRawTextureData(mipmap.data);
                compressedTex.Apply();

                // 创建未压缩纹理并复制像素数据
                Texture2D uncompressedTex = new Texture2D(mipmap.width, mipmap.height, TextureFormat.RGBA32, false);
                // uncompressedTex.SetPixels(compressedTex.GetPixels());
                uncompressedTex.SetPixels32(compressedTex.GetPixels32());
                uncompressedTex.Apply();

                // 编码为PNG并保存
                byte[] pngData = uncompressedTex.EncodeToPNG();

                Directory.CreateDirectory(Path.GetDirectoryName(savePath));

                File.WriteAllBytes(savePath, pngData);
                AssetDatabase.Refresh();

                // Debug.Log($"PNG文件已保存到: {savePath}");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"保存PNG文件失败: {e.Message}");
            }
        }

        void DecompressMipmap(DSTMipmap M, CompressionFormat fmt)
        {
            int width = (int)M.width;
            int height = (int)M.height;

            if (width == 0 || height == 0)
                return;

            if (!fmt.is_uncompressed)
            {

            }
        }

        CompressionFormat getCompressionFormat()
        {
            CompressionFormat fmt = new();
            fmt.squish_flags = getSquishCompressionFlag(ref fmt.is_uncompressed);
            return fmt;
        }

        public struct CompressionFormat
        {
            public bool is_uncompressed;
            public int squish_flags;
        };

        int getSquishCompressionFlag(ref bool isnone)
        {
            isnone = false;
            string internal_flag = header.getFieldString("compression");
            if (internal_flag == "DXT1")
                return (int)TextureFormat.DXT1;
            if (internal_flag == "DXT3")
            {
                // return (int)TextureFormat.DXT3; //没有dxt3啊
            }
            if (internal_flag == "DXT5")
                return (int)TextureFormat.DXT5;
            if (internal_flag == "RGBA")
                return (int)TextureFormat.RGBA32;

            isnone = true;
            return -1;
        }
    }

    public class DSTMipmap
    {
        public AtlasData parent;

        public ushort width; //图集分辨率x
        public ushort height; //图集分辨率y
        public ushort pitch;
        public uint datasz; //图集数据字节个数

        public byte[] data;

        public void loadPre(AnimReader reader)
        {
            // 每个mip级别为10字节，从最高级到最低级
            width = reader.ReadUInt16();
            height = reader.ReadUInt16();
            pitch = reader.ReadUInt16();
            datasz = reader.ReadUInt32();

            // Debug.Log($"检查 width: {width}, height: {height},pitch: {pitch},datasz: {datasz}");
        }

        public void loadPost(AnimReader reader)
        {
            data = reader.ReadBytes((int)datasz);
        }
    }


    public class Header
    {
        public static readonly uint MAGIC_NUMBER = BitConverter.ToUInt32(Encoding.ASCII.GetBytes("KTEX"), 0);

        uint data;

        public void load(AnimReader reader)
        {
            reset();

            uint magic = reader.ReadUInt32();
            if (magic != MAGIC_NUMBER)
                throw new Exception("Attempt to read a non-KTEX file as KTEX.");

            data = reader.ReadUInt32();

            const uint precavesMask = 0xFFFC0000;
            if ((data & precavesMask) == precavesMask)
            {
                convertFromPreCaves();
            }
        }

        void convertFromPreCaves()
        {
            // 解析头文件数据，参考了好几个，都有不对的数据，我都不知道什么格式才是对的，等出问题了再看
            // BitQueue bq = new(data);
            // data = 0;  // 调用setField前重置数据
            // ktools的，compression和texture_type不准
            // setField("platform", bq.Pop(3));
            // setField("compression", bq.Pop(3));
            // setField("texture_type", bq.Pop(3));
            // setField("mipmap_count", bq.Pop(4));
            // setField("flags", bq.Pop(1));
            // setField("fill", bq.Pop(18));

            // texexplorer的，mipmap_count不准
            var header = data;
            data = 0;  // 调用setField前重置数据
            setField("platform", header & 15); //4
            setField("compression", (header >> 4) & 31); //5
            setField("texture_type", (header >> 9) & 15); //
            setField("mipmap_count", (header >> 13) & 31);
            setField("flags", (header >> 18) & 3);
            setField("fill", (header >> 20) & 4095);
        }

        void reset()
        {
            data = 0;
            foreach (var entry in FieldSpecsMap_t.FieldSpecs.M)
                setField(entry.Key, entry.Value.value_default);
        }

        public uint getField(string id)
        {
            var spec = FieldSpecsMap_t.FieldSpecs[id];

            if (!spec.isValid()) return 0;

            return (uint)(((int)data >> (int)spec.offset) & ((1 << (int)spec.length) - 1));
        }

        public void setField(string id, uint val)
        {
            var spec = FieldSpecsMap_t.FieldSpecs[id];

            var mask = ((1 << (int)spec.length) - 1) << (int)spec.offset;

            var maskedval = ((int)spec.normalize_value(val) << (int)spec.offset) & mask;

            data = (data & ~(uint)mask) | (uint)maskedval;
        }

        public string getFieldString(string id)
        {
            var spec = FieldSpecsMap_t.FieldSpecs[id];
            if (!spec.isValid()) return "";
            return spec.normalize_value_inverse(getField(id));
        }
    }

    public class HeaderFieldSpec
    {
        public string id;
        public string name;
        public uint length; // Length, in bits.
        public uint value_default;
        public uint offset; // Offset, in bits (relative to the end of the magic number).

        public Dictionary<string, uint> values = new();
        public Dictionary<uint, string> values_inverse = new();

        public static Dictionary<string, uint> platform_values = new()
    {
        {"Default", 0},
        {"PC", 12},
        {"PS3", 10},
        {"Xbox 360", 11}
    };
        public static Dictionary<string, uint> compression_values = new()
    {
        {"DXT1", 0},
        {"DXT3", 1},
        {"DXT5", 2},
        {"RGBA", 4},
        {"RGB", 5}

        /*
        {"atitc", 8},
        {"atitc_a_e", 9},
        {"atitc_a_i", 10},
        */
    };
        public static Dictionary<string, uint> texturetype_values = new()
    {
        {"1D", 0},
        {"2D", 1},
        {"3D", 2},
        {"Cube Mapped", 3}
    };

        public static HeaderFieldSpec[] fieldspecs = new HeaderFieldSpec[]
        {
        new ("platform", "Platform", 4, platform_values, "DEFAULT"),
        new( "compression", "Compression Type", 5, compression_values, "DXT5" ),
        new( "texture_type", "Texture Type", 4, texturetype_values, "2D" ),
        new( "mipmap_count", "Mipmap Count", 5 ),
        new( "flags", "Flags", 2, (1 << 2) - 1 ),
        new( "fill", "Fill", 12, (1 << 12) - 1 )
        };

        public HeaderFieldSpec(string _id, string _name, uint _length, Dictionary<string, uint> _value_pairs, string value_default_name)
        {
            Construct(_id, _name, _length, _value_pairs, value_default_name);
        }
        public HeaderFieldSpec(string _id, string _name, uint _length, uint _value_default = 0)
        {
            ConstructBasic(_id, _name, _length, _value_default);
        }

        void Construct(string _id, string _name, uint _length, Dictionary<string, uint> _value_pairs, string value_default_name)
        {
            ConstructBasic(_id, _name, _length, 0);
            ConstructValues(_value_pairs);
            value_default = normalize_value(value_default_name);
        }

        void ConstructBasic(string _id, string _name, uint _length, uint _value_default)
        {
            id = _id;
            name = _name;
            length = _length;
            value_default = _value_default;
        }
        void ConstructValues(Dictionary<string, uint> _value_pairs)
        {
            foreach (var entry in _value_pairs)
            {
                values.Add(entry.Key, entry.Value);
                values_inverse.Add(entry.Value, entry.Key);
            }
        }

        public uint normalize_value(string s)
        {
            if (values.TryGetValue(s, out var v))
                return v;
            else
                return value_default;
        }

        public uint normalize_value(uint v)
        {
            return v;
        }

        public bool isValid()
        {
            return !string.IsNullOrEmpty(id);
        }

        public string normalize_value_inverse(string s)
        {
            return s;
        }

        public string normalize_value_inverse(uint v)
        {
            return values_inverse.TryGetValue(v, out var s) ? s : "";
        }
    }

    public class FieldSpecsMap_t
    {
        public static FieldSpecsMap_t FieldSpecs = new(HeaderFieldSpec.fieldspecs);
        const HeaderFieldSpec Invalid = null;

        internal Dictionary<string, HeaderFieldSpec> M = new();
        public List<string> sorted_ids = new();

        internal FieldSpecsMap_t(HeaderFieldSpec[] A)
        {
            uint offset = 0;
            foreach (var item in A)
            {
                item.offset = offset;
                offset += item.length;

                M.Add(item.id, item);
                sorted_ids.Add(item.id);
            }
        }

        internal HeaderFieldSpec this[string id]
        {
            get
            {
                return M.TryGetValue(id, out var val) ? val : Invalid;
            }
        }
    }

    public class BitQueue
    {
        private int data;

        public BitQueue(uint initialData)
        {
            data = (int)initialData;
        }

        // 从队列中弹出指定位数的值
        public uint Pop(int bits)
        {
            int ret;
            if (bits == 32)
            {
                ret = data;
                data = 0;
            }
            else
            {
                ret = data & ((1 << bits) - 1);
                data >>= bits;
                data &= (1 << (8 * 32 - bits)) - 1;
            }
            return (uint)ret;
        }
    }


}