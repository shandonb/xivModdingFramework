﻿// xivModdingFramework
// Copyright © 2018 Rafael Gonzalez - All Rights Reserved
// 
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with this program.  If not, see <http://www.gnu.org/licenses/>.

using SharpDX;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Tga;
using SixLabors.ImageSharp.PixelFormats;
using System;
using System.Collections.Generic;
using System.Data.Entity.ModelConfiguration.Conventions;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TeximpNet.Compression;
using TeximpNet.DDS;
using xivModdingFramework.Helpers;
using xivModdingFramework.SqPack.FileTypes;
using xivModdingFramework.Textures.DataContainers;
using xivModdingFramework.Textures.Enums;

namespace xivModdingFramework.Textures.FileTypes
{
    /// <summary>
    /// This class deals with dds file types
    /// </summary>
    public static class DDS
    {
        // Flags value indicating DWFourCC has real data.
        internal const uint _DDS_PixelFormatOffset = 76;
        internal const uint _DWFourCCFlag = 0x04;

        internal static uint _DX10 = (uint)BitConverter.ToInt32(Encoding.ASCII.GetBytes("DX10"), 0);
        /// <summary>
        /// A dictionary containing the int representations of known DDS FourCC Values to Xiv Tex Enum
        /// File types not listed here will fall over to DXGI header extension writing using "DX10" and their DxgiTypeToXivTex enum value.
        /// </summary>
        internal static readonly Dictionary<uint, XivTexFormat> DdsTypeToXivTex = new Dictionary<uint, XivTexFormat>
        {
            { (uint)BitConverter.ToInt32(Encoding.ASCII.GetBytes("DXT1"), 0) , XivTexFormat.DXT1 },
            { (uint)BitConverter.ToInt32(Encoding.ASCII.GetBytes("DXT3"), 0) , XivTexFormat.DXT3 },
            { (uint)BitConverter.ToInt32(Encoding.ASCII.GetBytes("DXT5"), 0) , XivTexFormat.DXT5 },
            { (uint)BitConverter.ToInt32(Encoding.ASCII.GetBytes("ATI1"), 0) , XivTexFormat.BC4 },
            { (uint)BitConverter.ToInt32(Encoding.ASCII.GetBytes("BC4U"), 0) , XivTexFormat.BC4 },
            { (uint)BitConverter.ToInt32(Encoding.ASCII.GetBytes("ATI2"), 0) , XivTexFormat.BC5 },
            { (uint)BitConverter.ToInt32(Encoding.ASCII.GetBytes("BC5U"), 0) , XivTexFormat.BC5 },
            { (uint)BitConverter.ToInt32(Encoding.ASCII.GetBytes("BC7L"), 0) , XivTexFormat.BC7 },
            { (uint)BitConverter.ToInt32(Encoding.ASCII.GetBytes("BC7\0"), 0) , XivTexFormat.BC7 },

            // Floating point formats
            { 112, XivTexFormat.G16R16F },
            { 113, XivTexFormat.A16B16G16R16F },
            { 114, XivTexFormat.R32F },
            { 115, XivTexFormat.G32R32F },
            { 116, XivTexFormat.A32B32G32R32F },

            //Uncompressed RGBA
            { 0, XivTexFormat.A8R8G8B8 }
        };

        /// <summary>
        /// A dictionary containing the int representations of known DXGI header extension enum values to Xiv Tex Enum
        /// These are only used if the format does not appear in the previous dictionary (DdsTypeToXivTex) above.
        /// </summary>
        internal static readonly Dictionary<uint, XivTexFormat> DxgiTypeToXivTex = new Dictionary<uint, XivTexFormat>
        {
            {(uint)DDS.DXGI_FORMAT.DXGI_FORMAT_BC1_UNORM, XivTexFormat.DXT1 },
            {(uint)DDS.DXGI_FORMAT.DXGI_FORMAT_BC2_UNORM, XivTexFormat.DXT3 },
            {(uint)DDS.DXGI_FORMAT.DXGI_FORMAT_BC3_UNORM, XivTexFormat.DXT5 },
            {(uint)DDS.DXGI_FORMAT.DXGI_FORMAT_BC4_UNORM, XivTexFormat.BC4 },
            {(uint)DDS.DXGI_FORMAT.DXGI_FORMAT_BC5_UNORM, XivTexFormat.BC5 },
            {(uint)DDS.DXGI_FORMAT.DXGI_FORMAT_BC7_UNORM, XivTexFormat.BC7 },
            {(uint)DDS.DXGI_FORMAT.DXGI_FORMAT_R16G16B16A16_FLOAT, XivTexFormat.A16B16G16R16F },
            {(uint)DDS.DXGI_FORMAT.DXGI_FORMAT_B8G8R8A8_UNORM, XivTexFormat.A8R8G8B8 }
        };

        internal static uint GetDDSType(XivTexFormat format)
        {
            return DdsTypeToXivTex.FirstOrDefault(x => x.Value == format).Key;
        }
        internal static uint GetDxgiType(XivTexFormat format)
        {
            return DxgiTypeToXivTex.FirstOrDefault(x => x.Value == format).Key;
        }

