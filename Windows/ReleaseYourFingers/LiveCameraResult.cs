using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReleaseYourFingers
{
    // Class to hold all possible result types. 
    public class LiveCameraResult
    {
        public Microsoft.ProjectOxford.Face.Contract.Face[] Faces { get; set; } = null;
        public Microsoft.ProjectOxford.Emotion.Contract.Scores[] EmotionScores { get; set; } = null;
        public string[] CelebrityNames { get; set; } = null;
        public Microsoft.ProjectOxford.Vision.Contract.Tag[] Tags { get; set; } = null;
    }
}
