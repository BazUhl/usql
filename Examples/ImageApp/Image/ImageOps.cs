﻿using Microsoft.Analytics.Interfaces;
using Microsoft.Analytics.Types.Sql;
using System;
using System.Collections.Generic;
using System.Linq;

using System.IO;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Drawing2D;

namespace Images
{
    // Helper class that binds together a stream and an image so that they can be disposed together.
    // It is needed because the stream must stay open for as long as the image is being used.
    public class StreamImage : IDisposable
    {
        private MemoryStream mMemoryStream;
        public Image mImage;

        public StreamImage(byte[] inBytes)
        {
            mMemoryStream = new MemoryStream(inBytes);
            mImage = null;
            try
            {
                mImage = Image.FromStream(mMemoryStream);
            }
            finally
            {
                if (mImage == null)
                {
                    mMemoryStream.Dispose();
                }
            }
        }

        public void Dispose()
        {
            try
            {
                mImage.Dispose();
            }
            finally
            {
                mMemoryStream.Dispose();
            }
        }

        public string getStreamImageProperty(int propertyId)
        {
            foreach (PropertyItem propItem in mImage.PropertyItems)
            {
                if (propItem.Id == propertyId)
                {
                    return (propItem.Type == 2) ? System.Text.Encoding.UTF8.GetString(propItem.Value) : propItem.Value.ToString();
                }
            }
            return null;
        }

        // Utility: draw the input image to the output image within the given region (at high quality).
        private static void drawImage(Image inImage, Bitmap outImage, Rectangle region)
        {
            outImage.SetResolution(inImage.HorizontalResolution, inImage.VerticalResolution);
            using (Graphics g = Graphics.FromImage(outImage))
            {
                // Clear background pixels, if any will remain after the drawing below.
                if ((region.X != 0) ||
                    (region.Y != 0) ||
                    (region.Height != outImage.Height) ||
                    (region.Width != outImage.Width))
                {
                    g.Clear(Color.Black);
                }
                // Draw image at high quality.
                g.CompositingMode = CompositingMode.SourceCopy;
                g.CompositingQuality = CompositingQuality.HighQuality;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.SmoothingMode = SmoothingMode.HighQuality;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                using (ImageAttributes attributes = new ImageAttributes())
                {
                    attributes.SetWrapMode(WrapMode.TileFlipXY);
                    g.DrawImage(inImage, region, 0, 0, inImage.Width, inImage.Height, GraphicsUnit.Pixel, attributes);
                }
            }
        }

        public Bitmap scaleStreamImageBy(float scaleFactor)
        {
            int outWidth = (int)(mImage.Width * scaleFactor);
            int outHeight = (int)(mImage.Height * scaleFactor);
            using (Bitmap outImage = new Bitmap(outWidth, outHeight))
            {
                drawImage(mImage, outImage, new Rectangle(0, 0, outWidth, outHeight));
                return outImage;
            }
        }
        public Bitmap scaleStreamImageTo(int outWidth, int outHeight)
        {
            int x, y, w, h;
            int iWoH = mImage.Width * outHeight;
            int iHoW = mImage.Height * outWidth;
            if (iWoH < iHoW)
            {
                w = (int)((double)(iWoH) / mImage.Height);
                h = outHeight;
                x = (int)((outWidth - w) / 2.0);
                y = 0;
            }
            else
            {
                w = outWidth;
                h = (int)((double)(iHoW) / mImage.Width);
                x = 0;
                y = (int)((outHeight - h) / 2.0);
            }
            using (Bitmap outImage = new Bitmap(outWidth, outHeight))
            {
                drawImage(mImage, outImage, new Rectangle(x, y, w, h));
                return outImage;
            }
        }

    }

    // Sample image utilities and operations.
    public class ImageOps
    {
        // The quality setting for generated JPEG files.
        private const long JPEG_QUALITY = 90;

        // Utility: convert a byte array into an image.
        private static StreamImage byteArrayToImage(byte[] inBytes)
        {
            return new StreamImage(inBytes);
        }