        /// <summary>
        /// Creates a DDS file for the given Texture
        /// </summary>
        /// <param name="saveDirectory">The directory to save the dds file to</param>
        /// <param name="xivTex">The Texture information</param>
        public static void MakeDDS(XivTex xivTex, string savePath)
        {
            var DDS = CreateDDSHeader(xivTex).ToList();

            var data = xivTex.TexData;
            if (xivTex.TextureFormat == XivTexFormat.A8R8G8B8 && xivTex.Layers > 1)
            {
                data = ShiftLayers(data);
            }
            DDS.AddRange(data);

            File.WriteAllBytes(savePath, DDS.ToArray());
        }


        public static byte[] MakeDDS(byte[] data, XivTexFormat format, int width, int height, int layers, int mipCount)
        {
            var DDS = new List<byte>();
            DDS.AddRange(CreateDDSHeader(format, width, height, layers, mipCount));

            if (format == XivTexFormat.A8R8G8B8 && layers > 1)
            {
                data = ShiftLayers(data);
            }
            DDS.AddRange(data);

            return DDS.ToArray();
        }

        // This is a simple shift of the layers around in order to convert ARGB to RGBA
        private static byte[] ShiftLayers(byte[] data)
        {
            for(int i = 0; i < data.Length; i += 4)
            {
                var alpha = data[i];
                var red = data[i + 1];
                var green  = data[i + 2];
                var blue = data[i + 3];

                data[i] = red;
                data[i + 1] = green;
                data[i + 2] = blue;
                data[i + 3] = alpha;
            }
            return data;
        }

        /// <summary>
        /// Creates the DDS header for given texture data.
        /// <see cref="https://msdn.microsoft.com/en-us/library/windows/desktop/bb943982(v=vs.85).aspx"/>
        /// </summary>
        /// <returns>Byte array containing DDS header</returns>
        private static byte[] CreateDDSHeader(XivTex xivTex)
        {
            return CreateDDSHeader(xivTex.TextureFormat, xivTex.Width, xivTex.Height, xivTex.Layers, xivTex.MipMapCount);
        }


