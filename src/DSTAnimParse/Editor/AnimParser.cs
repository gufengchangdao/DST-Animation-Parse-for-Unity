using System;
using System.Collections.Generic;
using UnityEngine;

namespace DSTAnimParse
{
    public class AnimParser
    {
        // 动画文件常量
        private const uint MAGIC_NUMBER = 0x4D494E41; // "ANIM"的十六进制表示
        private const int MIN_VERSION = 3;
        private const int MAX_VERSION = 4;

        public static AnimData Load(string zipPath)
        {
            AnimReader reader = AnimReader.GetReader(zipPath, "anim.bin");
            if (reader == null)
                return null;

            var animData = new AnimData();
            animData.zipPath = zipPath;

            // 1. 检查魔术数字
            uint magic = reader.ReadUInt32();
            if (magic != MAGIC_NUMBER)
                throw new Exception("无效的anim.bin文件");

            // 2. 读取版本号
            int version = reader.ReadInt32();
            if (version < MIN_VERSION || version > MAX_VERSION)
                throw new Exception($"不支持的动画版本: {version}");

            // 3. 读取基本计数信息
            animData.ElementCount = reader.ReadUInt32();
            animData.FrameCount = reader.ReadUInt32();
            animData.EventCount = reader.ReadUInt32();
            uint animCount = reader.ReadUInt32();

            // 4. 读取动画数据
            animData.Animations = new List<Animation>((int)animCount);
            for (int i = 0; i < animCount; i++)
                animData.Animations.Add(ParseAnimation(reader));

            // 5. 如果有哈希表则读取(版本4+)
            if (version >= 4 && reader.BaseStream.Position < reader.BaseStream.Length)
            {
                animData.HashTable = ParseHashTable(reader);

                foreach (var animation in animData.Animations)
                {
                    animation.Bank = animData.HashTable[animation.BankHash];
                    foreach (var frame in animation.Frames)
                    {
                        foreach (var element in frame.Elements)
                        {
                            element.Name = animData.HashTable[element.Hash];
                            element.Layer = animData.HashTable[element.LayerHash];
                        }
                    }
                }

            }

            reader.Dispose();

            return animData;
        }

        // 解析单个动画
        private static Animation ParseAnimation(AnimReader reader)
        {
            var anim = new Animation
            {
                // 1. 读取动画基本信息
                Name = reader.ReadLenString(),
                FacingByte = reader.ReadByte(),
                BankHash = reader.ReadUInt32(),
                FrameRate = reader.ReadSingle()
            };

            anim.Name += FacingDirectionHelper.GetDirectionSuffix(anim.FacingByte); //根据位标志计算方向后缀

            // 2. 读取帧数据
            uint frameCount = reader.ReadUInt32();
            anim.Frames = new List<KFrame>((int)frameCount);
            for (int i = 0; i < frameCount; i++)
                anim.Frames.Add(ParseFrame(reader));

            return anim;
        }

        // 解析单个帧
        private static KFrame ParseFrame(AnimReader reader)
        {
            var frame = new KFrame
            {
                Bounds = new Rect(
                    // 1. 读取边界框
                    reader.ReadSingle(),
                    reader.ReadSingle(),
                    reader.ReadSingle(),
                    reader.ReadSingle())
            };

            // 2. 读取事件
            uint eventCount = reader.ReadUInt32();
            frame.Events = new Dictionary<uint, string>((int)eventCount);
            for (int i = 0; i < eventCount; i++)
            {
                uint eventHash = reader.ReadUInt32();
                frame.Events[eventHash] = $"event_{eventHash:X8}"; // 默认事件名
            }

            // 3. 读取元素
            uint elementCount = reader.ReadUInt32();
            frame.Elements = new List<KElement>((int)elementCount);
            for (int i = 0; i < elementCount; i++)
            {
                frame.Elements.Add(ParseElement(reader));
            }

            return frame;
        }