        // Utility: convert an image into a byte array containing a JPEG encoding of the image.
        private static byte[] imageToByteArray(Image inImage)
        {
            using (MemoryStream outStream = new MemoryStream())
            {
                ImageCodecInfo jpegCodec = null;
                foreach (ImageCodecInfo codec in ImageCodecInfo.GetImageEncoders())
                {
                    if (codec.FormatID == ImageFormat.Jpeg.Guid)
                    {
                        jpegCodec = codec;
                        break;
                    }
                }
                if (jpegCodec == null)
                {
                    throw new MissingMemberException("Cannot find JPEG encoder");
                }
                using (EncoderParameters ep = new EncoderParameters(1))
                {
                    using (EncoderParameter p = new EncoderParameter(Encoder.Quality, JPEG_QUALITY))
                    {
                        ep.Param[0] = p;
                        inImage.Save(outStream, jpegCodec, ep);
                        return outStream.ToArray();
                    }
                }
            }
        }

        // Operation: return the value of an image property (provided it's a string or integer).
        // Use property ID 8298 for copyright.
        // See https://msdn.microsoft.com/en-us/library/ms534416.aspx for additional IDs.
        public static string getImageProperty(byte[] inBytes, int propertyId)
        {
            return byteArrayToImage(inBytes).getStreamImageProperty(propertyId);
        }

        // Operation: rotate an image by 90, 180, or 270 degrees.
        public static byte[] rotateImage(byte[] inBytes, int rotateType)
        {
            RotateFlipType rotateFlipType;
            switch (rotateType)
            {
                case 1:
                    rotateFlipType = RotateFlipType.Rotate90FlipNone;
                    break;
                case 2:
                    rotateFlipType = RotateFlipType.Rotate180FlipNone;
                    break;
                case 3:
                    rotateFlipType = RotateFlipType.Rotate270FlipNone;
                    break;
                default:
                    throw new ArgumentException("Invalid type");
            }
            using (StreamImage inImage = byteArrayToImage(inBytes))
            {
                inImage.mImage.RotateFlip(rotateFlipType);
                return imageToByteArray(inImage.mImage);
            }
        }

        // Operation: scale an image by a scale factor.
        public static byte[] scaleImageBy(byte[] inBytes, float scaleFactor)
        {
            return imageToByteArray(byteArrayToImage(inBytes).scaleStreamImageBy(scaleFactor));
        }

        // Operation: scale an image to the given dimensions.
        public static byte[] scaleImageTo(byte[] inBytes, int outWidth, int outHeight)
        {
            return imageToByteArray( byteArrayToImage(inBytes).scaleStreamImageTo(outWidth, outHeight));
        }


        public static byte[] GetByteArrayforImage(Stream input)
        {
            try
            {
                var image = Image.FromStream(input);
                MemoryStream ms = new MemoryStream();
                image.Save(ms, System.Drawing.Imaging.ImageFormat.Jpeg);
                return ms.ToArray();
            }
            catch (Exception)
            {
                return null;
            }
        }

        public static string[] ConvertTiffToJpeg(string fileName)
        {
            using (Image imageFile = Image.FromFile(fileName))
            {
                FrameDimension frameDimensions = new FrameDimension(
                    imageFile.FrameDimensionsList[0]);

                // Gets the number of pages from the tiff image (if multipage) 
                int frameNum = imageFile.GetFrameCount(frameDimensions);
                string[] jpegPaths = new string[frameNum];

                for (int frame = 0; frame < frameNum; frame++)
                {
                    // Selects one frame at a time and save as jpeg. 
                    imageFile.SelectActiveFrame(frameDimensions, frame);
                    using (Bitmap bmp = new Bitmap(imageFile))
                    {
                        jpegPaths[frame] = String.Format("{0}\\{1}{2}.jpg",
                            Path.GetDirectoryName(fileName),
                            Path.GetFileNameWithoutExtension(fileName),
                            frame);
                        bmp.Save(jpegPaths[frame], ImageFormat.Jpeg);
                    }
                }

                return jpegPaths;
            }

        }
    }
}