        private static byte[] CreateDDSHeader(XivTexFormat format, int width, int height, int layers, int mipCount)
        {
            var header = new List<byte>();

            if(layers <= 0)
            {
                layers = 1;
            }

            // DDS header magic number
            const uint dwMagic = 0x20534444;
            header.AddRange(BitConverter.GetBytes(dwMagic));

            // Size of structure. This member must be set to 124.
            const uint dwSize = 124;
            header.AddRange(BitConverter.GetBytes(dwSize));

            // Flags to indicate which members contain valid data.
            uint dwFlags = 0x21007; // DDSD_MIPMAPCOUNT | DDSD_PIXELFORMAT | DDSD_WIDTH | DDSD_HEIGHT | DDSD_CAPS

            // Surface height (in pixels). 
            var dwHeight = (uint)height;

            // Surface width (in pixels).
            var dwWidth = (uint)width;

            // The pitch or number of bytes per scan line in an uncompressed texture; the total number of bytes in the top level texture for a compressed texture.
            uint dwPitchOrLinearSize;

            switch (format)
            {
                case XivTexFormat.DXT1:
                case XivTexFormat.BC4:
                    dwPitchOrLinearSize = dwHeight * dwWidth / 2;
                    dwFlags |= 0x80000; // DDSD_LINEARSIZE
                    break;
                case XivTexFormat.DXT3:
                case XivTexFormat.DXT5:
                case XivTexFormat.BC5:
                case XivTexFormat.BC7:
                    dwPitchOrLinearSize = dwHeight * dwWidth;
                    dwFlags |= 0x80000; // DDSD_LINEARSIZE
                    break;
                case XivTexFormat.L8:
                case XivTexFormat.A8:
                    dwPitchOrLinearSize = dwWidth;
                    dwFlags |= 0x8; // DDSD_PITCH
                    break;
                case XivTexFormat.A4R4G4B4:
                case XivTexFormat.A1R5G5B5:
                case XivTexFormat.D16:
                    dwPitchOrLinearSize = dwWidth * 2;
                    dwFlags |= 0x8; // DDSD_PITCH
                    break;
                case XivTexFormat.A8R8G8B8:
                case XivTexFormat.X8R8G8B8:
                case XivTexFormat.R32F:
                case XivTexFormat.G16R16F:
                    dwPitchOrLinearSize = dwWidth * 4;
                    dwFlags |= 0x8; // DDSD_PITCH
                    break;
                case XivTexFormat.G32R32F:
                case XivTexFormat.A16B16G16R16F:
                    dwPitchOrLinearSize = dwWidth * 8;
                    dwFlags |= 0x8; // DDSD_PITCH
                    break;
                case XivTexFormat.A32B32G32R32F:
                    dwPitchOrLinearSize = dwWidth * 16;
                    dwFlags |= 0x8; // DDSD_PITCH
                    break;
                case XivTexFormat.INVALID:
                default:
                    throw new InvalidDataException("DDS Writer does not know how to write TexFormat: " + format.ToString());
            }

            header.AddRange(BitConverter.GetBytes(dwFlags));
            header.AddRange(BitConverter.GetBytes(dwHeight));
            header.AddRange(BitConverter.GetBytes(dwWidth));
            header.AddRange(BitConverter.GetBytes(dwPitchOrLinearSize));

            // Depth of a volume texture (in pixels), otherwise unused.
            const uint dwDepth = 0;
            header.AddRange(BitConverter.GetBytes(dwDepth));

            // Number of mipmap levels.
            var dwMipMapCount = (uint)mipCount;
            header.AddRange(BitConverter.GetBytes(dwMipMapCount));

            // Unused.
            var dwReserved1 = new byte[44];
            Array.Clear(dwReserved1, 0, 44);
            header.AddRange(dwReserved1);

            // DDS_PIXELFORMAT start

            // Structure size; set to 32 (bytes).
            const uint pfSize = 32;
            header.AddRange(BitConverter.GetBytes(pfSize));

            // Compressed and floating point formats will be identified by a FourCC code
            uint dwFourCC = GetDDSType(format);

            uint pfFlags = 0;
            uint dwRGBBitCount = 0;
            uint dwRBitMask = 0;
            uint dwGBitMask = 0;
            uint dwBBitMask = 0;
            uint dwABitMask = 0;
            uint dwCaps = 0x1000; // DDSCAPS_TEXTURE

            // Multi-layer images must use the DX10 extension
            if (layers > 1)
                dwFourCC = _DX10;

            // Use DX10 header for BC7 for better compatibility
            if (format == XivTexFormat.BC7)
                dwFourCC = _DX10;

            // Define the pixel layout for uncompressed integer formats
            switch (format)
            {
                case XivTexFormat.A8R8G8B8:
                    pfFlags |= 0x41; // DDPF_RGB | DDPF_ALPHAPIXELS
                    dwRGBBitCount = 32;
                    dwRBitMask = 0xFF0000;
                    dwGBitMask = 0xFF00;
                    dwBBitMask = 0xFF;
                    dwABitMask = 0xFF000000;
                    break;
                case XivTexFormat.X8R8G8B8:
                    pfFlags |= 0x40; // DDPF_RGB
                    dwRGBBitCount = 32;
                    dwRBitMask = 0xFF0000;
                    dwGBitMask = 0xFF00;
                    dwBBitMask = 0xFF;
                    break;
                case XivTexFormat.A4R4G4B4:
                    pfFlags |= 0x41; // DDPF_RGB | DDPF_ALPHAPIXELS
                    dwRGBBitCount = 16;
                    dwRBitMask = 0b0000111100000000;
                    dwGBitMask = 0b0000000011110000;
                    dwBBitMask = 0b0000000000001111;
                    dwABitMask = 0b1111000000000000;
                    break;
                case XivTexFormat.A1R5G5B5:
                    pfFlags |= 0x41; // DDPF_RGB | DDPF_ALPHAPIXELS
                    dwRGBBitCount = 16;
                    dwRBitMask = 0b0111110000000000;
                    dwGBitMask = 0b0000001111100000;
                    dwBBitMask = 0b0000000000011111;
                    dwABitMask = 0b1000000000000000;
                    break;
                case XivTexFormat.L8:
                    pfFlags |= 0x200000; // DDPF_LUMINANCE
                    dwRGBBitCount = 8;
                    dwRBitMask = 0xFF;
                    break;
                case XivTexFormat.A8:
                    pfFlags |= 0x02; // DDPF_ALPHA
                    dwRGBBitCount = 8;
                    dwABitMask = 0xFF;
                    break;
                default:
                    if (dwFourCC == 0)
                        throw new InvalidDataException("DDS Writer does not know how to write TexFormat: " + format.ToString());
                    break;
            }

            if (dwFourCC != 0)
                pfFlags = 0x04; // DDPF_FOURCC

            if (mipCount > 1)
                dwCaps |= 0x400000; // DDSCAPS_MIPMAP

            header.AddRange(BitConverter.GetBytes(pfFlags));
            header.AddRange(BitConverter.GetBytes(dwFourCC));
            header.AddRange(BitConverter.GetBytes(dwRGBBitCount));
            header.AddRange(BitConverter.GetBytes(dwRBitMask));
            header.AddRange(BitConverter.GetBytes(dwGBitMask));
            header.AddRange(BitConverter.GetBytes(dwBBitMask));
            header.AddRange(BitConverter.GetBytes(dwABitMask));
            header.AddRange(BitConverter.GetBytes(dwCaps));

            // DDS_PIXELFORMAT End

            // dwCaps2, dwCaps3, dwCaps4, dwReserved2
            header.AddRange(BitConverter.GetBytes(0));
            header.AddRange(BitConverter.GetBytes(0));
            header.AddRange(BitConverter.GetBytes(0));
            header.AddRange(BitConverter.GetBytes(0));

            // Need to write DX10 header for some compressed formats or multi-layer images
            if (dwFourCC == _DX10)
            {
                // DXGI_FORMAT dxgiFormat
                uint dxgiFormat = GetDxgiType(format);
                if (dxgiFormat == 0)
                    throw new InvalidDataException("DDS Writer does not know how to write TexFormat: " + format.ToString());
                header.AddRange(BitConverter.GetBytes(dxgiFormat));

                // D3D10_RESOURCE_DIMENSION resourceDimension
                header.AddRange(BitConverter.GetBytes((int)3)); // DDS_DIMENSION_TEXTURE2D

                // UINT miscFlag
                header.AddRange(BitConverter.GetBytes((int)0));

                // UINT arraySize
                header.AddRange(BitConverter.GetBytes(layers));

                // UINT miscFlags2
                header.AddRange(BitConverter.GetBytes((int)0));
            }

            return header.ToArray();
        }

