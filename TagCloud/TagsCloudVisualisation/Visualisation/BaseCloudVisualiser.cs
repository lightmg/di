﻿using System;
using System.Drawing;
using System.Linq;

namespace TagsCloudVisualisation.Visualisation
{
    /// <summary>
    /// Provides algorythm to draw cloud step-by-step with automatically image resizing
    /// </summary>
    public abstract class BaseCloudVisualiser
    {
        private readonly Point sourceCenterPoint;
        private Image? image;

        protected BaseCloudVisualiser(Point sourceCenterPoint)
        {
            this.sourceCenterPoint = sourceCenterPoint;
        }

        public Image? GetImage() => (Image) image?.Clone();

        /// <summary>
        /// Graphics instance used to draw on result image
        /// </summary>
        protected Graphics? Graphics { get; private set; }

        /// <summary>
        /// Occurs when algorythm is finished and ready to draw on image.
        /// RectangleF represents final location where image (or something else) need to be placed,
        /// and computed size of it
        /// </summary>
        protected event Action<RectangleF>? OnDraw;

        /// <summary>
        /// Entry point where drawing starts
        /// </summary>
        protected void PrepareAndDraw(RectangleF rectangle)
        {
            rectangle.Location = GetPositionToDraw(rectangle.Location);
            EnsureBitmapSize(rectangle);
            rectangle.Location += image!.Size / 2;
            OnDraw?.Invoke(rectangle);
        }

        private PointF GetPositionToDraw(PointF rectangle) => new PointF(
            rectangle.X - sourceCenterPoint.X,
            rectangle.Y - sourceCenterPoint.Y);

        private void EnsureBitmapSize(RectangleF nextRectangle)
        {
            var newBitmap = EnsureBitmapSize(image, Rectangle.Ceiling(nextRectangle));
            if (newBitmap == image)
                return;
            image = newBitmap;
            Graphics?.Dispose();
            Graphics = Graphics.FromImage(image);
        }

        private static Image EnsureBitmapSize(Image? bitmap, Rectangle nextRectangle)
        {
            if (bitmap == null)
            {
                var xSize = MaxAbs(nextRectangle.Right, nextRectangle.Left) + nextRectangle.Width;
                var ySize = MaxAbs(nextRectangle.Top, nextRectangle.Bottom) + nextRectangle.Height;
                bitmap = new Bitmap(xSize, ySize);
                using var g = Graphics.FromImage(bitmap);
            }
            else
            {
                var halfSize = bitmap.Size / 2;
                var xMaxDistance = MaxAbs(nextRectangle.Left, nextRectangle.Right, halfSize.Width);
                var yMaxDistance = MaxAbs(nextRectangle.Top, nextRectangle.Bottom, halfSize.Height);

                if (halfSize.Width != xMaxDistance || halfSize.Height != yMaxDistance)
                    return ExtendBitmap(bitmap, new Size(xMaxDistance * 2, yMaxDistance * 2));
            }

            return bitmap;
        }

        private static Image ExtendBitmap(Image bitmap, Size newSize)
        {
            var newBitmap = new Bitmap(newSize.Width, newSize.Height);
            using var g = Graphics.FromImage(newBitmap);

            g.DrawImage(bitmap, new Point((newSize - bitmap.Size) / 2));
            return newBitmap;
        }

        private static int MaxAbs(params int[] numbers) => numbers.Select(Math.Abs).Max();
    }
}