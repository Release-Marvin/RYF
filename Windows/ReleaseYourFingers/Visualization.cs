using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.ProjectOxford.Emotion.Contract;
using Microsoft.ProjectOxford.Face.Contract;
using Microsoft.ProjectOxford.Vision.Contract;
using System.Collections;

namespace ReleaseYourFingers
{
    public class Visualization
    {
        private static SolidColorBrush s_lineBrush = new SolidColorBrush(new System.Windows.Media.Color { R = 255, G = 185, B = 0, A = 255 });
        private static Typeface s_typeface = new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);

        private static BitmapSource DrawOverlay(BitmapSource baseImage, Action<DrawingContext, double> drawAction)
        {
            double annotationScale = baseImage.PixelHeight / 320;

            DrawingVisual visual = new DrawingVisual();
            DrawingContext drawingContext = visual.RenderOpen();

            drawingContext.DrawImage(baseImage, new Rect(0, 0, baseImage.Width, baseImage.Height));

            drawAction(drawingContext, annotationScale);

            drawingContext.Close();

            RenderTargetBitmap outputBitmap = new RenderTargetBitmap(
                baseImage.PixelWidth, baseImage.PixelHeight,
                baseImage.DpiX, baseImage.DpiY, PixelFormats.Pbgra32);

            outputBitmap.Render(visual);

            return outputBitmap;
        }

        public static BitmapSource DrawTags(BitmapSource baseImage, Tag[] tags)
        {
            if (tags == null)
            {
                return baseImage;
            }

            Action<DrawingContext, double> drawAction = (drawingContext, annotationScale) =>
            {
                double y = 0;
                foreach (var tag in tags)
                {
                    // Create formatted text--in a particular font at a particular size
                    FormattedText ft = new FormattedText(tag.Name,
                        CultureInfo.CurrentCulture, FlowDirection.LeftToRight, s_typeface,
                        42 * annotationScale, Brushes.Black);
                    // Instead of calling DrawText (which can only draw the text in a solid colour), we
                    // convert to geometry and use DrawGeometry, which allows us to add an outline. 
                    var geom = ft.BuildGeometry(new Point(10 * annotationScale, y));
                    drawingContext.DrawGeometry(s_lineBrush, new Pen(Brushes.Black, 2 * annotationScale), geom);
                    // Move line down
                    y += 42 * annotationScale;
                }
            };

            return DrawOverlay(baseImage, drawAction);
        }

        public static BitmapSource DrawFaces(BitmapSource baseImage, Microsoft.ProjectOxford.Face.Contract.Face[] faces, Scores[] emotionScores, string[] celebName)
        {
            if (faces == null)
            {
                return baseImage;
            }

            Action<DrawingContext, double> drawAction = (drawingContext, annotationScale) =>
            {
                for (int i = 0; i < faces.Length; i++)
                {
                    var face = faces[i];
                    if (face.FaceRectangle == null) { continue; }

                    Rect faceRect = new Rect(
                        face.FaceRectangle.Left, face.FaceRectangle.Top,
                        face.FaceRectangle.Width, face.FaceRectangle.Height);
                    string text = "";

                    if (face.FaceAttributes != null)
                    {
                        text += Aggregation.SummarizeFaceAttributes(face.FaceAttributes);
                    }

                    if (emotionScores?[i] != null)
                    {
                        text += Aggregation.SummarizeEmotion(emotionScores[i]);
                    }

                    if (celebName?[i] != null)
                    {
                        text += celebName[i];
                    }

                    faceRect.Inflate(6 * annotationScale, 6 * annotationScale);

                    double lineThickness = 4 * annotationScale;

                    drawingContext.DrawRectangle(
                        Brushes.Transparent,
                        new Pen(s_lineBrush, lineThickness),
                        faceRect);

                    if (text != "")
                    {
                        FormattedText ft = new FormattedText(text,
                            CultureInfo.CurrentCulture, FlowDirection.LeftToRight, s_typeface,
                            16 * annotationScale, Brushes.Black);

                        var pad = 3 * annotationScale;

                        var ypad = pad;
                        var xpad = pad + 4 * annotationScale;
                        var origin = new Point(
                            faceRect.Left + xpad - lineThickness / 2,
                            faceRect.Top - ft.Height - ypad + lineThickness / 2);
                        var rect = ft.BuildHighlightGeometry(origin).GetRenderBounds(null);
                        rect.Inflate(xpad, ypad);

                        drawingContext.DrawRectangle(s_lineBrush, null, rect);
                        drawingContext.DrawText(ft, origin);
                    }
                }
            };

            return DrawOverlay(baseImage, drawAction);
        }

        public static BitmapSource DrawFaces(BitmapSource baseImage, Microsoft.ProjectOxford.Face.Contract.Face[] faces, Scores[] emotionScores, string[] celebName, ArrayList errorList)
        {
            if (faces == null)
            {
                return baseImage;
            }

            Action<DrawingContext, double> drawAction = (drawingContext, annotationScale) =>
            {
                for (int j = 0; j < errorList.Count; j++)
                {
                    int i = (int)errorList[j];
                    var face = faces[i];
                    if (face.FaceRectangle == null) { continue; }

                    Rect faceRect = new Rect(
                        face.FaceRectangle.Left, face.FaceRectangle.Top,
                        face.FaceRectangle.Width, face.FaceRectangle.Height);
                    string text = "";

                    if (face.FaceAttributes != null)
                    {
                        text += Aggregation.SummarizeFaceAttributes(face.FaceAttributes);
                    }

                    if (emotionScores?[i] != null)
                    {
                        text += Aggregation.SummarizeEmotion(emotionScores[i]);
                    }

                    if (celebName?[i] != null)
                    {
                        text += celebName[i];
                    }

                    faceRect.Inflate(6 * annotationScale, 6 * annotationScale);

                    double lineThickness = 4 * annotationScale;

                    drawingContext.DrawRectangle(
                        Brushes.Transparent,
                        new Pen(s_lineBrush, lineThickness),
                        faceRect);

                    if (text != "")
                    {
                        FormattedText ft = new FormattedText(text,
                            CultureInfo.CurrentCulture, FlowDirection.LeftToRight, s_typeface,
                            16 * annotationScale, Brushes.Black);

                        var pad = 3 * annotationScale;

                        var ypad = pad;
                        var xpad = pad + 4 * annotationScale;
                        var origin = new Point(
                            faceRect.Left + xpad - lineThickness / 2,
                            faceRect.Top - ft.Height - ypad + lineThickness / 2);
                        var rect = ft.BuildHighlightGeometry(origin).GetRenderBounds(null);
                        rect.Inflate(xpad, ypad);

                        drawingContext.DrawRectangle(s_lineBrush, null, rect);
                        drawingContext.DrawText(ft, origin);
                    }
                }
            };

            return DrawOverlay(baseImage, drawAction);
        }
    }
}