        // Calculate a sequence of mipmap sizes given a texture format and size
        public static List<int> CalculateMipMapSizes(XivTexFormat format, int width, int height)
        {
            var offsets = new List<int>();

            int minDimension = format.GetMipMinDimension();
            int mipBitsPerPixel = format.GetBitsPerPixel();
            int mipWidth = width;
            int mipHeight = height;
            int mipLength = Math.Max(minDimension, mipWidth) * Math.Max(minDimension, mipHeight) * mipBitsPerPixel / 8;

            offsets.Add(mipLength);

            while (mipWidth > 1 || mipHeight > 1)
            {
                mipWidth = Math.Max(1, mipWidth / 2);
                mipHeight = Math.Max(1, mipHeight / 2);
                mipLength = Math.Max(minDimension, mipWidth) * Math.Max(minDimension, mipHeight) * mipBitsPerPixel / 8;
                offsets.Add(mipLength);
            }

            return offsets;
        }

        /// <summary>
        /// Compresses a normal DDS File into just the core image data that FFXIV stores.
        /// This assumes the Binary Reader is already positioned at the end of whatever header there is (.DDS or .Tex)
        /// </summary>
        /// <param name="br">The currently active BinaryReader.</param>
        /// <param name="newWidth">The width of the DDS texture to be imported.</param>
        /// <param name="newHeight">The height of the DDS texture to be imported.</param>
        /// <param name="newMipCount">The number of mipmaps the DDS texture to be imported contains.</param>
        /// <returns>A List structure of the resulting binary data keyed on [MipMap#] => Compressed Data Parts</returns>
        public static async Task<List<List<byte[]>>> CompressDDSBody(BinaryReader br, XivTexFormat format, int newWidth, int newHeight, int newMipCount)
        {
            var ddsParts = new List<List<byte[]>>();

            var mipSizes = CalculateMipMapSizes(format, newWidth, newHeight);

            if (mipSizes.Count < newMipCount)
                throw new InvalidDataException($"CompressDDSBody: newMipCount ({newMipCount}) is too high for texture ({newWidth}x{newHeight}, format={format})");

            // Queue all the compression tasks.
            var totalBytesRead = 0;
            var mipTasks = new List<Task<List<byte[]>>>();
            for (var i = 0; i < newMipCount; i++)
            {
                var uncompBytes = br.ReadBytes(mipSizes[i]);
                totalBytesRead += uncompBytes.Length;
                mipTasks.Add(Dat.CompressData(uncompBytes.ToList()));
            }

            // Wait for them to finish.
            await Task.WhenAll(mipTasks);
            foreach(var task in mipTasks)
            {
                ddsParts.Add(task.Result);
            }


            // Return parts.
            return ddsParts;
        }

        #region Pixel Conversion Functions