        // 解析元素
        private static KElement ParseElement(AnimReader reader)
        {
            var element = new KElement();

            // 1. 读取基本属性
            element.Hash = reader.ReadUInt32();
            element.BuildFrame = reader.ReadInt32();
            element.LayerHash = reader.ReadUInt32();

            // 2. 读取变换矩阵
            element.Transform = new Matrix4x4();
            element.Transform.m00 = reader.ReadSingle();
            element.Transform.m01 = reader.ReadSingle();
            element.Transform.m10 = reader.ReadSingle();
            element.Transform.m11 = reader.ReadSingle();
            element.Transform.m03 = reader.ReadSingle(); // tx
            element.Transform.m13 = reader.ReadSingle(); // ty

            // 3. 读取Z轴深度
            element.Z = reader.ReadSingle();

            return element;
        }

        // 解析哈希表
        private static Dictionary<uint, string> ParseHashTable(AnimReader reader)
        {
            var hashTable = new Dictionary<uint, string>();
            uint tableSize = reader.ReadUInt32();

            for (int i = 0; i < tableSize; i++)
            {
                uint hash = reader.ReadUInt32();
                string name = reader.ReadLenString();
                hashTable[hash] = name;
            }

            return hashTable;
        }
    }

    // 数据结构类
    public class AnimData
    {
        /// <summary>
        /// 动画文件路径
        /// </summary>
        public string zipPath;

        /// <summary>
        /// 元素数
        /// </summary>
        public uint ElementCount;
        /// <summary>
        /// 总帧数
        /// </summary>
        public uint FrameCount;
        /// <summary>
        /// 事件数
        /// </summary>
        public uint EventCount;
        /// <summary>
        /// 所有动画
        /// </summary>
        public List<Animation> Animations;
        /// <summary>
        /// 还原字符串用的哈希表
        /// </summary>
        public Dictionary<uint, string> HashTable;

        public override string ToString()
        {
            return $"动画压缩包: {zipPath}, 元素数: {ElementCount}, 总帧数: {FrameCount}, 事件数: {EventCount}, 动画数: {Animations.Count}";
        }
    }

    public class Animation
    {
        /// <summary>
        /// 动画名
        /// </summary>
        public string Name;
        /// <summary>
        /// 面朝向
        /// </summary>
        public byte FacingByte;
        /// <summary>
        /// bank哈希
        /// </summary>
        public uint BankHash;
        public string Bank;
        /// <summary>
        /// 帧率
        /// </summary>
        public float FrameRate;
        /// <summary>
        /// 帧数据
        /// </summary>
        public List<KFrame> Frames;

        public override string ToString()
        {
            return $"动画名: {Name}, 面朝向: {FacingByte}, bank哈希: {BankHash}, 帧率: {FrameRate}, 帧数: {Frames.Count}";
        }
    }

    public class KFrame
    {
        /// <summary>
        /// 边界框
        /// </summary>
        public Rect Bounds;
        /// <summary>
        /// 事件
        /// </summary>
        public Dictionary<uint, string> Events;
        /// <summary>
        /// 元素数据
        /// </summary>
        public List<KElement> Elements;

        public override string ToString()
        {
            return $"边界框: {Bounds}, 事件数: {Events.Count}, 元素数: {Elements.Count}";
        }
    }

    public class KElement
    {
        public uint Hash;
        public string Name;

        public int BuildFrame = -1;

        public uint LayerHash;
        public string Layer;

        /// <summary>
        /// 变换矩阵
        /// </summary>
        public Matrix4x4 Transform;
        public float Z;

        // 上一次分解的矩阵组件状态
        private float lastScaleX = 1.0f;
        private float lastScaleY = 1.0f;
        private float lastAngle = 0.0f;
        private bool isFirstDecompose = true;

