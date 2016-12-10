using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.ProjectOxford.Emotion.Contract;
using Microsoft.ProjectOxford.Face.Contract;

namespace ReleaseYourFingers
{
    internal class Aggregation
    {
        public static Tuple<string, float> GetDominantEmotion(Scores scores)
        {
            float maxScore = 0;
            string dominant = "";
            if (scores.Anger > maxScore) { maxScore = scores.Anger; dominant = "Anger"; }
            if (scores.Contempt > maxScore) { maxScore = scores.Contempt; dominant = "Contempt"; }
            if (scores.Disgust > maxScore) { maxScore = scores.Disgust; dominant = "Disgust"; }
            if (scores.Fear > maxScore) { maxScore = scores.Fear; dominant = "Fear"; }
            if (scores.Happiness > maxScore) { maxScore = scores.Happiness; dominant = "Happiness"; }
            if (scores.Neutral > maxScore) { maxScore = scores.Neutral; dominant = "Neutral"; }
            if (scores.Sadness > maxScore) { maxScore = scores.Sadness; dominant = "Sadness"; }
            if (scores.Surprise > maxScore) { maxScore = scores.Surprise; dominant = "Surprise"; }
            return new Tuple<string, float>(dominant, maxScore);
        }

        public static string SummarizeEmotion(Scores scores)
        {
            var bestEmotion = Aggregation.GetDominantEmotion(scores);
            return string.Format("{0}: {1:N1}", bestEmotion.Item1, bestEmotion.Item2);
        }

        public static string SummarizeFaceAttributes(FaceAttributes attr)
        {
            List<string> attrs = new List<string>();
            if (attr.Gender != null) attrs.Add(attr.Gender);
            if (attr.HeadPose != null)
            {
                // Simple rule to estimate whether person is facing camera. 
                bool facing = Math.Abs(attr.HeadPose.Yaw) < 20;
                attrs.Add(facing ? "facing camera" : "not facing camera");
            }
            return string.Join(", ", attrs);
        }
    }
}