        /// <summary>
        /// Converts the given DDS Compressed pixel data into 8.8.8.8 RGBA pixel data.
        /// </summary>
        /// <param name="DdsCompressedPixelData"></param>
        /// <param name="width"></param>
        /// <param name="height"></param>
        /// <param name="DDSFormat"></param>
        /// <returns></returns>
        public static async Task<byte[]> ConvertPixelData(byte[] DdsCompressedPixelData, int width, int height, XivTexFormat DDSFormat, int layers = 1, int targetLayer = -1)
        {
            return await Task.Run(async () =>
            {
                byte[] imageData = null;
                if (layers == 0)
                {
                    layers = 1;
                }

                switch (DDSFormat)
                {
                    case XivTexFormat.DXT1:
                        imageData = DxtUtil.DecompressDxt1(DdsCompressedPixelData, width, height * layers);
                        break;
                    case XivTexFormat.DXT3:
                        imageData = DxtUtil.DecompressDxt3(DdsCompressedPixelData, width, height * layers);
                        break;
                    case XivTexFormat.DXT5:
                        imageData = DxtUtil.DecompressDxt5(DdsCompressedPixelData, width, height * layers);
                        break;
                    case XivTexFormat.BC4:
                        imageData = DxtUtil.DecompressBc4(DdsCompressedPixelData, width, height * layers);
                        break;						
                    case XivTexFormat.BC5:
                        imageData = DxtUtil.DecompressBc5(DdsCompressedPixelData, width, height * layers);
                        break;
                    case XivTexFormat.BC7:
                        imageData = DxtUtil.DecompressBc7(DdsCompressedPixelData, width, height * layers);
                        break;
                    case XivTexFormat.A4R4G4B4:
                        imageData = await Read4444Image(DdsCompressedPixelData, width, height * layers);
                        break;
                    case XivTexFormat.A1R5G5B5:
                        imageData = await Read5551Image(DdsCompressedPixelData, width, height * layers);
                        break;
                    case XivTexFormat.A8R8G8B8:
                        imageData = await SwapRBColors(DdsCompressedPixelData, width, height * layers);
                        break;
                    case XivTexFormat.L8:
                    case XivTexFormat.A8:
                        imageData = await Read8bitImage(DdsCompressedPixelData, width, height * layers);
                        break;
                    case XivTexFormat.A16B16G16R16F:
                        imageData = await ReadHalfFloatImage(DdsCompressedPixelData, width, height * layers);
                        break;
                    case XivTexFormat.X8R8G8B8:
                    case XivTexFormat.R32F:
                    case XivTexFormat.G16R16F:
                    case XivTexFormat.G32R32F:
                    case XivTexFormat.A32B32G32R32F:
                    case XivTexFormat.D16:
                    default:
                        imageData = DdsCompressedPixelData;
                        break;
                }

                if (targetLayer >= 0)
                {
                    var bytesPerLayer = imageData.Length / layers;
                    var offset = bytesPerLayer * targetLayer;

                    byte[] nData = new byte[bytesPerLayer];
                    Array.Copy(imageData, offset, nData, 0, bytesPerLayer);

                    imageData = nData;
                }

                return imageData;
            });
        }

        /// <summary>
        /// Creates bitmap from decompressed A1R5G5B5 texture data.
        /// </summary>
        /// <param name="textureData">The decompressed texture data.</param>
        /// <param name="width">The textures width.</param>
        /// <param name="height">The textures height.</param>
        /// <returns>The raw byte data in 32bit</returns>
        internal static async Task<byte[]> Read5551Image(byte[] textureData, int width, int height)
        {
            var convertedBytes = new List<byte>();

            await Task.Run(() =>
            {
                using (var ms = new MemoryStream(textureData))
                {
                    using (var br = new BinaryReader(ms))
                    {
                        for (var y = 0; y < height; y++)
                        {
                            for (var x = 0; x < width; x++)
                            {
                                var pixel = br.ReadUInt16() & 0xFFFF;

                                var red = ((pixel & 0x7E00) >> 10) * 8;
                                var green = ((pixel & 0x3E0) >> 5) * 8;
                                var blue = ((pixel & 0x1F)) * 8;
                                var alpha = ((pixel & 0x8000) >> 15) * 255;

                                convertedBytes.Add((byte)red);
                                convertedBytes.Add((byte)green);
                                convertedBytes.Add((byte)blue);
                                convertedBytes.Add((byte)alpha);
                            }
                        }
                    }
                }
            });

            return convertedBytes.ToArray();
        }


        /// <summary>
        /// Creates bitmap from decompressed A4R4G4B4 texture data.
        /// </summary>
        /// <param name="textureData">The decompressed texture data.</param>
        /// <param name="width">The textures width.</param>
        /// <param name="height">The textures height.</param>
        /// <returns>The raw byte data in 32bit</returns>
        internal static async Task<byte[]> Read4444Image(byte[] textureData, int width, int height)
        {
            var convertedBytes = new List<byte>();

            await Task.Run(() =>
            {
                using (var ms = new MemoryStream(textureData))
                {
                    using (var br = new BinaryReader(ms))
                    {
                        for (var y = 0; y < height; y++)
                        {
                            for (var x = 0; x < width; x++)
                            {
                                var pixel = br.ReadUInt16() & 0xFFFF;
                                var red = ((pixel & 0xF)) * 16;
                                var green = ((pixel & 0xF0) >> 4) * 16;
                                var blue = ((pixel & 0xF00) >> 8) * 16;
                                var alpha = ((pixel & 0xF000) >> 12) * 16;

                                convertedBytes.Add((byte)blue);
                                convertedBytes.Add((byte)green);
                                convertedBytes.Add((byte)red);
                                convertedBytes.Add((byte)alpha);
                            }
                        }
                    }
                }
            });

            return convertedBytes.ToArray();
        }