        /// <summary>
        /// TODO 不对
        /// 从变换矩阵分解出缩放、旋转和翻转信息 
        /// </summary>
        public void DecomposeMatrix(out float scaleX, out float scaleY, out float angle, out int spin)
        {
            const float eps = 1e-3f;
            var M = Transform;

            // 计算X和Y轴的缩放值
            scaleX = Mathf.Sqrt(M.m00 * M.m00 + M.m01 * M.m01);
            scaleY = Mathf.Sqrt(M.m10 * M.m10 + M.m11 * M.m11);

            // 计算行列式判断是否需要翻转
            float det = M.m00 * M.m11 - M.m10 * M.m01;
            if (det < 0)
            {
                if (isFirstDecompose || lastScaleX <= lastScaleY)
                {
                    scaleX = -scaleX;
                    isFirstDecompose = false;
                }
                else
                {
                    scaleY = -scaleY;
                }
            }

            // 处理极小缩放值的情况
            if (Mathf.Abs(scaleX) < eps || Mathf.Abs(scaleY) < eps)
            {
                angle = lastAngle;
            }
            else
            {
                // 计算近似旋转角度
                float sin_approx = 0.5f * (M.m10 / scaleY - M.m01 / scaleX);
                float cos_approx = 0.5f * (M.m00 / scaleX + M.m11 / scaleY);
                angle = Mathf.Atan2(sin_approx, cos_approx);
            }

            // 计算旋转方向
            // spin = Mathf.Abs(angle - lastAngle) <= Mathf.PI ? 1 : -1;
            // if (angle < lastAngle)
            // {
            //     spin = -spin;
            // }
            spin = 1;

            // 更新last值
            lastScaleX = scaleX;
            lastScaleY = scaleY;
            lastAngle = angle;
        }

        public override string ToString()
        {
            return $"哈希: {Hash}, 帧数: {BuildFrame}, 层哈希: {LayerHash}, 变换矩阵: \n{Transform} Z: {Z}";
        }
    }

    public class FacingDirectionHelper
    {
        // 方向常量定义（与C++代码一致）
        public const byte FACING_RIGHT = 1 << 0;
        public const byte FACING_UP = 1 << 1;
        public const byte FACING_LEFT = 1 << 2;
        public const byte FACING_DOWN = 1 << 3;
        public const byte FACING_UPRIGHT = 1 << 4;
        public const byte FACING_UPLEFT = 1 << 5;
        public const byte FACING_DOWNRIGHT = 1 << 6;
        public const byte FACING_DOWNLEFT = 1 << 7;

        // 复合方向定义
        public const byte FACING_SIDE = FACING_LEFT | FACING_RIGHT;
        public const byte FACING_UPSIDE = FACING_UPLEFT | FACING_UPRIGHT;
        public const byte FACING_DOWNSIDE = FACING_DOWNLEFT | FACING_DOWNRIGHT;
        public const byte FACING_45S = FACING_UPLEFT | FACING_UPRIGHT | FACING_DOWNLEFT | FACING_DOWNRIGHT;
        public const byte FACING_90S = FACING_UP | FACING_DOWN | FACING_LEFT | FACING_RIGHT;

        /// <summary>
        /// 根据facing_byte获取对应的方向后缀
        /// </summary>
        /// <param name="facingByte">方向字节（如0x01表示朝右）</param>
        /// <returns>对应的后缀字符串（如"_right"）</returns>
        public static string GetDirectionSuffix(byte facingByte)
        {
            return facingByte switch
            {
                FACING_RIGHT => "_right",
                FACING_UP => "_up",
                FACING_LEFT => "_left",
                FACING_DOWN => "_down",
                FACING_UPRIGHT => "_upright",
                FACING_UPLEFT => "_upleft",
                FACING_DOWNRIGHT => "_downright",
                FACING_DOWNLEFT => "_downleft",
                FACING_SIDE => "_side",
                FACING_UPSIDE => "_upside",
                FACING_DOWNSIDE => "_downside",
                FACING_45S => "_45s",
                FACING_90S => "_90s",
                _ => "",// 默认无后缀
            };
        }
    }

}
