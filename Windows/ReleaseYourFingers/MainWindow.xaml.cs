using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Diagnostics;
using Newtonsoft.Json;
using OpenCvSharp;
using OpenCvSharp.Extensions;
using Microsoft.ProjectOxford.Emotion;
using Microsoft.ProjectOxford.Emotion.Contract;
using Microsoft.ProjectOxford.Face;
using Microsoft.ProjectOxford.Face.Contract;
using Microsoft.ProjectOxford.Vision;
using VideoFrameAnalyzer;
using System.Collections;

namespace ReleaseYourFingers
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : System.Windows.Window
    {
        private EmotionServiceClient _emotionClient = null;
        private FaceServiceClient _faceClient = null;
        private VisionServiceClient _visionClient = null;
        private readonly FrameGrabber<LiveCameraResult> _grabber = null;
        private static readonly ImageEncodingParam[] s_jpegParams = {
            new ImageEncodingParam(ImwriteFlags.JpegQuality, 60)
        };
        private readonly CascadeClassifier _localFaceDetector = new CascadeClassifier();
        private bool _fuseClientRemoteResults;
        private LiveCameraResult _latestResultsToDisplay = null;
        private AppMode _mode;
        private DateTime _startTime;

        private VideoFrame _preFrame = null;
        private LiveCameraResult _preResults = null;
        private bool checkMove = true;
        private bool smile = true;
        private bool start = false;

        public enum AppMode
        {
            Smile,
            SimilarEmotion
        }

        public MainWindow()
        {
            InitializeComponent();

            // Create grabber. 
            _grabber = new FrameGrabber<LiveCameraResult>();

            // Set up a listener for when the client receives a new frame.
            _grabber.NewFrameProvided += (s, e) =>
            {
                if (!start)
                {
                    return;
                }
                // The callback may occur on a different thread, so we must use the
                // MainWindow.Dispatcher when manipulating the UI. 
                this.Dispatcher.BeginInvoke((Action)(() =>
                {
                    // Display the image in the left pane.
                    LeftImage.Source = e.Frame.Image.ToBitmapSource();

                    // If we're fusing client-side face detection with remote analysis, show the
                    // new frame now with the most recent analysis available. 
                    if (_fuseClientRemoteResults)
                    {
                        RightImage.Source = VisualizeResult(e.Frame);
                    }
                }));

                // See if auto-stop should be triggered. 
                if (Properties.Settings.Default.AutoStopEnabled && (DateTime.Now - _startTime) > Properties.Settings.Default.AutoStopTime)
                {
                    _grabber.StopProcessingAsync();
                }
            };

            // Set up a listener for when the client receives a new result from an API call. 
            _grabber.NewResultAvailable += (s, e) =>
            {
                if (!start)
                {
                    return;
                }
                this.Dispatcher.BeginInvoke((Action)(() =>
                {
                    if (e.TimedOut)
                    {
                        MessageArea.Text = "API call timed out.";
                    }
                    else if (e.Exception != null)
                    {
                        string apiName = "";
                        string message = e.Exception.Message;
                        var faceEx = e.Exception as FaceAPIException;
                        var emotionEx = e.Exception as Microsoft.ProjectOxford.Common.ClientException;
                        var visionEx = e.Exception as Microsoft.ProjectOxford.Vision.ClientException;
                        if (faceEx != null)
                        {
                            apiName = "Face";
                            message = faceEx.ErrorMessage;
                        }
                        else if (emotionEx != null)
                        {
                            apiName = "Emotion";
                            message = emotionEx.Error.Message;
                        }
                        else if (visionEx != null)
                        {
                            apiName = "Computer Vision";
                            message = visionEx.Error.Message;
                        }
                        MessageArea.Text = string.Format("{0} API call failed on frame {1}. Exception: {2}", apiName, e.Frame.Metadata.Index, message);
                        Indicator.Fill = Brushes.Red;
                    }
                    else
                    {
                        _latestResultsToDisplay = e.Analysis;

                        // Display the image and visualization in the right pane. 
                        if (!_fuseClientRemoteResults)
                        {
                            ArrayList errorList = new ArrayList();
                            errorList.Add(0);
                            if (checkMove)
                            {
                                bool hasPeople = HasPeople();
                                if (!hasPeople)
                                {
                                    MessageArea.Text = "No Guys！！！";
                                    Indicator.Fill = Brushes.Red;
                                    return;
                                }
                                bool still = IsStill(e.Frame, errorList);
                                if (!still)
                                {
                                    MessageArea.Text = "Please don't move！！！";
                                    System.Media.SystemSounds.Beep.Play();
                                    //Player.Play("C:\\Users\\MarvinCao\\Desktop\\1.mp3");
                                    RightImage.Source = VisualizeResult(e.Frame, errorList);
                                    Indicator.Fill = Brushes.Red;
                                    return;
                                }
                            }
                            bool faceCamera = IsAllFaceCamera(errorList);
                            if (!faceCamera)
                            {
                                MessageArea.Text = "Please see the camera！！！";
                                //System.Media.SystemSounds.Beep.Play();
                                //Player.Play("C:\\Users\\MarvinCao\\Desktop\\1.mp3");
                                RightImage.Source = VisualizeResult(e.Frame, errorList);
                                Indicator.Fill = Brushes.Red;
                                checkMove = false;
                                return;
                            }
                            else
                            {
                                checkMove = true;
                            }
                            
                            bool noEyeClosed = NoEyeClosed(errorList);
                            if (!noEyeClosed)
                            {
                                MessageArea.Text = "Please open your eyes！！！";
                                RightImage.Source = VisualizeResult(e.Frame, errorList);
                                Indicator.Fill = Brushes.Red;
                                return;
                            }
                            
                            if (smile)
                            {
                                bool happy = IsAllHappiness(errorList);
                                if (!happy)
                                {
                                    MessageArea.Text = "Please smile, Guys！！！";
                                    System.Media.SystemSounds.Beep.Play();
                                    //Player.Play("C:\\Users\\MarvinCao\\Desktop\\1.mp3");
                                    RightImage.Source = VisualizeResult(e.Frame, errorList);
                                    Indicator.Fill = Brushes.Red;
                                    return;
                                }
                            }
                            else
                            {
                                bool similar = IsEmotionCorrelation(errorList);
                                if (!similar)
                                {
                                    MessageArea.Text = "Please use a similar emotion, Guys！！！";
                                    RightImage.Source = VisualizeResult(e.Frame, errorList);
                                    Indicator.Fill = Brushes.Red;
                                    return;
                                }
                            }
                            start = false;
                            _grabber.StopProcessingAsync();
                            //MessageArea.Text = move.ToString();
                            RightImage.Source = null;
                            LeftImage.Source = e.Frame.Image.ToBitmapSource();
                            Indicator.Fill = Brushes.LightGreen;
                            System.Media.SystemSounds.Beep.Play();
                            MessageArea.Text = "拍照成功";
                        }
                    }
                }));
            };

            // Create local face detector. 
            _localFaceDetector.Load("Data/haarcascade_frontalface_alt2.xml");
        }

        private async Task<LiveCameraResult> FacesAndEmotionAnalysisFunction(VideoFrame frame)
        {
            // Encode image. 
            var jpg = frame.Image.ToMemoryStream(".jpg", s_jpegParams);

            // Submit image to Face API. 
            var attrs = new List<FaceAttributeType> { FaceAttributeType.Smile,
                FaceAttributeType.HeadPose };
            var faces = await _faceClient.DetectAsync(jpg, returnFaceLandmarks: true, returnFaceAttributes: attrs);
            // Count the Face API call. 
            Properties.Settings.Default.FaceAPICallCount++;

            // Encode image. 
            var jpg1 = frame.Image.ToMemoryStream(".jpg", s_jpegParams);

            // Submit image to Emotion API. 
            Emotion[] emotions = null;

            emotions = await _emotionClient.RecognizeAsync(jpg1);

            // Count the Emotion API call. 
            Properties.Settings.Default.EmotionAPICallCount++;

            // Output. 
            return new LiveCameraResult
            {
                //Faces = emotions.Select(e => CreateFace(e.FaceRectangle)).ToArray(),
                Faces = faces,
                // Extract emotion scores from results. 
                EmotionScores = emotions.Select(e => e.Scores).ToArray()
            };
        }

        /// <summary> Function which submits a frame to the Face API. </summary>
        /// <param name="frame"> The video frame to submit. </param>
        /// <returns> A <see cref="Task{LiveCameraResult}"/> representing the asynchronous API call,
        ///     and containing the faces returned by the API. </returns>
        private async Task<LiveCameraResult> FacesAnalysisFunction(VideoFrame frame)
        {
            // Encode image. 
            var jpg = frame.Image.ToMemoryStream(".jpg", s_jpegParams);
            // Submit image to API. 
            var attrs = new List<FaceAttributeType> { FaceAttributeType.Smile,
                FaceAttributeType.HeadPose };
            var faces = await _faceClient.DetectAsync(jpg, returnFaceLandmarks:true,returnFaceAttributes: attrs);
            // Count the API call. 
            Properties.Settings.Default.FaceAPICallCount++;
            // Output. 
            return new LiveCameraResult { Faces = faces };
        }

        /// <summary> Function which submits a frame to the Emotion API. </summary>
        /// <param name="frame"> The video frame to submit. </param>
        /// <returns> A <see cref="Task{LiveCameraResult}"/> representing the asynchronous API call,
        ///     and containing the emotions returned by the API. </returns>
        private async Task<LiveCameraResult> EmotionAnalysisFunction(VideoFrame frame)
        {
            // Encode image. 
            var jpg = frame.Image.ToMemoryStream(".jpg", s_jpegParams);
            // Submit image to API. 
            Emotion[] emotions = null;

            // See if we have local face detections for this image.
            var localFaces = (OpenCvSharp.Rect[])frame.UserData;
            if (localFaces != null)
            {
                // If we have local face detections, we can call the API with them. 
                // First, convert the OpenCvSharp rectangles. 
                var rects = localFaces.Select(
                    f => new Microsoft.ProjectOxford.Common.Rectangle
                    {
                        Left = f.Left,
                        Top = f.Top,
                        Width = f.Width,
                        Height = f.Height
                    });
                emotions = await _emotionClient.RecognizeAsync(jpg, rects.ToArray());
            }
            else
            {
                // If not, the API will do the face detection. 
                emotions = await _emotionClient.RecognizeAsync(jpg);
            }

            // Count the API call. 
            Properties.Settings.Default.EmotionAPICallCount++;
            // Output. 
            return new LiveCameraResult
            {
                Faces = emotions.Select(e => CreateFace(e.FaceRectangle)).ToArray(),
                // Extract emotion scores from results. 
                EmotionScores = emotions.Select(e => e.Scores).ToArray()
            };
        }

        /// <summary> Function which submits a frame to the Computer Vision API for tagging. </summary>
        /// <param name="frame"> The video frame to submit. </param>
        /// <returns> A <see cref="Task{LiveCameraResult}"/> representing the asynchronous API call,
        ///     and containing the tags returned by the API. </returns>
        private async Task<LiveCameraResult> TaggingAnalysisFunction(VideoFrame frame)
        {
            // Encode image. 
            var jpg = frame.Image.ToMemoryStream(".jpg", s_jpegParams);
            // Submit image to API. 
            var analysis = await _visionClient.GetTagsAsync(jpg);
            // Count the API call. 
            Properties.Settings.Default.VisionAPICallCount++;
            // Output. 
            return new LiveCameraResult { Tags = analysis.Tags };
        }

        /// <summary> Function which submits a frame to the Computer Vision API for celebrity
        ///     detection. </summary>
        /// <param name="frame"> The video frame to submit. </param>
        /// <returns> A <see cref="Task{LiveCameraResult}"/> representing the asynchronous API call,
        ///     and containing the celebrities returned by the API. </returns>
        private async Task<LiveCameraResult> CelebrityAnalysisFunction(VideoFrame frame)
        {
            // Encode image. 
            var jpg = frame.Image.ToMemoryStream(".jpg", s_jpegParams);
            // Submit image to API. 
            var result = await _visionClient.AnalyzeImageInDomainAsync(jpg, "celebrities");
            // Count the API call. 
            Properties.Settings.Default.VisionAPICallCount++;
            // Output. 
            var celebs = JsonConvert.DeserializeObject<CelebritiesResult>(result.Result.ToString()).Celebrities;
            return new LiveCameraResult
            {
                // Extract face rectangles from results. 
                Faces = celebs.Select(c => CreateFace(c.FaceRectangle)).ToArray(),
                // Extract celebrity names from results. 
                CelebrityNames = celebs.Select(c => c.Name).ToArray()
            };
        }

        private BitmapSource VisualizeResult(VideoFrame frame)
        {
            // Draw any results on top of the image. 
            BitmapSource visImage = frame.Image.ToBitmapSource();

            var result = _latestResultsToDisplay;

            if (result != null)
            {
                // See if we have local face detections for this image.
                var clientFaces = (OpenCvSharp.Rect[])frame.UserData;
                if (clientFaces != null && result.Faces != null)
                {
                    // If so, then the analysis results might be from an older frame. We need to match
                    // the client-side face detections (computed on this frame) with the analysis
                    // results (computed on the older frame) that we want to display. 
                    MatchAndReplaceFaceRectangles(result.Faces, clientFaces);
                }

                visImage = Visualization.DrawFaces(visImage, result.Faces, result.EmotionScores, result.CelebrityNames);
                visImage = Visualization.DrawTags(visImage, result.Tags);
            }

            return visImage;
        }

        private BitmapSource VisualizeResult(VideoFrame frame, ArrayList errorList)
        {
            // Draw any results on top of the image. 
            BitmapSource visImage = frame.Image.ToBitmapSource();

            var result = _latestResultsToDisplay;

            if (result != null)
            {

                visImage = Visualization.DrawFaces(visImage, result.Faces, result.EmotionScores, result.CelebrityNames, errorList);
                //visImage = Visualization.DrawTags(visImage, result.Tags);
            }

            return visImage;
        }

        /// <summary> Populate CameraList in the UI, once it is loaded. </summary>
        /// <param name="sender"> Source of the event. </param>
        /// <param name="e">      Routed event information. </param>
        private void CameraList_Loaded(object sender, RoutedEventArgs e)
        {
            int numCameras = _grabber.GetNumCameras();

            if (numCameras == 0)
            {
                MessageArea.Text = "No cameras found!";
            }

            var comboBox = sender as ComboBox;
            comboBox.ItemsSource = Enumerable.Range(0, numCameras).Select(i => string.Format("Camera {0}", i + 1));
            comboBox.SelectedIndex = 0;
        }

        /// <summary> Populate ModeList in the UI, once it is loaded. </summary>
        /// <param name="sender"> Source of the event. </param>
        /// <param name="e">      Routed event information. </param>
        private void ModeList_Loaded(object sender, RoutedEventArgs e)
        {
            var modes = (AppMode[])Enum.GetValues(typeof(AppMode));

            var comboBox = sender as ComboBox;
            comboBox.ItemsSource = modes.Select(m => m.ToString());
            comboBox.SelectedIndex = 0;
        }

        private void ModeList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Disable "most-recent" results display. 
            _fuseClientRemoteResults = false;

            var comboBox = sender as ComboBox;
            var modes = (AppMode[])Enum.GetValues(typeof(AppMode));
            _mode = modes[comboBox.SelectedIndex];
            switch (_mode)
            {
                case AppMode.Smile:
                    _grabber.AnalysisFunction = FacesAnalysisFunction;
                    smile = true;
                    break;
                case AppMode.SimilarEmotion:
                    _grabber.AnalysisFunction = FacesAndEmotionAnalysisFunction;
                    smile = false;
                    break;
                default:
                    _grabber.AnalysisFunction = null;
                    break;
            }
        }

        private async void StartButton_Click(object sender, RoutedEventArgs e)
        {
            if (!CameraList.HasItems)
            {
                MessageArea.Text = "No cameras found; cannot start processing";
                return;
            }

            // Clean leading/trailing spaces in API keys. 
            Properties.Settings.Default.FaceAPIKey = Properties.Settings.Default.FaceAPIKey.Trim();
            Properties.Settings.Default.EmotionAPIKey = Properties.Settings.Default.EmotionAPIKey.Trim();
            Properties.Settings.Default.VisionAPIKey = Properties.Settings.Default.VisionAPIKey.Trim();

            // Create API clients. 
            _faceClient = new FaceServiceClient(Properties.Settings.Default.FaceAPIKey);
            _emotionClient = new EmotionServiceClient(Properties.Settings.Default.EmotionAPIKey);
            _visionClient = new VisionServiceClient(Properties.Settings.Default.VisionAPIKey);

            // How often to analyze. 
            _grabber.TriggerAnalysisOnInterval(Properties.Settings.Default.AnalysisInterval);

            // Reset message. 
            MessageArea.Text = "";

            // Record start time, for auto-stop
            _startTime = DateTime.Now;
            start = true;
            Indicator.Fill = Brushes.Red;
            await _grabber.StartProcessingCameraAsync(CameraList.SelectedIndex);
        }

        private async void StopButton_Click(object sender, RoutedEventArgs e)
        {
            start = false;
            await _grabber.StopProcessingAsync();
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            SettingsPanel.Visibility = 1 - SettingsPanel.Visibility;
        }

        private void SaveSettingsButton_Click(object sender, RoutedEventArgs e)
        {
            SettingsPanel.Visibility = Visibility.Hidden;
            Properties.Settings.Default.Save();
        }

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri));
            e.Handled = true;
        }

        private Face CreateFace(FaceRectangle rect)
        {
            return new Face
            {
                FaceRectangle = new FaceRectangle
                {
                    Left = rect.Left,
                    Top = rect.Top,
                    Width = rect.Width,
                    Height = rect.Height
                }
            };
        }

        private Face CreateFace(Microsoft.ProjectOxford.Vision.Contract.FaceRectangle rect)
        {
            return new Face
            {
                FaceRectangle = new FaceRectangle
                {
                    Left = rect.Left,
                    Top = rect.Top,
                    Width = rect.Width,
                    Height = rect.Height
                }
            };
        }

        private Face CreateFace(Microsoft.ProjectOxford.Common.Rectangle rect)
        {
            return new Face
            {
                FaceRectangle = new FaceRectangle
                {
                    Left = rect.Left,
                    Top = rect.Top,
                    Width = rect.Width,
                    Height = rect.Height
                }
            };
        }

        private void MatchAndReplaceFaceRectangles(Face[] faces, OpenCvSharp.Rect[] clientRects)
        {
            // Use a simple heuristic for matching the client-side faces to the faces in the
            // results. Just sort both lists left-to-right, and assume a 1:1 correspondence. 

            // Sort the faces left-to-right. 
            var sortedResultFaces = faces
                .OrderBy(f => f.FaceRectangle.Left + 0.5 * f.FaceRectangle.Width)
                .ToArray();

            // Sort the clientRects left-to-right.
            var sortedClientRects = clientRects
                .OrderBy(r => r.Left + 0.5 * r.Width)
                .ToArray();

            // Assume that the sorted lists now corrrespond directly. We can simply update the
            // FaceRectangles in sortedResultFaces, because they refer to the same underlying
            // objects as the input "faces" array. 
            for (int i = 0; i < Math.Min(faces.Length, clientRects.Length); i++)
            {
                // convert from OpenCvSharp rectangles
                OpenCvSharp.Rect r = sortedClientRects[i];
                sortedResultFaces[i].FaceRectangle = new FaceRectangle { Left = r.Left, Top = r.Top, Width = r.Width, Height = r.Height };
            }
        }

        private bool IsStill(VideoFrame currFrame, ArrayList errorList)
        {
            if (_preFrame == null || _preResults == null)
            {
                _preFrame = currFrame;
                _preResults = _latestResultsToDisplay;    
                return false;
            }

            LiveCameraResult currResults = _latestResultsToDisplay;
            //Mat currMat = currFrame.Image;
            //var currIndexer = currMat.GetGenericIndexer<Vec3b>();

            //Mat preMat = _preFrame.Image;
            //var preIndexer = preMat.GetGenericIndexer<Vec3d>();
            LiveCameraResult preResults = _preResults;

            double avr = 0;

            if (currResults.Faces.Length != preResults.Faces.Length)
            {
                _preFrame = currFrame;
                _preResults = currResults;
                return false;
            }

            for (int i = 0; i < currResults.Faces.Length; i++)
            {
                avr = 0;
                Face currFaceResult = currResults.Faces[i];
                Face preFaceResult = preResults.Faces[i];
                avr += Math.Abs(currFaceResult.FaceRectangle.Left - preFaceResult.FaceRectangle.Left);
                avr += Math.Abs(currFaceResult.FaceRectangle.Top - preFaceResult.FaceRectangle.Top);
                if (avr > 27)
                {
                    _preFrame = currFrame;
                    _preResults = currResults;
                    errorList.Add(i);
                    return false;
                }
            }

            avr /= currResults.Faces.Length;
            /*
            for (int y = 0; y < currMat.Height; y++)
            {
                for (int x = 0; x < currMat.Width; x++)
                {
                    Vec3b color = currIndexer[y, x];
                    byte temp = color.Item0;
                    color.Item0 = color.Item2; // B <- R
                    color.Item2 = temp;        // R <- B
                    currIndexer[y, x] = color;
                }
            }
            */
            _preFrame = currFrame;
            _preResults = currResults;
            return true;
            //MessageArea.Text = avr.ToString();
        }

        private bool IsAllFaceCamera(ArrayList errorList)
        {
            LiveCameraResult currResults = _latestResultsToDisplay;
            int numFaces = currResults.Faces.Length;
            for (int i = 0; i < numFaces; i++)
            {
                Face face = currResults.Faces[i];
                if (Math.Abs(face.FaceAttributes.HeadPose.Yaw) > 25)
                {
                    errorList.Add(i);
                    return false;
                }
            }
            return true;
        }

        private bool HasPeople()
        {
            LiveCameraResult currResults = _latestResultsToDisplay;
            if (currResults.Faces.Length <= 0)
            {
                return false;
            }
            else
            {
                return true;
            }
        }

        private bool IsAllHappiness(ArrayList errorList)
        {
            bool isAllHappiness = true;
            LiveCameraResult lcr = _latestResultsToDisplay;
            //        MessageArea.Text = lcr.Faces[0].FaceAttributes.Smile.ToString();
            for (int i = 0; i < lcr.Faces.Length; i++)
            {
                if (lcr.Faces[i].FaceAttributes.Smile <= 0.5)
                {
                    isAllHappiness = false;
                    errorList.Add(i);
                }
            }

            //MessageArea.Text = isAllHappiness.ToString();
            return isAllHappiness;
        }

        private bool NoEyeClosed(ArrayList errorList)
        {
            double EYE_THRESHOLD = 0.15;
            LiveCameraResult currResults = _latestResultsToDisplay;
            bool flag = true;
            int numFaces = currResults.Faces.Length;
            for (int i = 0; i < numFaces; i++)
            {
                Face face = currResults.Faces[i];
                double left_eye_hight = Math.Abs(face.FaceLandmarks.EyeLeftBottom.Y - face.FaceLandmarks.EyeLeftTop.Y);
                double left_eye_width = Math.Abs(face.FaceLandmarks.EyeLeftInner.X - face.FaceLandmarks.EyeLeftOuter.X);

                double right_eye_hight = Math.Abs(face.FaceLandmarks.EyeRightBottom.Y - face.FaceLandmarks.EyeRightTop.Y);
                double right_eye_width = Math.Abs(face.FaceLandmarks.EyeRightInner.X - face.FaceLandmarks.EyeRightOuter.X);

                double left_eye = left_eye_hight / left_eye_width;
                double right_eye = right_eye_hight / right_eye_width;
                if (left_eye < EYE_THRESHOLD && right_eye < EYE_THRESHOLD)
                {
                    errorList.Add(i);
                    flag = false;
                }
            }
            return flag;
        }

        private bool IsEmotionCorrelation(ArrayList errorList)
        {
            bool isEmotionCorrelation = true;
            LiveCameraResult lcr = _latestResultsToDisplay;
            //        MessageArea.Text = lcr.Faces[0].FaceAttributes.Smile.ToString();

            //            Tuple<string, float> dominantEmotion = Aggregation.GetDominantEmotion(lcr.EmotionScores[0]);
            //           MessageArea.Text = dominantEmotion.Item1;

            string[] dominants = new string[lcr.EmotionScores.Length];

            for (int i = 0; i < lcr.EmotionScores.Length; i++)
            {
                dominants[i] = Aggregation.GetDominantEmotion(lcr.EmotionScores[i]).Item1;
            }
            var result = from item in dominants   //每一项                        
                         group item by item into gro   //按项分组，没组就是gro                        
                         orderby gro.Count() descending   //按照每组的数量进行排序                        
                         select new { num = gro.Key, nums = gro.Count() };   //返回匿名类型对象，输出这个组的值和这个值出现的次数   
            String emotion = "";
            foreach (var item in result.Take(1))
            {
                emotion = item.num;
            }

            for (int i = 0; i < dominants.Length; i++)
            {
                if (dominants[i] != emotion)
                {
                    errorList.Add(i);
                    isEmotionCorrelation = false;
                }
            }
            //MessageArea.Text = emotion;
            return isEmotionCorrelation;
        }
    }
}