        /// <summary>
        /// Creates bitmap from decompressed A8/L8 texture data.
        /// </summary>
        /// <param name="textureData">The decompressed texture data.</param>
        /// <param name="width">The textures width.</param>
        /// <param name="height">The textures height.</param>
        /// <returns>The created bitmap.</returns>
        internal static async Task<byte[]> Read8bitImage(byte[] textureData, int width, int height)
        {
            var convertedBytes = new List<byte>();

            await Task.Run(() =>
            {
                using (var ms = new MemoryStream(textureData))
                {
                    using (var br = new BinaryReader(ms))
                    {
                        for (var y = 0; y < height; y++)
                        {
                            for (var x = 0; x < width; x++)
                            {
                                var pixel = br.ReadByte() & 0xFF;

                                convertedBytes.Add((byte)pixel);
                                convertedBytes.Add((byte)pixel);
                                convertedBytes.Add((byte)pixel);
                                convertedBytes.Add(255);
                            }
                        }
                    }
                }
            });

            return convertedBytes.ToArray();
        }

        internal static async Task<byte[]> ReadHalfFloatImage(byte[] textureData, int width, int height)
        {
            var convertedBytes = new List<byte>();

            await Task.Run(async () =>
            {
                using (var ms = new MemoryStream(textureData))
                {
                    using (var br = new BinaryReader(ms))
                    {
                        for (var y = 0; y < height; y++)
                        {
                            for (var x = 0; x < width; x++)
                            {
                                var r = new SharpDX.Half(br.ReadUInt16());
                                var g = new SharpDX.Half(br.ReadUInt16());
                                var b = new SharpDX.Half(br.ReadUInt16());
                                var a = new SharpDX.Half(br.ReadUInt16());

                                // 255 * value, clamped to 0-255.
                                var byteR = (byte)Math.Max(0, Math.Min(255, Math.Round(r * 255.0f)));
                                var byteG = (byte)Math.Max(0, Math.Min(255, Math.Round(g * 255.0f)));
                                var byteB = (byte)Math.Max(0, Math.Min(255, Math.Round(b * 255.0f)));
                                var byteA = (byte)Math.Max(0, Math.Min(255, Math.Round(a * 255.0f)));

                                convertedBytes.Add(byteR);
                                convertedBytes.Add(byteG);
                                convertedBytes.Add(byteB);
                                convertedBytes.Add(byteA);
                            }
                        }
                    }
                }
            });

            return convertedBytes.ToArray();
        }

        /// <summary>
        /// Creates bitmap from decompressed Linear texture data.
        /// </summary>
        /// <param name="textureData">The decompressed texture data.</param>
        /// <param name="width">The textures width.</param>
        /// <param name="height">The textures height.</param>
        /// <returns>The raw byte data in 32bit</returns>
        internal static async Task<byte[]> SwapRBColors(byte[] textureData, int width, int height)
        {
            var data = new byte[width * height * 4];
            await TextureHelpers.ModifyPixels((int offset) =>
            {
                data[offset + 0] = textureData[offset + 2];
                data[offset + 1] = textureData[offset + 1];
                data[offset + 2] = textureData[offset + 0];
                data[offset + 3] = textureData[offset + 3];
            }, width, height);

            return data;
        }
        #endregion


        public static async Task<byte[]> TexConvRawPixels(byte[] rgbaData, int width, int height, string format, bool generateMipMaps = false, bool omitHeader = true, bool bgra = false)
        {
            return await Task.Run(async () =>
            {
                // ImageSharp => TexConv Route.
                var ddsFile = await DDS.TexConv(rgbaData, width, height, format, generateMipMaps, bgra);
                try
                {
                    using (var fs = File.OpenRead(ddsFile))
                    {
                        using (var br = new BinaryReader(fs))
                        {
                            if (omitHeader)
                            {
                                br.BaseStream.Seek(148, SeekOrigin.Begin);
                            }
                            return br.ReadAllBytes();
                        }
                    }
                }
                finally
                {
                    IOUtil.DeleteTempFile(ddsFile);
                }
            });
        }

        /// <summary>
        /// Takes uncompressed RGBA data and uses TexConv.exe to convert it into a DDS file in the temp directory.
        /// </summary>
        /// <param name="rgbaData"></param>
        /// <param name="width"></param>
        /// <param name="height"></param>
        /// <param name="format"></param>
        /// <param name="generateMipMaps"></param>
        /// <returns></returns>
        public static async Task<string> TexConv(byte[] rgbaData, int width, int height, string format, bool generateMipMaps = false, bool bgra = false)
        {
            var tmpDir = IOUtil.GetFrameworkTempFolder();
            Directory.CreateDirectory(tmpDir);
            var input = Path.Combine(tmpDir, Guid.NewGuid().ToString() + ".tga");

            
            if(bgra)
            {
                using (var img = Image.LoadPixelData<Bgra32>(rgbaData, width, height))
                {
                    var encoder = new TgaEncoder
                    {
                        Compression = TgaCompression.None,
                        BitsPerPixel = TgaBitsPerPixel.Pixel32
                    };
                    img.SaveAsTga(input, encoder);
                }
            }
            else
            {
                using (var img = Image.LoadPixelData<Rgba32>(rgbaData, width, height))
                {
                    var encoder = new TgaEncoder
                    {
                        Compression = TgaCompression.None,
                        BitsPerPixel = TgaBitsPerPixel.Pixel32
                    };
                    img.SaveAsTga(input, encoder);
                }
            }

            try
            {
                return await TexConv(input, format, generateMipMaps);
            }
            finally
            {
                IOUtil.DeleteTempFile(input);
            }
        }


