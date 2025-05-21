using System;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace DSTAnimParse
{

    public class AnimReader : BinaryReader
    {
        public bool isBigEndian = false; // 是否是大端序

        public AnimReader(Stream input) : base(input)
        {
        }

        public static AnimReader GetReader(string zipPath, string filename)
        {
            using var archive = ZipFile.OpenRead(zipPath);
            var animEntry = archive.GetEntry(filename);
            if (animEntry == null)
                return null; //没有文件时返回空

            var stream = new MemoryStream();
            using var entryStream = animEntry.Open();
            entryStream.CopyTo(stream);
            stream.Position = 0;
            return new AnimReader(stream);
        }

        // 辅助方法：读取长度前缀字符串
        public string ReadLenString()
        {
            uint length = ReadUInt32();
            byte[] bytes = ReadBytes((int)length);
            return System.Text.Encoding.UTF8.GetString(bytes);
        }

        /// <summary>
        /// 大端序数字和小端序数字相互转换
        /// </summary>
        /// <param name="value">大端序数字或小端序数字</param>
        /// <returns></returns>
        public uint Reorder(uint value)
        {
            return BitConverter.ToUInt32(BitConverter.GetBytes(value).Reverse().ToArray(), 0);
        }

        public int Reorder(int value)
        {
            return BitConverter.ToInt32(BitConverter.GetBytes(value).Reverse().ToArray(), 0);
        }

        public ushort Reorder(ushort value)
        {
            return BitConverter.ToUInt16(BitConverter.GetBytes(value).Reverse().ToArray(), 0);
        }

        public override uint ReadUInt32()
        {
            var value = base.ReadUInt32();
            return isBigEndian ? Reorder(value) : value;
        }

        public override int ReadInt32()
        {
            var value = base.ReadInt32();
            return isBigEndian ? Reorder(value) : value;
        }

        public override ushort ReadUInt16()
        {
            var value = base.ReadUInt16();
            return isBigEndian ? Reorder(value) : value;
        }

        public override float ReadSingle()
        {
            var value = base.ReadSingle();
            if (!isBigEndian)
                return value;

            // 处理大端序浮点数
            byte[] bytes = BitConverter.GetBytes(value);
            if (BitConverter.IsLittleEndian)
                Array.Reverse(bytes);

            return BitConverter.ToSingle(bytes, 0);
        }
    }
}