        public static async Task<string> TexConv(string file, string format, bool generateMipMaps = true)
        {
            // TexConv does not handle PNGs correctly.  Rip the raw bytes ourselves and resave as TGA.
            if (file.ToLower().EndsWith(".png"))
            {
                var decoder = PngDecoder.Instance;
                byte[] data;
                int width, height;

                using (var stream = File.OpenRead(file))
                {
                    var options = new DecoderOptions { Configuration = SixLabors.ImageSharp.Configuration.Default };

                    using (var img = Image.Load<Rgba32>(options, stream))
                    {
                        width = img.Width;
                        height = img.Height;
                        data = IOUtil.GetImageSharpPixels(img);
                    }
                }
                // TexConv does not handle PNG reading correctly.
                return await TexConv(data, width, height, format, generateMipMaps);
            }

            return await Task.Run(() =>
            {

                var cwd = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location);
                var workingDirectory = Path.Combine(cwd, "converters");
                var converter = Path.Combine(workingDirectory, "texconv.exe");

                var mipArg = "-m " + (generateMipMaps ? 0 : 1);
                var formatArg = "-f " + format;


                var guid = Guid.NewGuid().ToString();
                var input = guid + Path.GetExtension(file);
                var output = guid + ".dds";
                var inFull = Path.Combine(workingDirectory, input);

                //var bcq = "-bc q";
                var bcq = "-bc q";

                File.Copy(file, inFull);
                try
                {

                    var outFile = Path.Combine(workingDirectory, output);
                    var tmpDir = IOUtil.GetFrameworkTempFolder();
                    Directory.CreateDirectory(tmpDir);
                    var outTemp = Path.Combine(tmpDir, output);

                    var args = $"{formatArg} {mipArg} {bcq} -tgazeroalpha -sepalpha -y {input}";

                    var proc = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName = converter,
                            Arguments = args,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            UseShellExecute = false,
                            CreateNoWindow = true,
                            WorkingDirectory = workingDirectory,
                        }
                    };


                    proc.Start();
                    proc.WaitForExit();
                    var code = proc.ExitCode;


                    var command = converter + " " + args;

                    if (code != 0)
                    {
                        throw new Exception("TexConv.exe threw error code: " + proc.ExitCode);
                    }

                    File.Move(outFile, outTemp);

                    return outTemp;
                }
                finally
                {
                    File.Delete(inFull);
                }
            });
        }

        public enum DXGI_FORMAT : uint
        {
            DXGI_FORMAT_UNKNOWN,
            DXGI_FORMAT_R32G32B32A32_TYPELESS,
            DXGI_FORMAT_R32G32B32A32_FLOAT,
            DXGI_FORMAT_R32G32B32A32_UINT,
            DXGI_FORMAT_R32G32B32A32_SINT,
            DXGI_FORMAT_R32G32B32_TYPELESS,
            DXGI_FORMAT_R32G32B32_FLOAT,
            DXGI_FORMAT_R32G32B32_UINT,
            DXGI_FORMAT_R32G32B32_SINT,
            DXGI_FORMAT_R16G16B16A16_TYPELESS,
            DXGI_FORMAT_R16G16B16A16_FLOAT,
            DXGI_FORMAT_R16G16B16A16_UNORM,
            DXGI_FORMAT_R16G16B16A16_UINT,
            DXGI_FORMAT_R16G16B16A16_SNORM,
            DXGI_FORMAT_R16G16B16A16_SINT,
            DXGI_FORMAT_R32G32_TYPELESS,
            DXGI_FORMAT_R32G32_FLOAT,
            DXGI_FORMAT_R32G32_UINT,
            DXGI_FORMAT_R32G32_SINT,
            DXGI_FORMAT_R32G8X24_TYPELESS,
            DXGI_FORMAT_D32_FLOAT_S8X24_UINT,
            DXGI_FORMAT_R32_FLOAT_X8X24_TYPELESS,
            DXGI_FORMAT_X32_TYPELESS_G8X24_UINT,
            DXGI_FORMAT_R10G10B10A2_TYPELESS,
            DXGI_FORMAT_R10G10B10A2_UNORM,
            DXGI_FORMAT_R10G10B10A2_UINT,
            DXGI_FORMAT_R11G11B10_FLOAT,
            DXGI_FORMAT_R8G8B8A8_TYPELESS,
            DXGI_FORMAT_R8G8B8A8_UNORM,
            DXGI_FORMAT_R8G8B8A8_UNORM_SRGB,
            DXGI_FORMAT_R8G8B8A8_UINT,
            DXGI_FORMAT_R8G8B8A8_SNORM,
            DXGI_FORMAT_R8G8B8A8_SINT,
            DXGI_FORMAT_R16G16_TYPELESS,
            DXGI_FORMAT_R16G16_FLOAT,
            DXGI_FORMAT_R16G16_UNORM,
            DXGI_FORMAT_R16G16_UINT,
            DXGI_FORMAT_R16G16_SNORM,
            DXGI_FORMAT_R16G16_SINT,
            DXGI_FORMAT_R32_TYPELESS,
            DXGI_FORMAT_D32_FLOAT,
            DXGI_FORMAT_R32_FLOAT,
            DXGI_FORMAT_R32_UINT,
            DXGI_FORMAT_R32_SINT,
            DXGI_FORMAT_R24G8_TYPELESS,
            DXGI_FORMAT_D24_UNORM_S8_UINT,
            DXGI_FORMAT_R24_UNORM_X8_TYPELESS,
            DXGI_FORMAT_X24_TYPELESS_G8_UINT,
            DXGI_FORMAT_R8G8_TYPELESS,
            DXGI_FORMAT_R8G8_UNORM,
            DXGI_FORMAT_R8G8_UINT,
            DXGI_FORMAT_R8G8_SNORM,
            DXGI_FORMAT_R8G8_SINT,
            DXGI_FORMAT_R16_TYPELESS,
            DXGI_FORMAT_R16_FLOAT,
            DXGI_FORMAT_D16_UNORM,
            DXGI_FORMAT_R16_UNORM,
            DXGI_FORMAT_R16_UINT,
            DXGI_FORMAT_R16_SNORM,
            DXGI_FORMAT_R16_SINT,
            DXGI_FORMAT_R8_TYPELESS,
            DXGI_FORMAT_R8_UNORM,
            DXGI_FORMAT_R8_UINT,
            DXGI_FORMAT_R8_SNORM,
            DXGI_FORMAT_R8_SINT,
            DXGI_FORMAT_A8_UNORM,
            DXGI_FORMAT_R1_UNORM,
            DXGI_FORMAT_R9G9B9E5_SHAREDEXP,
            DXGI_FORMAT_R8G8_B8G8_UNORM,
            DXGI_FORMAT_G8R8_G8B8_UNORM,
            DXGI_FORMAT_BC1_TYPELESS,
            DXGI_FORMAT_BC1_UNORM,
            DXGI_FORMAT_BC1_UNORM_SRGB,
            DXGI_FORMAT_BC2_TYPELESS,
            DXGI_FORMAT_BC2_UNORM,
            DXGI_FORMAT_BC2_UNORM_SRGB,
            DXGI_FORMAT_BC3_TYPELESS,
            DXGI_FORMAT_BC3_UNORM,
            DXGI_FORMAT_BC3_UNORM_SRGB,
            DXGI_FORMAT_BC4_TYPELESS,
            DXGI_FORMAT_BC4_UNORM,
            DXGI_FORMAT_BC4_SNORM,
            DXGI_FORMAT_BC5_TYPELESS,
            DXGI_FORMAT_BC5_UNORM,
            DXGI_FORMAT_BC5_SNORM,
            DXGI_FORMAT_B5G6R5_UNORM,
            DXGI_FORMAT_B5G5R5A1_UNORM,
            DXGI_FORMAT_B8G8R8A8_UNORM,
            DXGI_FORMAT_B8G8R8X8_UNORM,
            DXGI_FORMAT_R10G10B10_XR_BIAS_A2_UNORM,
            DXGI_FORMAT_B8G8R8A8_TYPELESS,
            DXGI_FORMAT_B8G8R8A8_UNORM_SRGB,
            DXGI_FORMAT_B8G8R8X8_TYPELESS,
            DXGI_FORMAT_B8G8R8X8_UNORM_SRGB,
            DXGI_FORMAT_BC6H_TYPELESS,
            DXGI_FORMAT_BC6H_UF16,
            DXGI_FORMAT_BC6H_SF16,
            DXGI_FORMAT_BC7_TYPELESS,
            DXGI_FORMAT_BC7_UNORM,
            DXGI_FORMAT_BC7_UNORM_SRGB,
            DXGI_FORMAT_AYUV,
            DXGI_FORMAT_Y410,
            DXGI_FORMAT_Y416,
            DXGI_FORMAT_NV12,
            DXGI_FORMAT_P010,
            DXGI_FORMAT_P016,
            DXGI_FORMAT_420_OPAQUE,
            DXGI_FORMAT_YUY2,
            DXGI_FORMAT_Y210,
            DXGI_FORMAT_Y216,
            DXGI_FORMAT_NV11,
            DXGI_FORMAT_AI44,
            DXGI_FORMAT_IA44,
            DXGI_FORMAT_P8,
            DXGI_FORMAT_A8P8,
            DXGI_FORMAT_B4G4R4A4_UNORM,
            DXGI_FORMAT_P208,
            DXGI_FORMAT_V208,
            DXGI_FORMAT_V408,
            DXGI_FORMAT_SAMPLER_FEEDBACK_MIN_MIP_OPAQUE,
            DXGI_FORMAT_SAMPLER_FEEDBACK_MIP_REGION_USED_OPAQUE,
            DXGI_FORMAT_FORCE_UINT
        };
    }
}
